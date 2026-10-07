using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class SaasBillingWebhookApplicationServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private sealed class FakeProcessedEventsRepository : IProcessedSaasWebhookEventRepository
    {
        public readonly HashSet<string> ProcessedEvents = [];

        public Task<bool> HasBeenProcessedAsync(string eventId, CancellationToken ct = default) =>
            Task.FromResult(ProcessedEvents.Contains(eventId));

        public Task AddAsync(ProcessedSaasWebhookEvent webhookEvent, CancellationToken ct = default)
        {
            ProcessedEvents.Add(webhookEvent.EventId);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeGatewayProvider : ISaasBillingGatewayProvider
    {
        public Task<Result<GatewaySubscriptionResult>> CreateSubscriptionAsync(Guid tenantId, string customerName, string customerEmail, SaasPlan plan, CancellationToken ct = default) =>
            Task.FromResult(Result<GatewaySubscriptionResult>.Success(new GatewaySubscriptionResult("cus_1", "sub_1", "inv_1", null, null, null)));

        public Task<Result<bool>> ChangeSubscriptionPlanAsync(string gatewaySubscriptionId, SaasPlan newPlan, CancellationToken ct = default) =>
            Task.FromResult(Result<bool>.Success(true));

        public Task<Result<GatewayInvoiceResult>> GenerateInvoiceAsync(Guid tenantId, string gatewayCustomerId, decimal amount, DateTimeOffset dueDateUtc, CancellationToken ct = default) =>
            Task.FromResult(Result<GatewayInvoiceResult>.Success(new GatewayInvoiceResult("inv_1", amount, dueDateUtc, null, null, null)));

        public bool VerifyWebhookSignature(string payload, string signatureHeader, string secret) => true;
    }

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

    private sealed class FakeGlobalTenantLookup : IGlobalTenantLookup
    {
        public TenantStatus CurrentStatus { get; private set; } = TenantStatus.Trial;

        public Task<Result<bool>> ExistsAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<bool>.Success(true));

        public Task<Result<GlobalTenantSummaryDto>> GetSummaryAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<GlobalTenantSummaryDto>.Success(new GlobalTenantSummaryDto(
                Id: tenantId,
                Name: "Lava Jato VIP",
                Status: CurrentStatus,
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
            CurrentStatus = request.NewStatus;
            return GetSummaryAsync(tenantId, ct);
        }
    }

    [Fact]
    public async Task ProcessWebhook_InvoicePaid_Should_Activate_Subscription_And_TenantStatus()
    {
        var gateway = new FakeGatewayProvider();
        var processedRepo = new FakeProcessedEventsRepository();
        var subRepo = new FakeSubscriptionRepository();
        var quotaRepo = new FakeQuotaUsageRepository();
        var invoiceRepo = new FakeInvoiceRepository();
        var planRepo = new FakePlanRepository();
        var tenantLookup = new FakeGlobalTenantLookup();

        var sub = TenantSaasSubscription.CreateTrial(_tenantId).Value!;
        subRepo.Subscriptions[_tenantId] = sub;

        var quota = TenantQuotaUsage.CreateForCycle(_tenantId, DateTimeOffset.UtcNow.AddDays(-15), DateTimeOffset.UtcNow.AddDays(15)).Value!;
        quota.RecordWorkOrderCreated(150);
        quotaRepo.Quotas[_tenantId] = quota;

        var invoice = SaasInvoice.CreatePending(_tenantId, "inv_999", 299.00m, DateTimeOffset.UtcNow).Value!;
        invoiceRepo.Invoices.Add(invoice);

        var service = new SaasBillingWebhookApplicationService(
            gateway, processedRepo, subRepo, quotaRepo, invoiceRepo, planRepo, tenantLookup);

        var payload = new SaasBillingWebhookPayload(
            EventId: "evt_101",
            EventType: "invoice.paid",
            TenantId: _tenantId,
            GatewaySubscriptionId: "sub_1",
            GatewayInvoiceId: "inv_999",
            Amount: 299.00m,
            TimestampUtc: DateTimeOffset.UtcNow);

        var result = await service.ProcessWebhookAsync(payload);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantSubscriptionStatus.Active, sub.Status);
        Assert.Equal(TenantStatus.Active, tenantLookup.CurrentStatus);
        Assert.Equal("Paid", invoice.Status);
        Assert.Equal(0, quota.WorkOrdersCreatedCount);
        Assert.Contains("evt_101", processedRepo.ProcessedEvents);
    }

    [Fact]
    public async Task ProcessWebhook_Should_Be_Idempotent_When_EventId_Is_Repeated()
    {
        var gateway = new FakeGatewayProvider();
        var processedRepo = new FakeProcessedEventsRepository();
        processedRepo.ProcessedEvents.Add("evt_duplicate");

        var subRepo = new FakeSubscriptionRepository();
        var quotaRepo = new FakeQuotaUsageRepository();
        var invoiceRepo = new FakeInvoiceRepository();
        var planRepo = new FakePlanRepository();
        var tenantLookup = new FakeGlobalTenantLookup();

        var service = new SaasBillingWebhookApplicationService(
            gateway, processedRepo, subRepo, quotaRepo, invoiceRepo, planRepo, tenantLookup);

        var payload = new SaasBillingWebhookPayload(
            EventId: "evt_duplicate",
            EventType: "invoice.paid",
            TenantId: _tenantId,
            GatewaySubscriptionId: null,
            GatewayInvoiceId: null,
            Amount: null,
            TimestampUtc: DateTimeOffset.UtcNow);

        var result = await service.ProcessWebhookAsync(payload);

        Assert.True(result.IsSuccess);
        Assert.Empty(subRepo.Subscriptions);
    }
}
