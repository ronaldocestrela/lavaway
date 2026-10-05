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
    private readonly FakeCustomerCommunicationPreferenceRepository _preferenceRepo = new();
    private readonly FakeAfterSalesLookup _afterSalesLookup = new();
    private readonly ChatbotConversationEngine _sut;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly string _customerPhone = "11999998888";

    public ChatbotConversationEngineTests()
    {
        _sut = new ChatbotConversationEngine(
            _sessionRepo,
            _schedulingLookup,
            _storeProfileLookup,
            _dispatcher,
            _preferenceRepo,
            _afterSalesLookup);
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

    [Fact]
    public async Task ProcessIncomingMessage_WhenCustomerConfirmsUpcomingBooking_ShouldConfirmAndSendSuccess()
    {
        var bookingId = Guid.NewGuid();
        _schedulingLookup.UpcomingBookingToReturn = new BookingSummaryDto(
            bookingId,
            _tenantId,
            "BK-CONF-7777",
            null,
            "João",
            _customerPhone,
            "BRA2E19",
            "Civic",
            VehicleSizeConstants.HatchSedan,
            Guid.NewGuid(),
            "Lavagem Completa",
            70m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(14, 0),
            60,
            BookingStatusConstants.Scheduled,
            BookingOriginConstants.WhatsAppBot,
            null,
            null,
            DateTimeOffset.UtcNow);

        _dispatcher.SentMessages.Clear();

        // Cliente responde "1" ou "confirmar"
        var result = await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "confirmar");

        Assert.True(result.IsSuccess);
        Assert.True(_schedulingLookup.ConfirmBookingCalled);
        Assert.Single(_dispatcher.SentMessages);
        Assert.Contains("Presença Confirmada com Sucesso", _dispatcher.SentMessages[0].Text);
        Assert.Contains("BRA2E19", _dispatcher.SentMessages[0].Text);
    }

    [Fact]
    public async Task ProcessIncomingMessage_WhenCustomerCancelsUpcomingBooking_ShouldCancelAndSendSuccess()
    {
        var bookingId = Guid.NewGuid();
        _schedulingLookup.UpcomingBookingToReturn = new BookingSummaryDto(
            bookingId,
            _tenantId,
            "BK-CANC-8888",
            null,
            "João",
            _customerPhone,
            "XYZ-9999",
            "Corolla",
            VehicleSizeConstants.HatchSedan,
            Guid.NewGuid(),
            "Lavagem Completa",
            70m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(10, 0),
            60,
            BookingStatusConstants.Scheduled,
            BookingOriginConstants.WhatsAppBot,
            null,
            null,
            DateTimeOffset.UtcNow);

        _dispatcher.SentMessages.Clear();

        // Cliente responde "cancelar"
        var result = await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "cancelar");

        Assert.True(result.IsSuccess);
        Assert.True(_schedulingLookup.CancelBookingCalled);
        Assert.Single(_dispatcher.SentMessages);
        Assert.Contains("Agendamento Cancelado com Sucesso", _dispatcher.SentMessages[0].Text);
    }

    [Fact]
    public async Task ProcessIncomingMessage_WhenCustomerReschedules_ShouldGuideAndReschedule()
    {
        var bookingId = Guid.NewGuid();
        _schedulingLookup.UpcomingBookingToReturn = new BookingSummaryDto(
            bookingId,
            _tenantId,
            "BK-RESCH-9999",
            null,
            "João",
            _customerPhone,
            "ABC-1234",
            "Onix",
            VehicleSizeConstants.HatchSedan,
            Guid.NewGuid(),
            "Lavagem Completa",
            70m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(10, 0),
            60,
            BookingStatusConstants.Scheduled,
            BookingOriginConstants.WhatsAppBot,
            null,
            null,
            DateTimeOffset.UtcNow);

        // 1: Cliente diz "remarcar"
        var r1 = await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "remarcar");
        Assert.True(r1.IsSuccess);
        Assert.Contains("Remarcação de Agendamento", _dispatcher.SentMessages.Last().Text);

        // 2: Escolhe data (1: Hoje)
        var r2 = await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "1");
        Assert.True(r2.IsSuccess);
        Assert.Contains("Horários disponíveis", _dispatcher.SentMessages.Last().Text);

        // 3: Escolhe horário (1: 10:00)
        var r3 = await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "1");
        Assert.True(r3.IsSuccess);
        Assert.True(_schedulingLookup.RescheduleBookingCalled);
        Assert.Contains("Agendamento Remarcado com Sucesso", _dispatcher.SentMessages.Last().Text);
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

        public BookingSummaryDto? UpcomingBookingToReturn { get; set; }
        public bool ConfirmBookingCalled { get; private set; }
        public bool CancelBookingCalled { get; private set; }
        public bool RescheduleBookingCalled { get; private set; }

        public Task<Result<BookingSummaryDto>> ConfirmBookingAsync(Guid tenantId, Guid bookingId, CancellationToken ct = default)
        {
            ConfirmBookingCalled = true;
            var summary = UpcomingBookingToReturn ?? new BookingSummaryDto(
                bookingId,
                tenantId,
                "BK-CONF-1234",
                null,
                "Cliente Teste",
                "11999998888",
                "ABC1234",
                "Civic",
                VehicleSizeConstants.HatchSedan,
                Guid.NewGuid(),
                "Lavagem",
                60m,
                DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                new TimeOnly(10, 0),
                60,
                BookingStatusConstants.Confirmed,
                BookingOriginConstants.WhatsAppBot,
                null,
                null,
                DateTimeOffset.UtcNow,
                null,
                null,
                DateTimeOffset.UtcNow);

            return Task.FromResult(Result<BookingSummaryDto>.Success(summary));
        }

        public Task<Result<BookingSummaryDto>> CancelBookingAsync(Guid tenantId, Guid bookingId, string? reason, CancellationToken ct = default)
        {
            CancelBookingCalled = true;
            var summary = UpcomingBookingToReturn ?? new BookingSummaryDto(
                bookingId,
                tenantId,
                "BK-CANC-1234",
                null,
                "Cliente Teste",
                "11999998888",
                "ABC1234",
                "Civic",
                VehicleSizeConstants.HatchSedan,
                Guid.NewGuid(),
                "Lavagem",
                60m,
                DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                new TimeOnly(10, 0),
                60,
                BookingStatusConstants.Cancelled,
                BookingOriginConstants.WhatsAppBot,
                null,
                reason,
                DateTimeOffset.UtcNow);

            return Task.FromResult(Result<BookingSummaryDto>.Success(summary));
        }

        public Task<Result<BookingSummaryDto>> RescheduleBookingAsync(Guid tenantId, Guid bookingId, DateOnly newDate, TimeOnly newTime, CancellationToken ct = default)
        {
            RescheduleBookingCalled = true;
            var summary = UpcomingBookingToReturn ?? new BookingSummaryDto(
                bookingId,
                tenantId,
                "BK-RESCH-1234",
                null,
                "Cliente Teste",
                "11999998888",
                "ABC1234",
                "Civic",
                VehicleSizeConstants.HatchSedan,
                Guid.NewGuid(),
                "Lavagem",
                60m,
                newDate,
                newTime,
                60,
                BookingStatusConstants.Confirmed,
                BookingOriginConstants.WhatsAppBot,
                null,
                null,
                DateTimeOffset.UtcNow);

            return Task.FromResult(Result<BookingSummaryDto>.Success(summary));
        }

        public Task<Result<BookingSummaryDto?>> GetUpcomingBookingForCustomerAsync(Guid tenantId, string customerPhone, CancellationToken ct = default)
        {
            return Task.FromResult<Result<BookingSummaryDto?>>(Result<BookingSummaryDto?>.Success(UpcomingBookingToReturn));
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

    private sealed class FakeCustomerCommunicationPreferenceRepository : ICustomerCommunicationPreferenceRepository
    {
        public readonly Dictionary<string, CustomerCommunicationPreference> Preferences = new();

        public Task<CustomerCommunicationPreference?> GetByPhoneAsync(Guid tenantId, string normalizedPhone, CancellationToken ct = default)
        {
            Preferences.TryGetValue(normalizedPhone, out var pref);
            return Task.FromResult(pref);
        }

        public Task<IReadOnlyList<CustomerCommunicationPreference>> ListPreferencesAsync(Guid tenantId, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<CustomerCommunicationPreference>>(Preferences.Values.ToList());
        }

        public Task AddAsync(CustomerCommunicationPreference preference, CancellationToken ct = default)
        {
            Preferences[preference.NormalizedPhone] = preference;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeAfterSalesLookup : IAfterSalesLookup
    {
        public SatisfactionSurveyDto? PendingSurvey { get; set; }
        public (int Rating, string? Comment)? LastSubmittedRating { get; private set; }

        public Task<Result<WorkOrderDto>> RegisterPickupAsync(Guid tenantId, Guid workOrderId, DateTimeOffset? pickedUpAtUtc = null, string? notes = null, CancellationToken ct = default)
        {
            return Task.FromResult(Result<WorkOrderDto>.Failure(new Error("not_impl", "Not implemented", ErrorType.Validation)));
        }

        public Task<Result> SubmitSurveyRatingAsync(Guid tenantId, string customerPhone, int rating, string? feedbackComment = null, CancellationToken ct = default)
        {
            LastSubmittedRating = (rating, feedbackComment);
            return Task.FromResult(Result.Success());
        }

        public Task<Result<SatisfactionSurveyDto?>> GetPendingSurveyForCustomerAsync(Guid tenantId, string customerPhone, CancellationToken ct = default)
        {
            return Task.FromResult(Result<SatisfactionSurveyDto?>.Success(PendingSurvey));
        }
    }

    [Theory]
    [InlineData("PARAR")]
    [InlineData("sair")]
    [InlineData("STOP")]
    [InlineData("descadastrar")]
    [InlineData("não quero")]
    public async Task ProcessIncomingMessage_When_OptOut_Keyword_Received_Should_Register_OptOut_And_Send_Confirmation(string keyword)
    {
        var result = await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", keyword);

        Assert.True(result.IsSuccess);
        var normalized = OutboundWhatsAppMessage.CleanPhoneNumber(_customerPhone);
        Assert.True(_preferenceRepo.Preferences.TryGetValue(normalized, out var pref));
        Assert.False(pref.IsOptedIn);
        Assert.NotNull(pref.OptedOutAtUtc);

        Assert.Single(_dispatcher.SentMessages);
        Assert.Contains("descadastrado", _dispatcher.SentMessages[0].Text);
        Assert.Contains("QUERO", _dispatcher.SentMessages[0].Text);
    }

    [Fact]
    public async Task ProcessIncomingMessage_When_OptIn_Keyword_Received_Should_Register_OptIn_And_Send_Confirmation()
    {
        // Pré-cadastra como opted out
        var normalized = OutboundWhatsAppMessage.CleanPhoneNumber(_customerPhone);
        var initialPref = CustomerCommunicationPreference.Create(_tenantId, normalized, isOptedIn: false).Value!;
        await _preferenceRepo.AddAsync(initialPref);

        var result = await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", "QUERO");

        Assert.True(result.IsSuccess);
        Assert.True(initialPref.IsOptedIn);
        Assert.Null(initialPref.OptedOutAtUtc);

        Assert.Single(_dispatcher.SentMessages);
        Assert.Contains("reativadas com sucesso", _dispatcher.SentMessages[0].Text);
    }

    [Theory]
    [InlineData("5", 5)]
    [InlineData("⭐⭐⭐⭐⭐", 5)]
    [InlineData("Excelente", 5)]
    [InlineData("4", 4)]
    public async Task ProcessIncomingMessage_When_Survey_Pending_And_High_Rating_Received_Should_Submit_And_Thank(string input, int expectedRating)
    {
        _afterSalesLookup.PendingSurvey = new SatisfactionSurveyDto(
            Guid.NewGuid(), Guid.NewGuid(), "João", _customerPhone, "ABC1D23", "Corolla", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddMinutes(-30), null, null, null, "Pendente");

        var result = await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", input);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_afterSalesLookup.LastSubmittedRating);
        Assert.Equal(expectedRating, _afterSalesLookup.LastSubmittedRating.Value.Rating);

        Assert.Single(_dispatcher.SentMessages);
        Assert.Contains("Muito obrigado pela sua avaliação", _dispatcher.SentMessages[0].Text);
        Assert.Contains("estrelas", _dispatcher.SentMessages[0].Text);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("2", 2)]
    [InlineData("3", 3)]
    public async Task ProcessIncomingMessage_When_Survey_Pending_And_Low_Rating_Received_Should_Submit_And_Empathize(string input, int expectedRating)
    {
        _afterSalesLookup.PendingSurvey = new SatisfactionSurveyDto(
            Guid.NewGuid(), Guid.NewGuid(), "João", _customerPhone, "ABC1D23", "Corolla", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddMinutes(-30), null, null, null, "Pendente");

        var result = await _sut.ProcessIncomingMessageAsync(_tenantId, _customerPhone, "João", input);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_afterSalesLookup.LastSubmittedRating);
        Assert.Equal(expectedRating, _afterSalesLookup.LastSubmittedRating.Value.Rating);

        Assert.Single(_dispatcher.SentMessages);
        Assert.Contains("retorno sincero", _dispatcher.SentMessages[0].Text);
        Assert.Contains("Lamentamos que sua experiência não tenha sido impecável", _dispatcher.SentMessages[0].Text);
    }
}
