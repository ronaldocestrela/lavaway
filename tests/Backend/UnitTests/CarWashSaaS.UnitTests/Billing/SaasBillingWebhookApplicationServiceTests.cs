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

        public Task<Result<GatewayCheckoutSessionResult>> CreateSubscriptionCheckoutSessionAsync(Guid tenantId, string customerName, string customerEmail, SaasPlan plan, string successUrl, string cancelUrl, CancellationToken ct = default) =>
            Task.FromResult(Result<GatewayCheckoutSessionResult>.Success(new GatewayCheckoutSessionResult("cs_1", "https://checkout.stripe.com/1")));

        public Task<Result<GatewayCheckoutSessionDetails>> GetCheckoutSessionAsync(string sessionId, CancellationToken ct = default) =>
            Task.FromResult(Result<GatewayCheckoutSessionDetails>.Success(new GatewayCheckoutSessionDetails("cs_1", Guid.Empty, SaasPlanTier.Enterprise, "cus_1", "sub_1", "paid", "complete")));

        public Task<Result<GatewayPortalSessionResult>> CreateCustomerPortalSessionAsync(string gatewayCustomerId, string returnUrl, CancellationToken ct = default) =>
            Task.FromResult(Result<GatewayPortalSessionResult>.Success(new GatewayPortalSessionResult("https://billing.stripe.com/p/1")));

        public bool VerifyWebhookSignature(string payload, string signatureHeader, string secret) => true;
    }

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

    [Fact]
    public async Task ProcessWebhook_Should_Activate_Subscription_On_CheckoutSessionCompleted()
    {
        var gateway = new FakeGatewayProvider();
        var processedRepo = new FakeProcessedEventsRepository();
        var subRepo = new FakeSubscriptionRepository();
        var quotaRepo = new FakeQuotaUsageRepository();
        var invoiceRepo = new FakeInvoiceRepository();
        var planRepo = new FakePlanRepository();
        var tenantLookup = new FakeGlobalTenantLookup();

        var plan = planRepo.Plans.First(p => p.Tier == SaasPlanTier.Pro);
        var sub = TenantSaasSubscription.CreateTrial(_tenantId).Value!;
        subRepo.Subscriptions[_tenantId] = sub;

        var quota = TenantQuotaUsage.CreateForCycle(_tenantId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(14)).Value!;
        quota.RecordWorkOrderCreated(1);
        quotaRepo.Quotas[_tenantId] = quota;

        var service = new SaasBillingWebhookApplicationService(
            gateway, processedRepo, subRepo, quotaRepo, invoiceRepo, planRepo, tenantLookup);

        var payload = new SaasBillingWebhookPayload(
            EventId: "evt_stripe_checkout_1",
            EventType: "checkout.session.completed",
            TenantId: _tenantId,
            GatewaySubscriptionId: "sub_stripe_abc",
            GatewayInvoiceId: null,
            Amount: 149.00m,
            TimestampUtc: DateTimeOffset.UtcNow,
            GatewayCustomerId: "cus_stripe_xyz");

        var result = await service.ProcessWebhookAsync(payload);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantSubscriptionStatus.Active, sub.Status);
        Assert.Equal("sub_stripe_abc", sub.GatewaySubscriptionId);
        Assert.Equal("cus_stripe_xyz", sub.GatewayCustomerId);
        Assert.Equal(TenantStatus.Active, tenantLookup.CurrentStatus);
        Assert.Equal(0, quota.WorkOrdersCreatedCount);
        Assert.Contains("evt_stripe_checkout_1", processedRepo.ProcessedEvents);
    }

    [Theory]
    [InlineData("customer.created")]
    [InlineData("payment_intent.created")]
    [InlineData("setup_intent.created")]
    [InlineData("charge.succeeded")]
    public async Task ProcessWebhook_Should_Acknowledge_NonActionable_Event_Without_Tenant_With_Success(string eventType)
    {
        var gateway = new FakeGatewayProvider();
        var processedRepo = new FakeProcessedEventsRepository();
        var subRepo = new FakeSubscriptionRepository();
        var quotaRepo = new FakeQuotaUsageRepository();
        var invoiceRepo = new FakeInvoiceRepository();
        var planRepo = new FakePlanRepository();
        var tenantLookup = new FakeGlobalTenantLookup();

        var service = new SaasBillingWebhookApplicationService(
            gateway, processedRepo, subRepo, quotaRepo, invoiceRepo, planRepo, tenantLookup);

        // Cenário exato do erro relatado: evento como payment_intent.created ou customer.created sem metadata de tenant
        var payload = new SaasBillingWebhookPayload(
            EventId: "evt_1UPjyIAEtNt6k2IqA6kjeZJl",
            EventType: eventType,
            TenantId: Guid.Empty,
            GatewaySubscriptionId: null,
            GatewayInvoiceId: null,
            Amount: null,
            TimestampUtc: DateTimeOffset.UtcNow,
            GatewayCustomerId: "cus_mock_123");

        var result = await service.ProcessWebhookAsync(payload);

        Assert.True(result.IsSuccess);
        Assert.Contains("evt_1UPjyIAEtNt6k2IqA6kjeZJl", processedRepo.ProcessedEvents);
        Assert.Empty(subRepo.Subscriptions);
    }

    [Fact]
    public async Task ProcessWebhook_Should_Resolve_Tenant_Via_GatewayCustomerId_When_TenantId_Is_Empty()
    {
        var gateway = new FakeGatewayProvider();
        var processedRepo = new FakeProcessedEventsRepository();
        var subRepo = new FakeSubscriptionRepository();
        var quotaRepo = new FakeQuotaUsageRepository();
        var invoiceRepo = new FakeInvoiceRepository();
        var planRepo = new FakePlanRepository();
        var tenantLookup = new FakeGlobalTenantLookup();

        var sub = TenantSaasSubscription.CreateTrial(_tenantId).Value!;
        sub.Activate(planRepo.Plans.First(p => p.Tier == SaasPlanTier.Pro), "sub_xyz", "cus_test_matched");
        subRepo.Subscriptions[_tenantId] = sub;

        var invoice = SaasInvoice.CreatePending(_tenantId, "inv_match_1", 299.00m, DateTimeOffset.UtcNow).Value!;
        invoiceRepo.Invoices.Add(invoice);

        var service = new SaasBillingWebhookApplicationService(
            gateway, processedRepo, subRepo, quotaRepo, invoiceRepo, planRepo, tenantLookup);

        var payload = new SaasBillingWebhookPayload(
            EventId: "evt_invoice_paid_no_tenant",
            EventType: "invoice.paid",
            TenantId: Guid.Empty, // TenantId não veio no payload raiz
            GatewaySubscriptionId: "sub_xyz",
            GatewayInvoiceId: "inv_match_1",
            Amount: 299.00m,
            TimestampUtc: DateTimeOffset.UtcNow,
            GatewayCustomerId: "cus_test_matched");

        var result = await service.ProcessWebhookAsync(payload);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantSubscriptionStatus.Active, sub.Status);
        Assert.Equal("Paid", invoice.Status);
        Assert.Contains("evt_invoice_paid_no_tenant", processedRepo.ProcessedEvents);
    }

    [Fact]
    public async Task ProcessWebhook_Should_Acknowledge_Actionable_Event_When_Tenant_Cannot_Be_Resolved()
    {
        var gateway = new FakeGatewayProvider();
        var processedRepo = new FakeProcessedEventsRepository();
        var subRepo = new FakeSubscriptionRepository();
        var quotaRepo = new FakeQuotaUsageRepository();
        var invoiceRepo = new FakeInvoiceRepository();
        var planRepo = new FakePlanRepository();
        var tenantLookup = new FakeGlobalTenantLookup();

        var service = new SaasBillingWebhookApplicationService(
            gateway, processedRepo, subRepo, quotaRepo, invoiceRepo, planRepo, tenantLookup);

        var payload = new SaasBillingWebhookPayload(
            EventId: "evt_orphan_invoice_paid",
            EventType: "invoice.paid",
            TenantId: Guid.Empty,
            GatewaySubscriptionId: "sub_unknown",
            GatewayInvoiceId: "inv_unknown",
            Amount: 199.00m,
            TimestampUtc: DateTimeOffset.UtcNow,
            GatewayCustomerId: "cus_unknown");

        var result = await service.ProcessWebhookAsync(payload);

        Assert.True(result.IsSuccess);
        Assert.Contains("evt_orphan_invoice_paid", processedRepo.ProcessedEvents);
    }

    [Fact]
    public async Task ProcessWebhook_Should_Upgrade_Plan_From_Basic_To_Enterprise_On_CheckoutSessionCompleted()
    {
        var gateway = new FakeGatewayProvider();
        var processedRepo = new FakeProcessedEventsRepository();
        var subRepo = new FakeSubscriptionRepository();
        var quotaRepo = new FakeQuotaUsageRepository();
        var invoiceRepo = new FakeInvoiceRepository();
        var planRepo = new FakePlanRepository();
        var tenantLookup = new FakeGlobalTenantLookup();

        var basicPlan = planRepo.Plans.First(p => p.Tier == SaasPlanTier.Basic);
        var sub = TenantSaasSubscription.CreateActive(_tenantId, basicPlan, "cus_stripe_1", "sub_stripe_1").Value!;
        subRepo.Subscriptions[_tenantId] = sub;

        var quota = TenantQuotaUsage.CreateForCycle(_tenantId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30)).Value!;
        quota.RecordWorkOrderCreated(10);
        quotaRepo.Quotas[_tenantId] = quota;

        var service = new SaasBillingWebhookApplicationService(
            gateway, processedRepo, subRepo, quotaRepo, invoiceRepo, planRepo, tenantLookup);

        var payload = new SaasBillingWebhookPayload(
            EventId: "evt_upgrade_checkout",
            EventType: "checkout.session.completed",
            TenantId: _tenantId,
            GatewaySubscriptionId: "sub_stripe_new",
            GatewayInvoiceId: null,
            Amount: 399.00m,
            TimestampUtc: DateTimeOffset.UtcNow,
            GatewayCustomerId: "cus_stripe_1",
            PlanTier: SaasPlanTier.Enterprise);

        var result = await service.ProcessWebhookAsync(payload);

        Assert.True(result.IsSuccess);
        Assert.Equal(SaasPlanTier.Enterprise, sub.PlanTier);
        Assert.Equal(TenantSubscriptionStatus.Active, sub.Status);
        Assert.Equal("sub_stripe_new", sub.GatewaySubscriptionId);
        Assert.Equal(0, quota.WorkOrdersCreatedCount); // Novo ciclo resetado
        Assert.Contains("evt_upgrade_checkout", processedRepo.ProcessedEvents);
    }

    [Fact]
    public async Task ProcessWebhook_Should_Update_Plan_On_CustomerSubscriptionUpdated()
    {
        var gateway = new FakeGatewayProvider();
        var processedRepo = new FakeProcessedEventsRepository();
        var subRepo = new FakeSubscriptionRepository();
        var quotaRepo = new FakeQuotaUsageRepository();
        var invoiceRepo = new FakeInvoiceRepository();
        var planRepo = new FakePlanRepository();
        var tenantLookup = new FakeGlobalTenantLookup();

        var basicPlan = planRepo.Plans.First(p => p.Tier == SaasPlanTier.Basic);
        var sub = TenantSaasSubscription.CreateActive(_tenantId, basicPlan, "cus_stripe_portal", "sub_stripe_portal").Value!;
        subRepo.Subscriptions[_tenantId] = sub;

        var service = new SaasBillingWebhookApplicationService(
            gateway, processedRepo, subRepo, quotaRepo, invoiceRepo, planRepo, tenantLookup);

        var payload = new SaasBillingWebhookPayload(
            EventId: "evt_sub_updated",
            EventType: "customer.subscription.updated",
            TenantId: _tenantId,
            GatewaySubscriptionId: "sub_stripe_portal",
            GatewayInvoiceId: null,
            Amount: null,
            TimestampUtc: DateTimeOffset.UtcNow,
            GatewayCustomerId: "cus_stripe_portal",
            PlanTier: SaasPlanTier.Pro);

        var result = await service.ProcessWebhookAsync(payload);

        Assert.True(result.IsSuccess);
        Assert.Equal(SaasPlanTier.Pro, sub.PlanTier);
        Assert.Contains("evt_sub_updated", processedRepo.ProcessedEvents);
    }

    [Fact]
    public async Task ProcessWebhook_Should_Update_Plan_On_InvoicePaid_When_TargetPlan_Specified()
    {
        var gateway = new FakeGatewayProvider();
        var processedRepo = new FakeProcessedEventsRepository();
        var subRepo = new FakeSubscriptionRepository();
        var quotaRepo = new FakeQuotaUsageRepository();
        var invoiceRepo = new FakeInvoiceRepository();
        var planRepo = new FakePlanRepository();
        var tenantLookup = new FakeGlobalTenantLookup();

        var basicPlan = planRepo.Plans.First(p => p.Tier == SaasPlanTier.Basic);
        var sub = TenantSaasSubscription.CreateActive(_tenantId, basicPlan, "cus_stripe_inv", "sub_stripe_inv").Value!;
        subRepo.Subscriptions[_tenantId] = sub;

        var quota = TenantQuotaUsage.CreateForCycle(_tenantId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30)).Value!;
        quotaRepo.Quotas[_tenantId] = quota;

        var invoice = SaasInvoice.CreatePending(_tenantId, "inv_stripe_upgrade", 399.00m, DateTimeOffset.UtcNow).Value!;
        invoiceRepo.Invoices.Add(invoice);

        var service = new SaasBillingWebhookApplicationService(
            gateway, processedRepo, subRepo, quotaRepo, invoiceRepo, planRepo, tenantLookup);

        var payload = new SaasBillingWebhookPayload(
            EventId: "evt_inv_paid_upgrade",
            EventType: "invoice.paid",
            TenantId: _tenantId,
            GatewaySubscriptionId: "sub_stripe_inv",
            GatewayInvoiceId: "inv_stripe_upgrade",
            Amount: 399.00m,
            TimestampUtc: DateTimeOffset.UtcNow,
            GatewayCustomerId: "cus_stripe_inv",
            PlanTier: SaasPlanTier.Enterprise);

        var result = await service.ProcessWebhookAsync(payload);

        Assert.True(result.IsSuccess);
        Assert.Equal(SaasPlanTier.Enterprise, sub.PlanTier);
        Assert.Equal("Paid", invoice.Status);
        Assert.Equal(TenantSubscriptionStatus.Active, sub.Status);
    }
}

