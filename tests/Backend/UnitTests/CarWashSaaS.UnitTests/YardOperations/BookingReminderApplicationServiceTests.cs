using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class BookingReminderApplicationServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _serviceId = Guid.NewGuid();
    private readonly InMemoryBookingRepository _bookingRepo = new();
    private readonly FakeWhatsAppDispatcher _messageDispatcher = new();
    private readonly FakeTenantStoreProfileLookup _profileLookup = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly BookingReminderApplicationService _sut;

    public BookingReminderApplicationServiceTests()
    {
        _sut = new BookingReminderApplicationService(
            _bookingRepo,
            _messageDispatcher,
            _profileLookup,
            _unitOfWork,
            NullLogger<BookingReminderApplicationService>.Instance);
    }

    [Fact]
    public async Task ExecuteReminderScan_WhenPending24h_ShouldDispatchMessageAndMarkSent()
    {
        var now = DateTimeOffset.UtcNow;
        var tomorrow = DateOnly.FromDateTime(now.LocalDateTime.AddHours(24));
        var time = TimeOnly.FromTimeSpan(now.LocalDateTime.AddHours(24).TimeOfDay);

        var booking = Booking.Create(
            _tenantId,
            "Fernanda",
            "11988889999",
            "ABC-1234",
            "Onix",
            VehicleSize.HatchSedan,
            _serviceId,
            "Lavagem Simples",
            50m,
            tomorrow,
            time,
            60).Value!;

        await _bookingRepo.AddAsync(booking);

        var result = await _sut.ExecuteReminderScanForTenantAsync(_tenantId, now);

        Assert.True(result.IsSuccess);
        Assert.Single(_messageDispatcher.DispatchedMessages);
        var msg = _messageDispatcher.DispatchedMessages[0];
        Assert.Equal("11988889999", msg.RecipientPhone);
        Assert.Contains("Lembrete de Agendamento", msg.Text);
        Assert.Contains("ABC1234", msg.Text);
        Assert.NotNull(booking.Reminder24hSentAt);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ExecuteReminderScan_WhenPending2h_ShouldDispatchMessageAndMarkSent()
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.LocalDateTime.AddHours(2));
        var time = TimeOnly.FromTimeSpan(now.LocalDateTime.AddHours(2).TimeOfDay);

        var booking = Booking.Create(
            _tenantId,
            "Roberto",
            "11977778888",
            "XYZ-9999",
            "Compass",
            VehicleSize.Suv,
            _serviceId,
            "Lavagem Completa",
            80m,
            today,
            time,
            60).Value!;

        await _bookingRepo.AddAsync(booking);

        var result = await _sut.ExecuteReminderScanForTenantAsync(_tenantId, now);

        Assert.True(result.IsSuccess);
        Assert.Single(_messageDispatcher.DispatchedMessages);
        var msg = _messageDispatcher.DispatchedMessages[0];
        Assert.Equal("11977778888", msg.RecipientPhone);
        Assert.Contains("Seu horário está chegando", msg.Text);
        Assert.NotNull(booking.Reminder2hSentAt);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    private sealed class InMemoryBookingRepository : IBookingRepository
    {
        public readonly List<Booking> Bookings = [];

        public Task<Booking?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Bookings.FirstOrDefault(b => b.Id == id));

        public Task<Booking?> GetByProtocolAsync(string protocol, CancellationToken ct = default)
            => Task.FromResult(Bookings.FirstOrDefault(b => b.Protocol == protocol));

        public Task<IReadOnlyList<Booking>> GetByDateAsync(Guid tenantId, DateOnly date, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Booking>>(Bookings.Where(b => b.TenantId == tenantId && b.ScheduledDate == date).ToList());

        public Task<IReadOnlyList<Booking>> GetActiveBookingsByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Booking>>(Bookings.Where(b => b.TenantId == tenantId && b.CustomerPhone == customerPhone).ToList());

        public Task<int> CountActiveBookingsInSlotAsync(Guid tenantId, DateOnly date, TimeOnly startTime, TimeOnly endTime, CancellationToken ct = default)
            => Task.FromResult(0);

        public Task AddAsync(Booking booking, CancellationToken ct = default)
        {
            Bookings.Add(booking);
            return Task.CompletedTask;
        }

        public void Update(Booking booking)
        {
            var idx = Bookings.FindIndex(b => b.Id == booking.Id);
            if (idx >= 0) Bookings[idx] = booking;
        }

        public Task<IReadOnlyList<Booking>> GetBookingsPending24hReminderAsync(Guid tenantId, DateTimeOffset referenceTime, CancellationToken ct = default)
        {
            var targetStart = referenceTime.AddHours(23);
            var targetEnd = referenceTime.AddHours(25);
            var list = Bookings.Where(b => b.TenantId == tenantId &&
                                           b.Status is BookingStatus.Scheduled or BookingStatus.Confirmed &&
                                           b.Reminder24hSentAt == null &&
                                           b.ScheduledDate.ToDateTime(b.ScheduledTime) >= targetStart.LocalDateTime &&
                                           b.ScheduledDate.ToDateTime(b.ScheduledTime) <= targetEnd.LocalDateTime).ToList();
            return Task.FromResult<IReadOnlyList<Booking>>(list);
        }

        public Task<IReadOnlyList<Booking>> GetBookingsPending2hReminderAsync(Guid tenantId, DateTimeOffset referenceTime, CancellationToken ct = default)
        {
            var targetStart = referenceTime.AddMinutes(105);
            var targetEnd = referenceTime.AddMinutes(135);
            var list = Bookings.Where(b => b.TenantId == tenantId &&
                                           b.Status is BookingStatus.Scheduled or BookingStatus.Confirmed &&
                                           b.Reminder2hSentAt == null &&
                                           b.ScheduledDate.ToDateTime(b.ScheduledTime) >= targetStart.LocalDateTime &&
                                           b.ScheduledDate.ToDateTime(b.ScheduledTime) <= targetEnd.LocalDateTime).ToList();
            return Task.FromResult<IReadOnlyList<Booking>>(list);
        }

        public Task<Booking?> GetUpcomingActiveBookingByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default)
            => Task.FromResult(Bookings.FirstOrDefault(b => b.TenantId == tenantId && b.CustomerPhone == customerPhone));
    }

    private sealed class FakeWhatsAppDispatcher : IOutboundWhatsAppDispatcher
    {
        public readonly List<(Guid TenantId, string RecipientPhone, string Text, string? IdempotencyKey)> DispatchedMessages = [];

        public Task<Result<WhatsAppMessageDto>> DispatchTextMessageAsync(Guid tenantId, string recipientPhone, string text, string? idempotencyKey = null, CancellationToken ct = default)
        {
            DispatchedMessages.Add((tenantId, recipientPhone, text, idempotencyKey));
            var dto = new WhatsAppMessageDto(
                Guid.NewGuid(),
                recipientPhone,
                text,
                "queued",
                null,
                0,
                DateTimeOffset.UtcNow,
                null,
                null);
            return Task.FromResult(Result<WhatsAppMessageDto>.Success(dto));
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
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IReadOnlyList<WhatsAppMessageDto>>> GetRecentMessagesAsync(
            Guid tenantId,
            int count = 20,
            CancellationToken ct = default)
            => Task.FromResult(Result<IReadOnlyList<WhatsAppMessageDto>>.Success([]));
    }

    private sealed class FakeTenantStoreProfileLookup : ITenantStoreProfileLookup
    {
        public Task<Result<StoreProfileDto>> GetProfileAsync(Guid tenantId, CancellationToken ct = default)
        {
            var profile = new StoreProfileDto(
                Guid.NewGuid(),
                tenantId,
                "Lava-Jato Legal LTDA",
                "Lavaway Centro",
                "12345678000199",
                "11988880000",
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

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<Result<int>> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCount++;
            return Task.FromResult(Result<int>.Success(1));
        }
    }
}
