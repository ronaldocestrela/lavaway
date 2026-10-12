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

        public Task<TenantSaasSubscription?> GetByGatewayCustomerIdAsync(string gatewayCustomerId, CancellationToken ct = default) =>
            Task.FromResult(Subscriptions.Values.FirstOrDefault(s => s.GatewayCustomerId == gatewayCustomerId));

        public Task<IReadOnlyList<TenantSaasSubscription>> ListAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TenantSaasSubscription>>(Subscriptions.Values.ToList());

        public Task AddAsync(TenantSaasSubscription subscription, CancellationToken ct = default)
        {
            Subscriptions[subscription.TenantId] = subscription;
            return Task.CompletedTask;
        }

        public void Update(TenantSaasSubscription subscription) => Subscriptions[subscription.TenantId] = subscription;

        public void SetTenantContext(Guid tenantId) { }

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

        public Task<IReadOnlyList<SaasPlan>> ListAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SaasPlan>>(Plans.ToList());

        public Task<SaasPlan?> GetByTierAsync(SaasPlanTier tier, CancellationToken ct = default) =>
            Task.FromResult(Plans.FirstOrDefault(p => p.Tier == tier));

        public Task AddAsync(SaasPlan plan, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(SaasPlan plan, CancellationToken ct = default)
        {
            var idx = Plans.FindIndex(p => p.Tier == plan.Tier);
            if (idx >= 0) Plans[idx] = plan;
            return Task.CompletedTask;
        }

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

        public Task<Result<GatewayCheckoutSessionResult>> CreateSubscriptionCheckoutSessionAsync(Guid tenantId, string customerName, string customerEmail, SaasPlan plan, string successUrl, string cancelUrl, CancellationToken ct = default) =>
            Task.FromResult(Result<GatewayCheckoutSessionResult>.Success(new GatewayCheckoutSessionResult("cs_1", "https://checkout.stripe.com/1")));

        public Task<Result<GatewayCheckoutSessionDetails>> GetCheckoutSessionAsync(string sessionId, CancellationToken ct = default) =>
            Task.FromResult(Result<GatewayCheckoutSessionDetails>.Success(new GatewayCheckoutSessionDetails("cs_1", Guid.Empty, SaasPlanTier.Enterprise, "cus_1", "sub_1", "paid", "complete")));

        public Task<Result<GatewayPortalSessionResult>> CreateCustomerPortalSessionAsync(string gatewayCustomerId, string returnUrl, CancellationToken ct = default) =>
            Task.FromResult(Result<GatewayPortalSessionResult>.Success(new GatewayPortalSessionResult("https://billing.stripe.com/p/1")));

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

    [Fact]
    public async Task CreateCheckoutSessionAsync_WithValidTier_Should_Return_Session_Url()
    {
        var subRepo = new FakeSubscriptionRepository();
        var quotaRepo = new FakeQuotaUsageRepository();
        var invoiceRepo = new FakeInvoiceRepository();
        var planRepo = new FakePlanRepository();
        var gateway = new FakeGatewayProvider();
        var tenantLookup = new FakeGlobalTenantLookup();
        var quotaLookup = new TenantPlanQuotaApplicationService(subRepo, quotaRepo, planRepo, invoiceRepo, tenantLookup);

        var service = new TenantSaasSubscriptionApplicationService(
            subRepo, quotaRepo, invoiceRepo, planRepo, gateway, tenantLookup, quotaLookup);

        var result = await service.CreateCheckoutSessionAsync(_tenantId, SaasPlanTier.Enterprise);

        Assert.True(result.IsSuccess);
        Assert.Equal("cs_1", result.Value!.SessionId);
        Assert.Equal("https://checkout.stripe.com/1", result.Value.CheckoutUrl);
    }

    [Fact]
    public async Task CreateCustomerPortalSessionAsync_WhenCustomerExists_Should_Return_Portal_Url()
    {
        var subRepo = new FakeSubscriptionRepository();
        var quotaRepo = new FakeQuotaUsageRepository();
        var invoiceRepo = new FakeInvoiceRepository();
        var planRepo = new FakePlanRepository();
        var gateway = new FakeGatewayProvider();
        var tenantLookup = new FakeGlobalTenantLookup();
        var quotaLookup = new TenantPlanQuotaApplicationService(subRepo, quotaRepo, planRepo, invoiceRepo, tenantLookup);

        var plan = planRepo.Plans.First(p => p.Tier == SaasPlanTier.Pro);
        var sub = TenantSaasSubscription.CreateActive(_tenantId, plan, "cus_stripe_123", "sub_stripe_123").Value!;
        subRepo.Subscriptions[_tenantId] = sub;

        var service = new TenantSaasSubscriptionApplicationService(
            subRepo, quotaRepo, invoiceRepo, planRepo, gateway, tenantLookup, quotaLookup);

        var result = await service.CreateCustomerPortalSessionAsync(_tenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal("https://billing.stripe.com/p/1", result.Value!.PortalUrl);
    }

    [Fact]
    public async Task SyncCheckoutSessionAsync_WhenSessionPaid_Should_Upgrade_Plan_And_Activate()
    {
        var subRepo = new FakeSubscriptionRepository();
        var quotaRepo = new FakeQuotaUsageRepository();
        var invoiceRepo = new FakeInvoiceRepository();
        var planRepo = new FakePlanRepository();
        var gateway = new FakeGatewayProvider();
        var tenantLookup = new FakeGlobalTenantLookup();
        var quotaLookup = new TenantPlanQuotaApplicationService(subRepo, quotaRepo, planRepo, invoiceRepo, tenantLookup);

        var basicPlan = planRepo.Plans.First(p => p.Tier == SaasPlanTier.Basic);
        var sub = TenantSaasSubscription.CreateActive(_tenantId, basicPlan, "cus_old", "sub_old").Value!;
        subRepo.Subscriptions[_tenantId] = sub;

        var service = new TenantSaasSubscriptionApplicationService(
            subRepo, quotaRepo, invoiceRepo, planRepo, gateway, tenantLookup, quotaLookup);

        var result = await service.SyncCheckoutSessionAsync(_tenantId, "cs_1");

        Assert.True(result.IsSuccess);
        Assert.Equal(SaasPlanTier.Enterprise, result.Value!.PlanTier);
        Assert.Equal(TenantSubscriptionStatus.Active, result.Value.Status);
        Assert.Equal(SaasPlanTier.Enterprise, sub.PlanTier);
    }
}

