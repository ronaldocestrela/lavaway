using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class TenantSaasSubscriptionApplicationServiceTests
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

    private sealed class FakePlanRepository : ISaasPlanRepository
    {
        public readonly List<SaasPlan> Plans = [.. SaasPlan.GetStandardPlans()];

        public Task<IReadOnlyList<SaasPlan>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SaasPlan>>(Plans.Where(p => p.IsActive).ToList());

        public Task<SaasPlan?> GetByTierAsync(SaasPlanTier tier, CancellationToken ct = default) =>
            Task.FromResult(Plans.FirstOrDefault(p => p.Tier == tier));

        public Task AddAsync(SaasPlan plan, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureSeedDataAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeGatewayProvider : ISaasBillingGatewayProvider
    {
        public Task<Result<GatewaySubscriptionResult>> CreateSubscriptionAsync(Guid tenantId, string customerName, string customerEmail, SaasPlan plan, CancellationToken ct = default) =>
            Task.FromResult(Result<GatewaySubscriptionResult>.Success(new GatewaySubscriptionResult("cus_1", "sub_1", "inv_1", "https://pay/1", "qr", "copia")));

        public Task<Result<bool>> ChangeSubscriptionPlanAsync(string gatewaySubscriptionId, SaasPlan newPlan, CancellationToken ct = default) =>
            Task.FromResult(Result<bool>.Success(true));

        public Task<Result<GatewayInvoiceResult>> GenerateInvoiceAsync(Guid tenantId, string gatewayCustomerId, decimal amount, DateTimeOffset dueDateUtc, CancellationToken ct = default) =>
            Task.FromResult(Result<GatewayInvoiceResult>.Success(new GatewayInvoiceResult("inv_1", amount, dueDateUtc, "https://pay/1", "qr", "copia")));

        public bool VerifyWebhookSignature(string payload, string signatureHeader, string secret) => true;
    }

    private sealed class FakeGlobalTenantLookup : IGlobalTenantLookup
    {
        public TenantStatus Status { get; set; } = TenantStatus.Trial;

        public Task<Result<bool>> ExistsAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<bool>.Success(true));

        public Task<Result<GlobalTenantSummaryDto>> GetSummaryAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<GlobalTenantSummaryDto>.Success(new GlobalTenantSummaryDto(
                Id: tenantId,
                Name: "Lava Jato Central",
                Status: Status,
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
            CancellationToken ct = default)
        {
            Status = request.NewStatus;
            return GetSummaryAsync(tenantId, ct);
        }
    }

    [Fact]
    public async Task ChangePlanAsync_From_Trial_Should_Activate_And_Create_Pending_Invoice()
    {
        var subRepo = new FakeSubscriptionRepository();
        var quotaRepo = new FakeQuotaUsageRepository();
        var invoiceRepo = new FakeInvoiceRepository();
        var planRepo = new FakePlanRepository();
        var gateway = new FakeGatewayProvider();
        var tenantLookup = new FakeGlobalTenantLookup();

        var sub = TenantSaasSubscription.CreateTrial(_tenantId).Value!;
        subRepo.Subscriptions[_tenantId] = sub;

        var quotaLookup = new TenantPlanQuotaApplicationService(subRepo, quotaRepo, planRepo, invoiceRepo, tenantLookup);
        var service = new TenantSaasSubscriptionApplicationService(
            subRepo, quotaRepo, invoiceRepo, planRepo, gateway, tenantLookup, quotaLookup);

        var result = await service.ChangePlanAsync(_tenantId, SaasPlanTier.Enterprise);

        Assert.True(result.IsSuccess);
        Assert.Equal(SaasPlanTier.Enterprise, result.Value!.PlanTier);
        Assert.Equal(TenantSubscriptionStatus.Active, result.Value.Status);
        Assert.Equal(599.00m, result.Value.MonthlyPrice);
        Assert.Single(invoiceRepo.Invoices);
        Assert.Equal("Pending", invoiceRepo.Invoices[0].Status);
        Assert.Equal(599.00m, invoiceRepo.Invoices[0].Amount);
    }

    [Fact]
    public async Task SimulateSettleInvoiceAsync_Should_Mark_Invoice_Paid_And_Activate_Tenant()
    {
        var subRepo = new FakeSubscriptionRepository();
        var quotaRepo = new FakeQuotaUsageRepository();
        var invoiceRepo = new FakeInvoiceRepository();
        var planRepo = new FakePlanRepository();
        var gateway = new FakeGatewayProvider();
        var tenantLookup = new FakeGlobalTenantLookup();

        var plan = planRepo.Plans.First(p => p.Tier == SaasPlanTier.Pro);
        var sub = TenantSaasSubscription.CreateActive(_tenantId, plan, "cus_1", "sub_1").Value!;
        sub.Suspend("Inadimplente");
        subRepo.Subscriptions[_tenantId] = sub;
        tenantLookup.Status = TenantStatus.Delinquent;

        var invoice = SaasInvoice.CreatePending(_tenantId, "inv_1", 299.00m, DateTimeOffset.UtcNow).Value!;
        invoiceRepo.Invoices.Add(invoice);

        var quotaLookup = new TenantPlanQuotaApplicationService(subRepo, quotaRepo, planRepo, invoiceRepo, tenantLookup);
        var service = new TenantSaasSubscriptionApplicationService(
            subRepo, quotaRepo, invoiceRepo, planRepo, gateway, tenantLookup, quotaLookup);

        var result = await service.SimulateSettleInvoiceAsync(_tenantId, invoice.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantSubscriptionStatus.Active, result.Value!.Status);
        Assert.Equal(TenantStatus.Active, tenantLookup.Status);
        Assert.Equal("Paid", invoice.Status);
        Assert.NotNull(invoice.PaidAtUtc);
    }
}
