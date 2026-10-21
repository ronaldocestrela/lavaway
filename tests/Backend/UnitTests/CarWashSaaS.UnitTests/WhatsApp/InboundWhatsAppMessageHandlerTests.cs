using System.Text.Json;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarWashSaaS.UnitTests.WhatsApp;

public sealed class InboundWhatsAppMessageHandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly string _senderPhone = "5511999998888";

    [Fact]
    public async Task HandleAsync_WhenAiChatbotNotAllowed_ShouldSendFallbackMessage_AndNotProcessBot()
    {
        var quotaLookup = new ConfigurableFakeQuotaLookup(allowedFeatures: []);
        var dispatcher = new FakeOutboundDispatcher();
        var storeProfileLookup = new FakeStoreProfileLookup("Lava Aê Express");
        var sessionRepo = new FakeChatbotSessionRepository();

        var engine = new ChatbotConversationEngine(
            sessionRepo,
            new FakeSchedulingLookup(),
            storeProfileLookup,
            dispatcher);

        var handler = new InboundWhatsAppMessageHandler(
            engine,
            NullLogger<InboundWhatsAppMessageHandler>.Instance,
            quotaLookup,
            dispatcher,
            storeProfileLookup);

        var payload = JsonSerializer.Serialize(new InboundWhatsAppReceivedEvent(
            _tenantId,
            _senderPhone,
            "Carlos",
            "Olá gostaria de lavar o carro",
            "MSG-1",
            DateTimeOffset.UtcNow));

        var message = new TenantQueueMessage(_tenantId, InboundWhatsAppMessageHandler.InboundWhatsAppDispatchEventType, payload);

        await handler.HandleAsync(message, CancellationToken.None);

        // Fallback message was sent
        Assert.Single(dispatcher.SentMessages);
        Assert.Contains("Lava Aê Express", dispatcher.SentMessages[0].Text);
        Assert.Contains("atendimento digital automatizado não está ativo", dispatcher.SentMessages[0].Text);

        // No chatbot session started
        var session = await sessionRepo.GetActiveByPhoneAsync(_tenantId, _senderPhone);
        Assert.Null(session);
    }

    [Fact]
    public async Task HandleAsync_WhenAiChatbotAllowed_ShouldProcessChatbotMessage()
    {
        var quotaLookup = new ConfigurableFakeQuotaLookup(allowedFeatures: [SaasFeature.AiChatbot]);
        var dispatcher = new FakeOutboundDispatcher();
        var storeProfileLookup = new FakeStoreProfileLookup("Lava Aê Express");
        var sessionRepo = new FakeChatbotSessionRepository();

        var engine = new ChatbotConversationEngine(
            sessionRepo,
            new FakeSchedulingLookup(),
            storeProfileLookup,
            dispatcher);

        var handler = new InboundWhatsAppMessageHandler(
            engine,
            NullLogger<InboundWhatsAppMessageHandler>.Instance,
            quotaLookup,
            dispatcher,
            storeProfileLookup);

        var payload = JsonSerializer.Serialize(new InboundWhatsAppReceivedEvent(
            _tenantId,
            _senderPhone,
            "Carlos",
            "Olá",
            "MSG-2",
            DateTimeOffset.UtcNow));

        var message = new TenantQueueMessage(_tenantId, InboundWhatsAppMessageHandler.InboundWhatsAppDispatchEventType, payload);

        await handler.HandleAsync(message, CancellationToken.None);

        // Chatbot menu was sent
        Assert.Single(dispatcher.SentMessages);
        Assert.Contains("Como podemos te ajudar hoje?", dispatcher.SentMessages[0].Text);

        // Session was created
        var session = await sessionRepo.GetActiveByPhoneAsync(_tenantId, _senderPhone);
        Assert.NotNull(session);
    }

    private sealed class ConfigurableFakeQuotaLookup(HashSet<SaasFeature> allowedFeatures) : ITenantPlanQuotaLookup
    {
        public Task<Result<TenantQuotaStatusDto>> CheckWorkOrderQuotaAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<TenantQuotaStatusDto>.Success(new(true, SaasPlanTier.Basic, TenantSubscriptionStatus.Active, 0, 100, 0, 100)));

        public Task<Result> ConsumeWorkOrderQuotaAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result.Success());

        public Task<Result<TenantQuotaStatusDto>> CheckWhatsAppQuotaAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<TenantQuotaStatusDto>.Success(new(true, SaasPlanTier.Basic, TenantSubscriptionStatus.Active, 0, 100, 0, 100)));

        public Task<Result> ConsumeWhatsAppQuotaAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result.Success());

        public Task<Result<TenantSubscriptionOverviewDto>> GetSubscriptionOverviewAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<TenantSubscriptionOverviewDto>.Failure(new("not_found", "Not found", ErrorType.NotFound)));

        public Task<Result<bool>> HasFeatureAsync(Guid tenantId, SaasFeature feature, CancellationToken ct = default) =>
            Task.FromResult(Result<bool>.Success(allowedFeatures.Contains(feature)));

        public Task<Result> ValidateTeamMemberLimitAsync(Guid tenantId, int currentTeamMemberCount, CancellationToken ct = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> ValidateBoxesLimitAsync(Guid tenantId, int requestedBoxes, CancellationToken ct = default) =>
            Task.FromResult(Result.Success());
    }

    private sealed class FakeOutboundDispatcher : IOutboundWhatsAppDispatcher
    {
        public readonly List<(string Phone, string Text)> SentMessages = [];

        public Task<Result<WhatsAppMessageDto>> DispatchTextMessageAsync(
            Guid tenantId,
            string recipientPhone,
            string messageText,
            string? idempotencyKey = null,
            CancellationToken ct = default)
        {
            SentMessages.Add((recipientPhone, messageText));
            return Task.FromResult(Result<WhatsAppMessageDto>.Success(new WhatsAppMessageDto(
                Guid.NewGuid(), recipientPhone, messageText, "Sent", null, 1, DateTimeOffset.UtcNow, null, null)));
        }

        public Task<Result<WhatsAppMessageDto>> DispatchMediaMessageAsync(
            Guid tenantId,
            string recipientPhone,
            string caption,
            string mediaType,
            string mediaUrlOrBase64,
            string mediaMimeType,
            string mediaFileName,
            string? idempotencyKey = null,
            CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Result<IReadOnlyList<WhatsAppMessageDto>>> GetRecentMessagesAsync(Guid tenantId, int count = 20, CancellationToken ct = default) =>
            Task.FromResult(Result<IReadOnlyList<WhatsAppMessageDto>>.Success([]));
    }

    private sealed class FakeStoreProfileLookup(string tradeName) : ITenantStoreProfileLookup
    {
        public Task<Result<StoreProfileDto>> GetProfileAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<StoreProfileDto>.Success(new StoreProfileDto(
                Guid.NewGuid(),
                tenantId,
                "Lava Aê LTDA",
                tradeName,
                "12345678000199",
                "11999998888",
                "Rua Teste, 1",
                "São Paulo",
                "SP",
                "01000-000",
                null,
                null,
                null)));
    }

    private sealed class FakeChatbotSessionRepository : IChatbotSessionRepository
    {
        private readonly List<ChatbotConversationSession> _sessions = [];

        public Task<ChatbotConversationSession?> GetActiveByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default)
        {
            var session = _sessions.FirstOrDefault(s => s.TenantId == tenantId && s.CustomerPhone == customerPhone && s.IsActive);
            return Task.FromResult(session);
        }

        public Task<ChatbotConversationSession?> GetByIdAsync(Guid tenantId, Guid sessionId, CancellationToken ct = default)
        {
            var session = _sessions.FirstOrDefault(s => s.TenantId == tenantId && s.Id == sessionId);
            return Task.FromResult(session);
        }

        public Task AddAsync(ChatbotConversationSession session, CancellationToken ct = default)
        {
            _sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ChatbotConversationSession session, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSchedulingLookup : ISchedulingBookingLookup
    {
        public Task<Result<IReadOnlyList<ServiceDto>>> GetAvailableServicesCatalogAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<Result<IReadOnlyList<ServiceDto>>>(Result<IReadOnlyList<ServiceDto>>.Success([]));

        public Task<Result<IReadOnlyList<AvailableTimeSlotDto>>> GetAvailableTimeSlotsAsync(Guid tenantId, DateOnly date, Guid serviceId, string vehicleSize, CancellationToken ct = default) =>
            Task.FromResult<Result<IReadOnlyList<AvailableTimeSlotDto>>>(Result<IReadOnlyList<AvailableTimeSlotDto>>.Success([]));

        public Task<Result<BookingConfirmationDto>> CreateBookingFromChatbotAsync(Guid tenantId, CreateBookingFromChatbotRequest request, CancellationToken ct = default) =>
            Task.FromResult(Result<BookingConfirmationDto>.Success(new BookingConfirmationDto(Guid.NewGuid(), "BK-123", "Cliente", "Serviço", DateOnly.FromDateTime(DateTime.Today), new TimeOnly(10, 0), 50m)));

        public Task<Result<IReadOnlyList<BookingSummaryDto>>> GetCustomerActiveBookingsAsync(Guid tenantId, string customerPhone, CancellationToken ct = default) =>
            Task.FromResult<Result<IReadOnlyList<BookingSummaryDto>>>(Result<IReadOnlyList<BookingSummaryDto>>.Success([]));

        public Task<Result<BookingSummaryDto>> ConfirmBookingAsync(Guid tenantId, Guid bookingId, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Result<BookingSummaryDto>> CancelBookingAsync(Guid tenantId, Guid bookingId, string? reason, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Result<BookingSummaryDto>> RescheduleBookingAsync(Guid tenantId, Guid bookingId, DateOnly newDate, TimeOnly newTime, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Result<BookingSummaryDto?>> GetUpcomingBookingForCustomerAsync(Guid tenantId, string customerPhone, CancellationToken ct = default) =>
            Task.FromResult<Result<BookingSummaryDto?>>(Result<BookingSummaryDto?>.Success(null));
    }
}
