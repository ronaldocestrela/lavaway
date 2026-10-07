using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class TenantPlanQuotaApplicationServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private sealed class FakeSubscriptionRepository : ITenantSaasSubscriptionRepository
    {
        public readonly Dictionary<Guid, TenantSaasSubscription> Subscriptions = [];

        public Task<TenantSaasSubscription?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Subscriptions.GetValueOrDefault(tenantId));

        public Task<TenantSaasSubscription?> GetByGatewaySubscriptionIdAsync(string gatewaySubscriptionId, CancellationToken ct = default) =>
            Task.FromResult(Subscriptions.Values.FirstOrDefault(s => s.GatewaySubscriptionId == gatewaySubscriptionId));

        public Task<IReadOnlyList<TenantSaasSubscription>> ListAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TenantSaasSubscription>>(Subscriptions.Values.ToList());

        public Task AddAsync(TenantSaasSubscription subscription, CancellationToken ct = default)
        {
            Subscriptions[subscription.TenantId] = subscription;
            return Task.CompletedTask;
        }

        public void Update(TenantSaasSubscription subscription) => Subscriptions[subscription.TenantId] = subscription;

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeQuotaUsageRepository : ITenantQuotaUsageRepository
    {
        public readonly Dictionary<Guid, TenantQuotaUsage> Quotas = [];

        public Task<TenantQuotaUsage?> GetCurrentCycleByTenantIdAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Quotas.GetValueOrDefault(tenantId));

        public Task AddAsync(TenantQuotaUsage quotaUsage, CancellationToken ct = default)
        {
            Quotas[quotaUsage.TenantId] = quotaUsage;
            return Task.CompletedTask;
        }

        public void Update(TenantQuotaUsage quotaUsage) => Quotas[quotaUsage.TenantId] = quotaUsage;

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakePlanRepository : ISaasPlanRepository
    {
        public readonly List<SaasPlan> Plans = [.. SaasPlan.GetStandardPlans()];

        public Task<IReadOnlyList<SaasPlan>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SaasPlan>>(Plans.Where(p => p.IsActive).ToList());

        public Task<SaasPlan?> GetByTierAsync(SaasPlanTier tier, CancellationToken ct = default) =>
            Task.FromResult(Plans.FirstOrDefault(p => p.Tier == tier));

        public Task AddAsync(SaasPlan plan, CancellationToken ct = default)
        {
            Plans.Add(plan);
            return Task.CompletedTask;
        }

        public Task EnsureSeedDataAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeInvoiceRepository : ISaasInvoiceRepository
    {
        public readonly List<SaasInvoice> Invoices = [];

        public Task<SaasInvoice?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Invoices.FirstOrDefault(i => i.Id == id));

        public Task<SaasInvoice?> GetByGatewayInvoiceIdAsync(string gatewayInvoiceId, CancellationToken ct = default) =>
            Task.FromResult(Invoices.FirstOrDefault(i => i.GatewayInvoiceId == gatewayInvoiceId));

        public Task<IReadOnlyList<SaasInvoice>> ListByTenantIdAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SaasInvoice>>(Invoices.Where(i => i.TenantId == tenantId).ToList());

        public Task<SaasInvoice?> GetPendingByTenantIdAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Invoices.FirstOrDefault(i => i.TenantId == tenantId && (i.Status == "Pending" || i.Status == "Overdue")));

        public Task AddAsync(SaasInvoice invoice, CancellationToken ct = default)
        {
            Invoices.Add(invoice);
            return Task.CompletedTask;
        }

        public void Update(SaasInvoice invoice) { }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeGlobalTenantLookup : IGlobalTenantLookup
    {
        public Task<Result<bool>> ExistsAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<bool>.Success(true));

        public Task<Result<GlobalTenantSummaryDto>> GetSummaryAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<GlobalTenantSummaryDto>.Success(new GlobalTenantSummaryDto(
                Id: tenantId,
                Name: "Lava Jato Express",
                Status: TenantStatus.Active,
                CreatedAtUtc: DateTimeOffset.UtcNow,
                StatusChangedAtUtc: DateTimeOffset.UtcNow,
                TrialEndsAtUtc: null,
                StatusReason: null,
                TradeName: null,
                LegalName: null,
                Cnpj: null,
                Phone: null,
                City: null,
                State: null)));

        public Task<Result<GlobalTenantSummaryDto>> UpdateTenantStatusAsync(
            Guid tenantId,
            UpdateTenantStatusRequest request,
            CancellationToken ct = default) =>
            GetSummaryAsync(tenantId, ct);
    }

    [Fact]
    public async Task CheckWorkOrderQuotaAsync_Should_Allow_When_Under_Plan_Limit()
    {
        var subRepo = new FakeSubscriptionRepository();
        var quotaRepo = new FakeQuotaUsageRepository();
        var planRepo = new FakePlanRepository();
        var invoiceRepo = new FakeInvoiceRepository();
        var tenantLookup = new FakeGlobalTenantLookup();

        var service = new TenantPlanQuotaApplicationService(subRepo, quotaRepo, planRepo, invoiceRepo, tenantLookup);

        var result = await service.CheckWorkOrderQuotaAsync(_tenantId);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.CanProceed);
        Assert.Equal(0, result.Value.UsedWorkOrders);
    }

    [Fact]
    public async Task ConsumeWorkOrderQuotaAsync_Should_Block_When_Tenant_Is_Delinquent()
    {
        var subRepo = new FakeSubscriptionRepository();
        var quotaRepo = new FakeQuotaUsageRepository();
        var planRepo = new FakePlanRepository();
        var invoiceRepo = new FakeInvoiceRepository();
        var tenantLookup = new FakeGlobalTenantLookup();

        var basicPlan = planRepo.Plans.First(p => p.Tier == SaasPlanTier.Basic);
        var sub = TenantSaasSubscription.CreateActive(_tenantId, basicPlan, "cus_1", "sub_1").Value!;
        sub.Suspend("Pagamento atrasado.");
        subRepo.Subscriptions[_tenantId] = sub;

        var service = new TenantPlanQuotaApplicationService(subRepo, quotaRepo, planRepo, invoiceRepo, tenantLookup);

        var checkResult = await service.CheckWorkOrderQuotaAsync(_tenantId);
        Assert.True(checkResult.IsSuccess);
        Assert.False(checkResult.Value!.CanProceed, "Tenant com assinatura suspensa não deve poder abrir OS");

        var consumeResult = await service.ConsumeWorkOrderQuotaAsync(_tenantId);
        Assert.False(consumeResult.IsSuccess);
        Assert.Equal("tenant.subscription.delinquent", consumeResult.Error!.Code);
    }

    [Fact]
    public async Task ConsumeWorkOrderQuotaAsync_Should_Increment_And_Block_When_Limit_Reached()
    {
        var subRepo = new FakeSubscriptionRepository();
        var quotaRepo = new FakeQuotaUsageRepository();
        var planRepo = new FakePlanRepository();
        var invoiceRepo = new FakeInvoiceRepository();
        var tenantLookup = new FakeGlobalTenantLookup();

        var basicPlan = planRepo.Plans.First(p => p.Tier == SaasPlanTier.Basic); // 150 OS
        var sub = TenantSaasSubscription.CreateActive(_tenantId, basicPlan, "cus_1", "sub_1").Value!;
        subRepo.Subscriptions[_tenantId] = sub;

        var quota = TenantQuotaUsage.CreateForCycle(_tenantId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1)).Value!;
        for (int i = 0; i < 149; i++)
        {
            quota.RecordWorkOrderCreated(150);
        }
        quotaRepo.Quotas[_tenantId] = quota;

        var service = new TenantPlanQuotaApplicationService(subRepo, quotaRepo, planRepo, invoiceRepo, tenantLookup);

        // 150ª OS permitida
        var r1 = await service.ConsumeWorkOrderQuotaAsync(_tenantId);
        Assert.True(r1.IsSuccess);

        // 151ª OS deve ser bloqueada por estouro de cota
        var r2 = await service.ConsumeWorkOrderQuotaAsync(_tenantId);
        Assert.False(r2.IsSuccess);
        Assert.Equal("tenant.quota.work_orders_exceeded", r2.Error!.Code);
    }
}
