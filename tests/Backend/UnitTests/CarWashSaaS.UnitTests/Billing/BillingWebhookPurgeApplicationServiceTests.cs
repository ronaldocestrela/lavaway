using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class BillingWebhookPurgeApplicationServiceTests
{
    [Fact]
    public async Task PurgeOldWebhookPayloadsAsync_Should_Purge_Eligible_Webhooks_And_Save()
    {
        // Arrange
        var fakePaymentRepo = new FakePaymentWebhookRepository();
        var fakeSaasRepo = new FakeSaasWebhookRepository();

        var tenantId = Guid.NewGuid();
        var expiredPaymentWebhook = ProcessedPaymentWebhook.Create(
            tenantId,
            "PagarMe",
            "evt_old",
            "tx_123",
            "Processed",
            rawPayload: "{\"sensitive\":\"credit_card_data\"}").Value!;

        var expiredSaasWebhook = new ProcessedSaasWebhookEvent(
            Guid.NewGuid(),
            "stripe_evt_old",
            "invoice.paid",
            tenantId,
            DateTimeOffset.UtcNow.AddDays(-70),
            rawPayload: "{\"customer_address\":\"Rua X, 123\"}");

        fakePaymentRepo.Items.Add(expiredPaymentWebhook);
        fakeSaasRepo.Items.Add(expiredSaasWebhook);

        var service = new BillingWebhookPurgeApplicationService(
            fakePaymentRepo,
            fakeSaasRepo,
            NullLogger<BillingWebhookPurgeApplicationService>.Instance);

        // Act
        var result = await service.PurgeOldWebhookPayloadsAsync(retentionDays: 60);

        // Assert
        Assert.True(result.IsSuccess);
        var (paymentPurged, saasPurged) = result.Value;
        Assert.Equal(1, paymentPurged);
        Assert.Equal(1, saasPurged);

        Assert.True(expiredPaymentWebhook.IsPurged);
        Assert.Null(expiredPaymentWebhook.RawPayload);

        Assert.True(expiredSaasWebhook.IsPurged);
        Assert.Null(expiredSaasWebhook.RawPayload);

        Assert.True(fakePaymentRepo.Saved);
        Assert.True(fakeSaasRepo.Saved);
    }

    [Fact]
    public async Task PurgeOldWebhookPayloadsAsync_Should_Fail_When_RetentionDays_Is_Zero_Or_Negative()
    {
        // Arrange
        var service = new BillingWebhookPurgeApplicationService(
            new FakePaymentWebhookRepository(),
            new FakeSaasWebhookRepository());

        // Act
        var result = await service.PurgeOldWebhookPayloadsAsync(retentionDays: 0);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("billing.retention_days.invalid", result.Error!.Code);
    }

    private sealed class FakePaymentWebhookRepository : IProcessedPaymentWebhookRepository
    {
        public List<ProcessedPaymentWebhook> Items { get; } = [];
        public bool Saved { get; private set; }

        public Task<ProcessedPaymentWebhook?> GetByEventIdAsync(Guid tenantId, string provider, string eventId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(w => w.TenantId == tenantId && w.Provider == provider && w.EventId == eventId));

        public Task<bool> HasBeenProcessedAsync(Guid tenantId, string provider, string eventId, CancellationToken ct = default) =>
            Task.FromResult(Items.Any(w => w.TenantId == tenantId && w.Provider == provider && w.EventId == eventId));

        public Task<IReadOnlyList<ProcessedPaymentWebhook>> GetEligibleForPayloadPurgeAsync(DateTimeOffset cutoffUtc, int batchSize = 100, CancellationToken ct = default)
        {
            var eligible = Items
                .Where(w => !w.IsPurged && w.RawPayload != null)
                .Take(batchSize)
                .ToList();
            return Task.FromResult<IReadOnlyList<ProcessedPaymentWebhook>>(eligible);
        }

        public Task AddAsync(ProcessedPaymentWebhook webhook, CancellationToken ct = default)
        {
            Items.Add(webhook);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSaasWebhookRepository : IProcessedSaasWebhookEventRepository
    {
        public List<ProcessedSaasWebhookEvent> Items { get; } = [];
        public bool Saved { get; private set; }

        public Task<bool> HasBeenProcessedAsync(string eventId, CancellationToken ct = default) =>
            Task.FromResult(Items.Any(w => w.EventId == eventId));

        public Task<IReadOnlyList<ProcessedSaasWebhookEvent>> GetEligibleForPayloadPurgeAsync(DateTimeOffset cutoffUtc, int batchSize = 100, CancellationToken ct = default)
        {
            var eligible = Items
                .Where(w => !w.IsPurged && w.RawPayload != null)
                .Take(batchSize)
                .ToList();
            return Task.FromResult<IReadOnlyList<ProcessedSaasWebhookEvent>>(eligible);
        }

        public Task AddAsync(ProcessedSaasWebhookEvent webhookEvent, CancellationToken ct = default)
        {
            Items.Add(webhookEvent);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }
}
