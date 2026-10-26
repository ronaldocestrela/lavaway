using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarWashSaaS.UnitTests.WhatsApp;

public sealed class WhatsAppTransientDataPurgeApplicationServiceTests
{
    [Fact]
    public async Task PurgeTransientDataAsync_Should_Purge_Media_And_Inactive_Sessions()
    {
        // Arrange
        var fakeMessageRepo = new FakeOutboundMessageRepository();
        var fakeSessionRepo = new FakeChatbotSessionRepository();

        var tenantId = Guid.NewGuid();
        var message = OutboundWhatsAppMessage.CreateWithMedia(
            tenantId,
            "11999998888",
            "Fatura da Lavagem",
            "document",
            "base64_data_payload_sensivel",
            "application/pdf",
            "fatura.pdf").Value!;

        message.MarkSent("msg_123");
        fakeMessageRepo.Items.Add(message);

        fakeSessionRepo.PurgedCount = 3;

        var service = new WhatsAppTransientDataPurgeApplicationService(
            fakeMessageRepo,
            fakeSessionRepo,
            NullLogger<WhatsAppTransientDataPurgeApplicationService>.Instance);

        // Act
        var result = await service.PurgeTransientDataAsync(retentionDays: 60);

        // Assert
        Assert.True(result.IsSuccess);
        var (mediaPurged, sessionsPurged) = result.Value;
        Assert.Equal(1, mediaPurged);
        Assert.Equal(3, sessionsPurged);

        Assert.Null(message.MediaUrlOrBase64);
        Assert.True(fakeMessageRepo.Saved);
    }

    [Fact]
    public async Task PurgeTransientDataAsync_Should_Fail_When_RetentionDays_Is_Zero_Or_Negative()
    {
        // Arrange
        var service = new WhatsAppTransientDataPurgeApplicationService(
            new FakeOutboundMessageRepository(),
            new FakeChatbotSessionRepository());

        // Act
        var result = await service.PurgeTransientDataAsync(retentionDays: 0);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("whatsapp.retention_days.invalid", result.Error!.Code);
    }

    private sealed class FakeOutboundMessageRepository : IOutboundWhatsAppMessageRepository
    {
        public List<OutboundWhatsAppMessage> Items { get; } = [];
        public bool Saved { get; private set; }

        public Task<OutboundWhatsAppMessage?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(m => m.Id == id));

        public Task<OutboundWhatsAppMessage?> GetByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(m => m.TenantId == tenantId && m.IdempotencyKey == idempotencyKey));

        public Task<OutboundWhatsAppMessage?> GetByProviderMessageIdAsync(Guid tenantId, string providerMessageId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(m => m.TenantId == tenantId && m.ProviderMessageId == providerMessageId));

        public Task<IReadOnlyList<OutboundWhatsAppMessage>> GetRecentAsync(Guid tenantId, int count = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OutboundWhatsAppMessage>>(Items.Take(count).ToList());

        public Task<IReadOnlyList<OutboundWhatsAppMessage>> GetEligibleForMediaPurgeAsync(DateTimeOffset cutoffUtc, int batchSize = 100, CancellationToken ct = default)
        {
            var eligible = Items
                .Where(m => m.MediaUrlOrBase64 != null)
                .Take(batchSize)
                .ToList();
            return Task.FromResult<IReadOnlyList<OutboundWhatsAppMessage>>(eligible);
        }

        public Task AddAsync(OutboundWhatsAppMessage message, CancellationToken ct = default)
        {
            Items.Add(message);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeChatbotSessionRepository : IChatbotSessionRepository
    {
        public int PurgedCount { get; set; }

        public Task<ChatbotConversationSession?> GetActiveByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default) =>
            Task.FromResult<ChatbotConversationSession?>(null);

        public Task AddAsync(ChatbotConversationSession session, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(ChatbotConversationSession session, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<int> PurgeInactiveSessionsOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken ct = default) =>
            Task.FromResult(PurgedCount);
    }
}
