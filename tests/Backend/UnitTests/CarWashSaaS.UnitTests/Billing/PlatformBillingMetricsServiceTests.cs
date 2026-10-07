using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class PlatformBillingMetricsServiceTests
{
    private sealed class FakeSubscriptionRepository : ITenantSaasSubscriptionRepository
    {
        public readonly List<TenantSaasSubscription> Subscriptions = [];

        public Task<TenantSaasSubscription?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Subscriptions.FirstOrDefault(s => s.TenantId == tenantId));

        public Task<TenantSaasSubscription?> GetByGatewaySubscriptionIdAsync(string gatewaySubscriptionId, CancellationToken ct = default) =>
            Task.FromResult(Subscriptions.FirstOrDefault(s => s.GatewaySubscriptionId == gatewaySubscriptionId));

        public Task<IReadOnlyList<TenantSaasSubscription>> ListAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TenantSaasSubscription>>(Subscriptions);

        public Task AddAsync(TenantSaasSubscription subscription, CancellationToken ct = default)
        {
            Subscriptions.Add(subscription);
            return Task.CompletedTask;
        }

        public void Update(TenantSaasSubscription subscription) { }

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
            Task.FromResult(Invoices.FirstOrDefault(i => i.TenantId == tenantId && i.Status == "Pending"));

        public Task<IReadOnlyList<SaasInvoice>> ListPaidInPeriodAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SaasInvoice>>(Invoices.Where(i => i.PaidAtUtc >= fromUtc && i.PaidAtUtc <= toUtc && i.Status == "Paid").ToList());

        public Task AddAsync(SaasInvoice invoice, CancellationToken ct = default)
        {
            Invoices.Add(invoice);
            return Task.CompletedTask;
        }

        public void Update(SaasInvoice invoice) { }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakePixChargeRepository : IPixChargeRepository
    {
        public readonly List<PixCharge> Charges = [];

        public Task<PixCharge?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Charges.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id));

        public Task<PixCharge?> GetActiveByWorkOrderIdAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default) =>
            Task.FromResult(Charges.FirstOrDefault(c => c.TenantId == tenantId && c.WorkOrderId == workOrderId && c.Status == PixChargeStatusConstants.Pending));

        public Task<PixCharge?> GetLatestByWorkOrderIdAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default) =>
            Task.FromResult(Charges.Where(c => c.TenantId == tenantId && c.WorkOrderId == workOrderId).OrderByDescending(c => c.CreatedAtUtc).FirstOrDefault());

        public Task<PixCharge?> GetByTxIdAsync(Guid tenantId, string txId, CancellationToken ct = default) =>
            Task.FromResult(Charges.FirstOrDefault(c => c.TenantId == tenantId && c.TxId == txId));

        public Task<IReadOnlyList<PixCharge>> ListPaidInPeriodAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, Guid? tenantId = null, CancellationToken ct = default)
        {
            var query = Charges.Where(c => c.Status == PixChargeStatusConstants.Paid && c.PaidAtUtc >= fromUtc && c.PaidAtUtc <= toUtc);
            if (tenantId.HasValue)
            {
                query = query.Where(c => c.TenantId == tenantId.Value);
            }
            return Task.FromResult<IReadOnlyList<PixCharge>>(query.ToList());
        }

        public Task AddAsync(PixCharge charge, CancellationToken ct = default)
        {
            Charges.Add(charge);
            return Task.CompletedTask;
        }

        public void Update(PixCharge charge) { }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeProcessedPaymentWebhookRepository : IProcessedPaymentWebhookRepository
    {
        public readonly List<ProcessedPaymentWebhook> Webhooks = [];

        public Task<ProcessedPaymentWebhook?> GetByEventIdAsync(Guid tenantId, string provider, string eventId, CancellationToken ct = default) =>
            Task.FromResult(Webhooks.FirstOrDefault(w => w.TenantId == tenantId && w.Provider == provider && w.EventId == eventId));

        public Task<bool> HasBeenProcessedAsync(Guid tenantId, string provider, string eventId, CancellationToken ct = default) =>
            Task.FromResult(Webhooks.Any(w => w.TenantId == tenantId && w.Provider == provider && w.EventId == eventId));

        public Task<IReadOnlyList<ProcessedPaymentWebhook>> ListInPeriodAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProcessedPaymentWebhook>>(Webhooks.Where(w => w.ReceivedAtUtc >= fromUtc && w.ReceivedAtUtc <= toUtc).ToList());

        public Task AddAsync(ProcessedPaymentWebhook webhook, CancellationToken ct = default)
        {
            Webhooks.Add(webhook);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeProcessedSaasWebhookEventRepository : IProcessedSaasWebhookEventRepository
    {
        public readonly List<ProcessedSaasWebhookEvent> Events = [];

        public Task<bool> HasBeenProcessedAsync(string eventId, CancellationToken ct = default) =>
            Task.FromResult(Events.Any(e => e.EventId == eventId));

        public Task<IReadOnlyList<ProcessedSaasWebhookEvent>> ListInPeriodAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProcessedSaasWebhookEvent>>(Events.Where(e => e.ReceivedAtUtc >= fromUtc && e.ReceivedAtUtc <= toUtc).ToList());

        public Task AddAsync(ProcessedSaasWebhookEvent webhookEvent, CancellationToken ct = default)
        {
            Events.Add(webhookEvent);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeGlobalTenantLookup : IGlobalTenantLookup
    {
        public readonly Dictionary<Guid, string> TenantNames = [];

        public Task<Result<bool>> ExistsAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<bool>.Success(TenantNames.ContainsKey(tenantId)));

        public Task<Result<GlobalTenantSummaryDto>> GetSummaryAsync(Guid tenantId, CancellationToken ct = default)
        {
            if (TenantNames.TryGetValue(tenantId, out var name))
            {
                return Task.FromResult(Result<GlobalTenantSummaryDto>.Success(new GlobalTenantSummaryDto(
                    tenantId, name, TenantStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, name, name, null, null, null, null)));
            }

            return Task.FromResult(Result<GlobalTenantSummaryDto>.Failure(new Error("tenant.not_found", "Not found", ErrorType.NotFound)));
        }

        public Task<Result<GlobalTenantSummaryDto>> UpdateTenantStatusAsync(Guid tenantId, UpdateTenantStatusRequest request, CancellationToken ct = default) =>
            Task.FromResult(Result<GlobalTenantSummaryDto>.Success(new GlobalTenantSummaryDto(
                tenantId, "Tenant", request.NewStatus, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, request.TrialEndsAtUtc, request.Reason, null, null, null, null, null, null)));
    }

    [Fact]
    public async Task Should_Calculate_Mrr_Arr_And_Plan_Distribution_Accurately()
    {
        var subRepo = new FakeSubscriptionRepository();
        var invRepo = new FakeInvoiceRepository();
        var pixRepo = new FakePixChargeRepository();
        var payHookRepo = new FakeProcessedPaymentWebhookRepository();
        var saasHookRepo = new FakeProcessedSaasWebhookEventRepository();
        var tenantLookup = new FakeGlobalTenantLookup();

        var planBasic = SaasPlan.Create(SaasPlanTier.Basic, "Básico", "Plano Básico", 129.90m, 150, 300, false, false, false, false, 2).Value!;
        var planPro = SaasPlan.Create(SaasPlanTier.Pro, "Pro", "Plano Pro", 249.90m, 600, 1500, true, true, true, false, 5).Value!;

        var sub1 = TenantSaasSubscription.CreateTrial(Guid.NewGuid()).Value!;
        sub1.Activate(planBasic);
        subRepo.Subscriptions.Add(sub1);

        var sub2 = TenantSaasSubscription.CreateTrial(Guid.NewGuid()).Value!;
        sub2.Activate(planPro);
        subRepo.Subscriptions.Add(sub2);

        var sub3 = TenantSaasSubscription.CreateTrial(Guid.NewGuid()).Value!;
        sub3.Activate(planPro);
        sub3.MarkOverdue(DateTimeOffset.UtcNow, 5);
        subRepo.Subscriptions.Add(sub3);

        var subTrial = TenantSaasSubscription.CreateTrial(Guid.NewGuid()).Value!;
        subRepo.Subscriptions.Add(subTrial);

        var service = new PlatformBillingMetricsService(subRepo, invRepo, pixRepo, payHookRepo, saasHookRepo, tenantLookup);

        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow.AddDays(1);

        var metrics = await service.GetRevenueMetricsAsync(from, to);

        Assert.Equal(629.70m, metrics.Mrr);
        Assert.Equal(7556.40m, metrics.Arr);
        Assert.Equal(3, metrics.ActiveSubscriptionsCount);
        Assert.Equal(1, metrics.TrialSubscriptionsCount);
        Assert.Equal(2, metrics.PlanDistribution.Count);
    }

    [Fact]
    public async Task Should_Calculate_Churn_Rate_And_Ltv_Safely()
    {
        var subRepo = new FakeSubscriptionRepository();
        var invRepo = new FakeInvoiceRepository();
        var pixRepo = new FakePixChargeRepository();
        var payHookRepo = new FakeProcessedPaymentWebhookRepository();
        var saasHookRepo = new FakeProcessedSaasWebhookEventRepository();
        var tenantLookup = new FakeGlobalTenantLookup();

        var plan = SaasPlan.Create(SaasPlanTier.Basic, "Básico", "Plano Básico", 100m, 150, 300, false, false, false, false, 2).Value!;

        var subActive = TenantSaasSubscription.CreateTrial(Guid.NewGuid()).Value!;
        subActive.Activate(plan);
        subRepo.Subscriptions.Add(subActive);

        var subCanceled = TenantSaasSubscription.CreateTrial(Guid.NewGuid()).Value!;
        subCanceled.Activate(plan);
        subCanceled.Cancel("Cancelado pelo lojista");
        subRepo.Subscriptions.Add(subCanceled);

        var service = new PlatformBillingMetricsService(subRepo, invRepo, pixRepo, payHookRepo, saasHookRepo, tenantLookup);

        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow.AddDays(1);

        var metrics = await service.GetRevenueMetricsAsync(from, to);

        Assert.Equal(1, metrics.CanceledInPeriodCount);
        Assert.Equal(50m, metrics.ChurnRatePercentage);
        Assert.Equal(200m, metrics.Ltv);
    }

    [Fact]
    public async Task Should_Aggregate_Paid_Pix_Transactions_And_Daily_Series()
    {
        var subRepo = new FakeSubscriptionRepository();
        var invRepo = new FakeInvoiceRepository();
        var pixRepo = new FakePixChargeRepository();
        var payHookRepo = new FakeProcessedPaymentWebhookRepository();
        var saasHookRepo = new FakeProcessedSaasWebhookEventRepository();
        var tenantLookup = new FakeGlobalTenantLookup();

        var tenantId = Guid.NewGuid();
        var charge1 = PixCharge.Create(tenantId, Guid.NewGuid(), 100m, "tx1", "qr1", "copy1", DateTimeOffset.UtcNow.AddHours(1)).Value!;
        charge1.MarkAsPaid(DateTimeOffset.UtcNow.AddHours(-2));
        pixRepo.Charges.Add(charge1);

        var charge2 = PixCharge.Create(tenantId, Guid.NewGuid(), 50m, "tx2", "qr2", "copy2", DateTimeOffset.UtcNow.AddHours(1)).Value!;
        charge2.MarkAsPaid(DateTimeOffset.UtcNow.AddHours(-1));
        pixRepo.Charges.Add(charge2);

        var service = new PlatformBillingMetricsService(subRepo, invRepo, pixRepo, payHookRepo, saasHookRepo, tenantLookup);

        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow.AddDays(1);

        var pixMetrics = await service.GetPixMetricsAsync(from, to, tenantId);

        Assert.Equal(150m, pixMetrics.TotalAmountTransacted);
        Assert.Equal(2, pixMetrics.TotalTransactionsCount);
        Assert.Equal(75m, pixMetrics.AverageTicket);
        Assert.NotEmpty(pixMetrics.DailyVolume);
    }
}
