using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.UnitTests.WhatsApp;

public sealed class ChatbotConversationEngineTests
{
    private readonly InMemoryChatbotSessionRepository _sessionRepo = new();
    private readonly FakeSchedulingBookingLookup _schedulingLookup = new();
    private readonly FakeTenantStoreProfileLookup _storeProfileLookup = new();
    private readonly FakeOutboundWhatsAppDispatcher _dispatcher = new();
    private readonly ChatbotConversationEngine _sut;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly string _customerPhone = "11999998888";

    public ChatbotConversationEngineTests()
    {
        _sut = new ChatbotConversationEngine(
            _sessionRepo,
            _schedulingLookup,
            _storeProfileLookup,
            _dispatcher);
    }

    [Fact]
    public async Task ProcessIncomingMessage_FirstMessage_ShouldSendMainMenu()
    {
        var result = await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "Oi");

        Assert.True(result.IsSuccess);
        Assert.Single(_dispatcher.SentMessages);
        Assert.Contains("Como podemos te ajudar hoje?", _dispatcher.SentMessages[0].Text);
        Assert.Contains("1️⃣ - 📅 Agendar um serviço", _dispatcher.SentMessages[0].Text);
    }

    [Fact]
    public async Task ProcessIncomingMessage_Option2_ShouldSendCatalog()
    {
        // 1ª mensagem: saudar
        await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "Oi");
        _dispatcher.SentMessages.Clear();

        // 2ª mensagem: escolher opção 2 (Catálogo)
        var result = await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "2");

        Assert.True(result.IsSuccess);
        Assert.Single(_dispatcher.SentMessages);
        Assert.Contains("Catálogo de Serviços Disponíveis", _dispatcher.SentMessages[0].Text);
        Assert.Contains("Lavagem Completa", _dispatcher.SentMessages[0].Text);
    }

    [Fact]
    public async Task ProcessIncomingMessage_FullBookingFlow_ShouldCreateBooking()
    {
        // 1: Menu
        await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "Oi");

        // 2: Escolhe 1 (Agendar)
        await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "1");

        // 3: Escolhe o primeiro serviço (1: Lavagem Completa)
        await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "1");

        // 4: Escolhe o porte do veículo (1: Hatch / Sedan)
        await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "1");

        // 5: Escolhe a data (1: Hoje)
        await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "1");

        // 6: Escolhe o primeiro horário vago (1: 10:00)
        await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "1");

        // 7: Digita a placa
        await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "ABC-1234");

        _dispatcher.SentMessages.Clear();

        // 8: Confirma o agendamento (1)
        var result = await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "1");

        Assert.True(result.IsSuccess);
        Assert.Single(_dispatcher.SentMessages);
        Assert.Contains("Agendamento Confirmado com Sucesso", _dispatcher.SentMessages[0].Text);
        Assert.Contains("BK-2610-TEST", _dispatcher.SentMessages[0].Text);
        Assert.NotNull(_schedulingLookup.LastCreatedRequest);
        Assert.Equal("ABC1234", _schedulingLookup.LastCreatedRequest.VehiclePlate);
    }

    [Fact]
    public async Task ProcessIncomingMessage_WhenResetCommandTyped_ShouldResetAndSendMenu()
    {
        // Começa conversa
        await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "Oi");
        await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "1");

        _dispatcher.SentMessages.Clear();

        // Digita "menu"
        var result = await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "menu");

        Assert.True(result.IsSuccess);
        Assert.Contains("Como podemos te ajudar hoje?", _dispatcher.SentMessages[0].Text);
    }

    private sealed class InMemoryChatbotSessionRepository : IChatbotSessionRepository
    {
        private readonly List<ChatbotConversationSession> _sessions = [];

        public Task<ChatbotConversationSession?> GetActiveByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default)
        {
            var normalized = new string(customerPhone.Where(char.IsDigit).ToArray());
            return Task.FromResult(_sessions.FirstOrDefault(s => s.TenantId == tenantId && s.CustomerPhone == normalized && s.IsActive));
        }

        public Task AddAsync(ChatbotConversationSession session, CancellationToken ct = default)
        {
            _sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ChatbotConversationSession session, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeSchedulingBookingLookup : ISchedulingBookingLookup
    {
        public CreateBookingFromChatbotRequest? LastCreatedRequest { get; private set; }

        public Task<Result<IReadOnlyList<ServiceDto>>> GetAvailableServicesCatalogAsync(Guid tenantId, CancellationToken ct = default)
        {
            var prices = new[]
            {
                new ServicePriceDto(VehicleSizeConstants.HatchSedan, 60m, 45),
                new ServicePriceDto(VehicleSizeConstants.Suv, 80m, 60)
            };
            var services = new[]
            {
                new ServiceDto(Guid.NewGuid(), "Lavagem Completa", "Lavagens", prices)
            };
            return Task.FromResult(Result<IReadOnlyList<ServiceDto>>.Success(services));
        }

        public Task<Result<IReadOnlyList<AvailableTimeSlotDto>>> GetAvailableTimeSlotsAsync(Guid tenantId, DateOnly date, Guid serviceId, string vehicleSize, CancellationToken ct = default)
        {
            var slots = new[]
            {
                new AvailableTimeSlotDto(new TimeOnly(10, 0), new TimeOnly(11, 0), 2, 2),
                new AvailableTimeSlotDto(new TimeOnly(14, 0), new TimeOnly(15, 0), 1, 2)
            };
            return Task.FromResult(Result<IReadOnlyList<AvailableTimeSlotDto>>.Success(slots));
        }

        public Task<Result<BookingConfirmationDto>> CreateBookingFromChatbotAsync(Guid tenantId, CreateBookingFromChatbotRequest request, CancellationToken ct = default)
        {
            LastCreatedRequest = request;
            var confirmation = new BookingConfirmationDto(
                Guid.NewGuid(),
                "BK-2610-TEST",
                request.CustomerName ?? "Cliente",
                "Lavagem Completa",
                request.ScheduledDate,
                request.ScheduledTime,
                60m);
            return Task.FromResult(Result<BookingConfirmationDto>.Success(confirmation));
        }

        public Task<Result<IReadOnlyList<BookingSummaryDto>>> GetCustomerActiveBookingsAsync(Guid tenantId, string customerPhone, CancellationToken ct = default)
        {
            return Task.FromResult<Result<IReadOnlyList<BookingSummaryDto>>>(Result<IReadOnlyList<BookingSummaryDto>>.Success([]));
        }
    }

    private sealed class FakeTenantStoreProfileLookup : ITenantStoreProfileLookup
    {
        public Task<Result<StoreProfileDto>> GetProfileAsync(Guid tenantId, CancellationToken ct = default)
        {
            var profile = new StoreProfileDto(
                Guid.NewGuid(),
                tenantId,
                "Razao Social",
                "Estética Lava Show",
                "12345678000199",
                "11988887777",
                "Rua das Flores, 100",
                "São Paulo",
                "SP",
                "01001-000",
                null,
                null,
                null);
            return Task.FromResult(Result<StoreProfileDto>.Success(profile));
        }
    }

    private sealed class FakeOutboundWhatsAppDispatcher : IOutboundWhatsAppDispatcher
    {
        public readonly List<(string Phone, string Text)> SentMessages = [];

        public Task<Result<WhatsAppMessageDto>> DispatchTextMessageAsync(Guid tenantId, string recipientPhone, string messageText, string? idempotencyKey = null, CancellationToken ct = default)
        {
            SentMessages.Add((recipientPhone, messageText));
            var dto = new WhatsAppMessageDto(Guid.NewGuid(), recipientPhone, messageText, WhatsAppMessageStatusConstants.Sent, null, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
            return Task.FromResult(Result<WhatsAppMessageDto>.Success(dto));
        }

        public Task<Result<WhatsAppMessageDto>> DispatchMediaMessageAsync(Guid tenantId, string recipientPhone, string caption, string mediaType, string mediaUrlOrBase64, string mediaMimeType, string mediaFileName, string? idempotencyKey = null, CancellationToken ct = default)
        {
            return Task.FromResult(Result<WhatsAppMessageDto>.Failure(new Error("not_implemented", "Not used in test", ErrorType.Validation)));
        }

        public Task<Result<IReadOnlyList<WhatsAppMessageDto>>> GetRecentMessagesAsync(Guid tenantId, int count = 20, CancellationToken ct = default)
        {
            return Task.FromResult(Result<IReadOnlyList<WhatsAppMessageDto>>.Success([]));
        }
    }
}
