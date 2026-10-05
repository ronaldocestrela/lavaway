using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace CarWashSaaS.UnitTests.WhatsApp;

public sealed class WhatsAppMessageApplicationServiceTests
{
    [Fact]
    public async Task SendTestMessageAsync_Should_Succeed_When_Connected_And_Quota_Available()
    {
        var tenantId = Guid.NewGuid();
        var connection = WhatsAppConnection.Create(tenantId, "session-1", "qr-1").Value!;
        connection.MarkConnected();

        var connectionRepo = new FakeWhatsAppConnectionRepository(connection);
        var messageRepo = new FakeOutboundWhatsAppMessageRepository();
        var quotaRepo = new FakeTenantWhatsAppQuotaRepository(TenantWhatsAppQuota.CreateDefault(tenantId).Value!);
        var preferenceRepo = new FakeCustomerCommunicationPreferenceRepository();
        var queue = new FakeBackgroundQueue();

        var service = new WhatsAppMessageApplicationService(connectionRepo, messageRepo, quotaRepo, preferenceRepo, queue);

        var result = await service.SendTestMessageAsync(tenantId, "(11) 98765-4321", "Mensagem de teste unitário");

        Assert.True(result.IsSuccess);
        Assert.Equal("11987654321", result.Value!.RecipientPhone);
        Assert.Equal("queued", result.Value.Status);
        Assert.Single(messageRepo.Messages);
        Assert.Single(queue.Messages);
        Assert.Equal("whatsapp.message.dispatch", queue.Messages[0].EventType);
    }

    [Fact]
    public async Task SendTestMessageAsync_Should_Fail_When_WhatsApp_Not_Connected()
    {
        var tenantId = Guid.NewGuid();
        var connection = WhatsAppConnection.Create(tenantId, "session-1", "qr-1").Value!; // Status = Connecting

        var connectionRepo = new FakeWhatsAppConnectionRepository(connection);
        var messageRepo = new FakeOutboundWhatsAppMessageRepository();
        var quotaRepo = new FakeTenantWhatsAppQuotaRepository(TenantWhatsAppQuota.CreateDefault(tenantId).Value!);
        var preferenceRepo = new FakeCustomerCommunicationPreferenceRepository();
        var queue = new FakeBackgroundQueue();

        var service = new WhatsAppMessageApplicationService(connectionRepo, messageRepo, quotaRepo, preferenceRepo, queue);

        var result = await service.SendTestMessageAsync(tenantId, "11987654321", "Teste");

        Assert.False(result.IsSuccess);
        Assert.Equal("whatsapp.not_connected", result.Error!.Code);
        Assert.Empty(messageRepo.Messages);
        Assert.Empty(queue.Messages);
    }

    [Fact]
    public async Task SendTestMessageAsync_Should_Fail_When_Recipient_Opted_Out()
    {
        var tenantId = Guid.NewGuid();
        var connection = WhatsAppConnection.Create(tenantId, "session-1", "qr-1").Value!;
        connection.MarkConnected();

        var pref = CustomerCommunicationPreference.Create(tenantId, "11987654321").Value!;
        pref.OptOut("Cliente não quer receber mensagens");

        var connectionRepo = new FakeWhatsAppConnectionRepository(connection);
        var messageRepo = new FakeOutboundWhatsAppMessageRepository();
        var quotaRepo = new FakeTenantWhatsAppQuotaRepository(TenantWhatsAppQuota.CreateDefault(tenantId).Value!);
        var preferenceRepo = new FakeCustomerCommunicationPreferenceRepository(pref);
        var queue = new FakeBackgroundQueue();

        var service = new WhatsAppMessageApplicationService(connectionRepo, messageRepo, quotaRepo, preferenceRepo, queue);

        var result = await service.SendTestMessageAsync(tenantId, "11987654321", "Teste");

        Assert.False(result.IsSuccess);
        Assert.Equal("whatsapp.recipient.opted_out", result.Error!.Code);
        Assert.Empty(messageRepo.Messages);
    }

    [Fact]
    public async Task QueueHandler_Should_Dispatch_Message_And_Mark_Sent()
    {
        var tenantId = Guid.NewGuid();
        var message = OutboundWhatsAppMessage.Create(tenantId, "11987654321", "Teste assíncrono").Value!;

        var messageRepo = new FakeOutboundWhatsAppMessageRepository();
        await messageRepo.AddAsync(message);

        var sender = new FakeWhatsAppMessageSender(successResult: "provider-msg-777");
        var handler = new OutboundWhatsAppMessageQueueHandler(messageRepo, sender, NullLogger<OutboundWhatsAppMessageQueueHandler>.Instance);

        var queueMessage = new TenantQueueMessage(tenantId, "whatsapp.message.dispatch", message.Id.ToString("D"), message.Id);
        await handler.HandleAsync(queueMessage, CancellationToken.None);

        Assert.Equal(WhatsAppMessageStatus.Sent, message.Status);
        Assert.Equal("provider-msg-777", message.ProviderMessageId);
        Assert.NotNull(message.SentAtUtc);
    }

    [Fact]
    public async Task ProcessDeliveryWebhookAsync_Should_Update_Message_Status()
    {
        var tenantId = Guid.NewGuid();
        var message = OutboundWhatsAppMessage.Create(tenantId, "11987654321", "Teste").Value!;
        message.MarkSent("provider-xyz");

        var messageRepo = new FakeOutboundWhatsAppMessageRepository();
        await messageRepo.AddAsync(message);

        var service = new WhatsAppMessageApplicationService(
            new FakeWhatsAppConnectionRepository(null),
            messageRepo,
            new FakeTenantWhatsAppQuotaRepository(TenantWhatsAppQuota.CreateDefault(tenantId).Value!),
            new FakeCustomerCommunicationPreferenceRepository(),
            new FakeBackgroundQueue());

        var deliveredResult = await service.ProcessDeliveryWebhookAsync(tenantId, "provider-xyz", "delivered");
        Assert.True(deliveredResult.Value);
        Assert.Equal(WhatsAppMessageStatus.Delivered, message.Status);

        var readResult = await service.ProcessDeliveryWebhookAsync(tenantId, "provider-xyz", "read");
        Assert.True(readResult.Value);
        Assert.Equal(WhatsAppMessageStatus.Read, message.Status);
    }

    private sealed class FakeWhatsAppConnectionRepository(WhatsAppConnection? connection) : IWhatsAppConnectionRepository
    {
        public Task<WhatsAppConnection?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(connection);

        public Task AddAsync(WhatsAppConnection connection, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(WhatsAppConnection connection, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeOutboundWhatsAppMessageRepository : IOutboundWhatsAppMessageRepository
    {
        public List<OutboundWhatsAppMessage> Messages { get; } = [];

        public Task<OutboundWhatsAppMessage?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Messages.FirstOrDefault(m => m.Id == id));

        public Task<OutboundWhatsAppMessage?> GetByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken ct = default)
            => Task.FromResult(Messages.FirstOrDefault(m => m.TenantId == tenantId && m.IdempotencyKey == idempotencyKey));

        public Task<OutboundWhatsAppMessage?> GetByProviderMessageIdAsync(Guid tenantId, string providerMessageId, CancellationToken ct = default)
            => Task.FromResult(Messages.FirstOrDefault(m => m.TenantId == tenantId && m.ProviderMessageId == providerMessageId));

        public Task<IReadOnlyList<OutboundWhatsAppMessage>> GetRecentAsync(Guid tenantId, int count = 20, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<OutboundWhatsAppMessage>>(Messages.Where(m => m.TenantId == tenantId).Take(count).ToList());

        public Task AddAsync(OutboundWhatsAppMessage message, CancellationToken ct = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeTenantWhatsAppQuotaRepository(TenantWhatsAppQuota quota) : ITenantWhatsAppQuotaRepository
    {
        public Task<TenantWhatsAppQuota> GetOrCreateAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(quota);

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeCustomerCommunicationPreferenceRepository(CustomerCommunicationPreference? pref = null) : ICustomerCommunicationPreferenceRepository
    {
        public Task<CustomerCommunicationPreference?> GetByPhoneAsync(Guid tenantId, string normalizedPhone, CancellationToken ct = default)
            => Task.FromResult(pref);

        public Task<IReadOnlyList<CustomerCommunicationPreference>> ListPreferencesAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CustomerCommunicationPreference>>(pref is not null ? [pref] : []);

        public Task AddAsync(CustomerCommunicationPreference preference, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeBackgroundQueue : IBackgroundQueue
    {
        public List<TenantQueueMessage> Messages { get; } = [];

        public ValueTask EnqueueAsync(TenantQueueMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return ValueTask.CompletedTask;
        }

        public ValueTask<IBackgroundQueueDelivery?> DequeueAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult<IBackgroundQueueDelivery?>(null);
    }

    [Fact]
    public async Task SendMediaMessageAsync_Should_Succeed_With_Valid_Parameters()
    {
        var tenantId = Guid.NewGuid();
        var connection = WhatsAppConnection.Create(tenantId, "sess-1", "qr-1").Value!;
        connection.MarkConnected();

        var quota = TenantWhatsAppQuota.CreateDefault(tenantId, 10, 100).Value!;
        var prefRepo = new FakeCustomerCommunicationPreferenceRepository();
        var messageRepo = new FakeOutboundWhatsAppMessageRepository();
        var queue = new FakeBackgroundQueue();
        var service = new WhatsAppMessageApplicationService(
            new FakeWhatsAppConnectionRepository(connection),
            messageRepo,
            new FakeTenantWhatsAppQuotaRepository(quota),
            prefRepo,
            queue);

        var result = await service.SendMediaMessageAsync(
            tenantId,
            "11987654321",
            "Segue comprovante",
            "document",
            "data:application/pdf;base64,JVBERi...",
            "application/pdf",
            "comprovante.pdf");

        Assert.True(result.IsSuccess);
        Assert.Equal("11987654321", result.Value!.RecipientPhone);
        Assert.Equal("document", result.Value.MediaType);
        Assert.Equal("comprovante.pdf", result.Value.MediaFileName);
        Assert.Single(messageRepo.Messages);
        Assert.Single(queue.Messages);
    }

    private sealed class FakeWhatsAppMessageSender(string successResult) : IWhatsAppMessageSender
    {
        public Task<Result<string>> SendTextMessageAsync(Guid tenantId, string recipientPhone, string messageText, CancellationToken ct = default)
            => Task.FromResult(Result<string>.Success(successResult));

        public Task<Result<string>> SendMediaMessageAsync(
            Guid tenantId,
            string recipientPhone,
            string mediaBase64OrUrl,
            string mediaType,
            string mimeType,
            string fileName,
            string? caption = null,
            CancellationToken ct = default)
            => Task.FromResult(Result<string>.Success(successResult));
    }
}

