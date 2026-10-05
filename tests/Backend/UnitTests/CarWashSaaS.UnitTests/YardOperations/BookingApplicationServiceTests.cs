using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class BookingApplicationServiceTests
{
    private readonly InMemoryBookingRepository _bookingRepo = new();
    private readonly InMemoryServiceRepository _serviceRepo = new();
    private readonly InMemoryYardCapacityRepository _capacityRepo = new();
    private readonly InMemoryCustomerVehicleSearchRepository _customerSearchRepo = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly BookingApplicationService _sut;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _serviceId = Guid.NewGuid();

    public BookingApplicationServiceTests()
    {
        _sut = new BookingApplicationService(
            _bookingRepo,
            _serviceRepo,
            _capacityRepo,
            _customerSearchRepo,
            _unitOfWork);
    }

    [Fact]
    public async Task GetAvailableServicesCatalogAsync_ShouldReturnServicesWithPrices()
    {
        var prices = new[]
        {
            ServicePrice.Create(_tenantId, VehicleSize.HatchSedan, 60m, 45).Value!,
            ServicePrice.Create(_tenantId, VehicleSize.Suv, 80m, 60).Value!
        };
        var service = Service.Create(_tenantId, "Lavagem Completa", "Lavagens", prices).Value!;
        await _serviceRepo.AddAsync(service);

        var result = await _sut.GetAvailableServicesCatalogAsync(_tenantId);

        Assert.True(result.IsSuccess);
        var list = result.Value!;
        Assert.Single(list);
        Assert.Equal("Lavagem Completa", list[0].Name);
        Assert.Equal(2, list[0].Prices.Count);
    }

    [Fact]
    public async Task CreateBookingFromChatbotAsync_WhenCapacityAvailable_ShouldCreateAndConfirmBooking()
    {
        var prices = new[]
        {
            ServicePrice.Create(_tenantId, VehicleSize.HatchSedan, 60m, 45).Value!
        };
        var service = Service.Create(_tenantId, "Ducha", "Lavagens", prices).Value!;
        await _serviceRepo.AddAsync(service);

        var capacity = YardCapacity.Create(_tenantId, 2, "Pátio").Value!;
        await _capacityRepo.AddAsync(capacity);

        var request = new CreateBookingFromChatbotRequest(
            "11999998888",
            "Cliente Teste",
            "ABC-1234",
            "Gol",
            VehicleSizeConstants.HatchSedan,
            service.Id,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(14, 0));

        var result = await _sut.CreateBookingFromChatbotAsync(_tenantId, request);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ducha", result.Value!.ServiceName);
        Assert.Equal(60m, result.Value.EstimatedPrice);
        Assert.Single(_bookingRepo.Bookings);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateBookingFromChatbotAsync_WhenCapacityExceeded_ShouldReturnConflictError()
    {
        var prices = new[]
        {
            ServicePrice.Create(_tenantId, VehicleSize.HatchSedan, 60m, 45).Value!
        };
        var service = Service.Create(_tenantId, "Ducha", "Lavagens", prices).Value!;
        await _serviceRepo.AddAsync(service);

        // Capacidade de 1 box, adicionando já 1 reserva ativa no mesmo slot
        var capacity = YardCapacity.Create(_tenantId, 1, "Pátio Pequeno").Value!;
        await _capacityRepo.AddAsync(capacity);

        var existingBooking = Booking.Create(
            _tenantId,
            "Cliente 1",
            "11911111111",
            "AAA-1111",
            null,
            VehicleSize.HatchSedan,
            service.Id,
            service.Name,
            60m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(14, 0)).Value!;
        existingBooking.Confirm();
        await _bookingRepo.AddAsync(existingBooking);

        var request = new CreateBookingFromChatbotRequest(
            "11999998888",
            "Cliente 2",
            "XYZ-9999",
            null,
            VehicleSizeConstants.HatchSedan,
            service.Id,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(14, 0));

        var result = await _sut.CreateBookingFromChatbotAsync(_tenantId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal("booking.capacity.exceeded", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public async Task CancelBookingAsync_WhenBookingExists_ShouldUpdateStatus()
    {
        var booking = Booking.Create(
            _tenantId,
            "Carlos",
            "11988887777",
            "ABC-1234",
            null,
            VehicleSize.HatchSedan,
            _serviceId,
            "Lavagem",
            50m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(10, 0)).Value!;

        await _bookingRepo.AddAsync(booking);

        var result = await _sut.CancelBookingAsync(_tenantId, booking.Id, "Motivo pessoal");

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ConfirmBooking_ShouldSucceedAndRecordTimestamp()
    {
        var booking = Booking.Create(
            _tenantId,
            "Carlos",
            "11988887777",
            "ABC-1234",
            null,
            VehicleSize.HatchSedan,
            _serviceId,
            "Lavagem",
            50m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(10, 0)).Value!;

        await _bookingRepo.AddAsync(booking);

        var result = await _sut.ConfirmBookingAsync(_tenantId, booking.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.NotNull(booking.ConfirmedAtUtc);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task RescheduleBooking_WithAvailableCapacity_ShouldSucceed()
    {
        var booking = Booking.Create(
            _tenantId,
            "Carlos",
            "11988887777",
            "ABC-1234",
            null,
            VehicleSize.HatchSedan,
            _serviceId,
            "Lavagem",
            50m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(10, 0),
            60).Value!;

        await _bookingRepo.AddAsync(booking);

        var newDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2));
        var newTime = new TimeOnly(14, 0);

        var result = await _sut.RescheduleBookingAsync(_tenantId, booking.Id, newDate, newTime);

        Assert.True(result.IsSuccess);
        Assert.Equal(newDate, booking.ScheduledDate);
        Assert.Equal(newTime, booking.ScheduledTime);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task RescheduleBooking_WhenCapacityExceeded_ShouldReturnConflict()
    {
        // Capacidade de 1 box já preenchida
        await _capacityRepo.AddAsync(YardCapacity.Create(_tenantId, 1, "Box 1").Value!);

        var existingBooking = Booking.Create(
            _tenantId,
            "Cliente 1",
            "11999991111",
            "AAA-1111",
            null,
            VehicleSize.HatchSedan,
            _serviceId,
            "Lavagem",
            50m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            new TimeOnly(14, 0),
            60).Value!;
        existingBooking.Confirm();
        await _bookingRepo.AddAsync(existingBooking);

        var bookingToReschedule = Booking.Create(
            _tenantId,
            "Cliente 2",
            "11999992222",
            "BBB-2222",
            null,
            VehicleSize.HatchSedan,
            _serviceId,
            "Lavagem",
            50m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(10, 0),
            60).Value!;
        await _bookingRepo.AddAsync(bookingToReschedule);

        var result = await _sut.RescheduleBookingAsync(_tenantId, bookingToReschedule.Id, DateOnly.FromDateTime(DateTime.Today.AddDays(2)), new TimeOnly(14, 0));

        Assert.False(result.IsSuccess);
        Assert.Equal("booking.capacity.exceeded", result.Error!.Code);
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
            => Task.FromResult<IReadOnlyList<Booking>>(Bookings.Where(b => b.TenantId == tenantId && b.CustomerPhone == customerPhone && b.Status is BookingStatus.Scheduled or BookingStatus.Confirmed).ToList());

        public Task<int> CountActiveBookingsInSlotAsync(Guid tenantId, DateOnly date, TimeOnly startTime, TimeOnly endTime, CancellationToken ct = default)
        {
            var count = Bookings.Count(b =>
                b.TenantId == tenantId &&
                b.ScheduledDate == date &&
                b.Status is BookingStatus.Scheduled or BookingStatus.Confirmed or BookingStatus.Arrived &&
                b.ScheduledTime >= startTime && b.ScheduledTime < endTime);
            return Task.FromResult(count);
        }

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
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var booking = Bookings
                .Where(b => b.TenantId == tenantId &&
                            b.CustomerPhone == customerPhone &&
                            b.Status is BookingStatus.Scheduled or BookingStatus.Confirmed &&
                            b.ScheduledDate >= today)
                .OrderBy(b => b.ScheduledDate)
                .ThenBy(b => b.ScheduledTime)
                .FirstOrDefault();
            return Task.FromResult(booking);
        }
    }

    private sealed class InMemoryServiceRepository : IServiceRepository
    {
        private readonly List<Service> _services = [];

        public Task<IReadOnlyCollection<Service>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyCollection<Service>>(_services.Where(s => s.TenantId == tenantId).ToList());

        public Task<Service?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
            => Task.FromResult(_services.FirstOrDefault(s => s.TenantId == tenantId && s.Id == id));

        public Task AddAsync(Service service, CancellationToken ct = default)
        {
            _services.Add(service);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Service service, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemoryYardCapacityRepository : IYardCapacityRepository
    {
        private YardCapacity? _capacity;

        public Task<YardCapacity?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(_capacity?.TenantId == tenantId ? _capacity : null);

        public Task AddAsync(YardCapacity yardCapacity, CancellationToken ct = default)
        {
            _capacity = yardCapacity;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(YardCapacity yardCapacity, CancellationToken ct = default)
        {
            _capacity = yardCapacity;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryCustomerVehicleSearchRepository : ICustomerVehicleSearchRepository
    {
        public Task<CustomerVehicleMatchDto?> GetByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
            => Task.FromResult<CustomerVehicleMatchDto?>(null);

        public Task<IReadOnlyCollection<CustomerVehicleMatchDto>> SearchAsync(Guid tenantId, string? normalizedPlate, string? normalizedPhone, int limit, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyCollection<CustomerVehicleMatchDto>>([]);
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
