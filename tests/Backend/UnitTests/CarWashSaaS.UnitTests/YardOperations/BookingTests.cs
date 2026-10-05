using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class BookingTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _serviceId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        var scheduledDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var scheduledTime = new TimeOnly(10, 0);

        var result = Booking.Create(
            _tenantId,
            "João Silva",
            "11999998888",
            "ABC-1234",
            "Civic",
            VehicleSize.HatchSedan,
            _serviceId,
            "Lavagem Completa",
            75.00m,
            scheduledDate,
            scheduledTime,
            60,
            BookingOrigin.WhatsAppBot);

        Assert.True(result.IsSuccess);
        var booking = result.Value!;
        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(_tenantId, booking.TenantId);
        Assert.Equal("João Silva", booking.CustomerName);
        Assert.Equal("11999998888", booking.CustomerPhone);
        Assert.Equal("ABC1234", booking.VehiclePlate);
        Assert.Equal(VehicleSize.HatchSedan, booking.VehicleSize);
        Assert.Equal(75.00m, booking.EstimatedPrice);
        Assert.Equal(BookingStatus.Scheduled, booking.Status);
        Assert.Equal(BookingOrigin.WhatsAppBot, booking.Origin);
        Assert.StartsWith("BK-", booking.Protocol);
    }

    [Fact]
    public void Create_WithEmptyTenant_ShouldFail()
    {
        var result = Booking.Create(
            Guid.Empty,
            "João Silva",
            "11999998888",
            "ABC-1234",
            null,
            VehicleSize.HatchSedan,
            _serviceId,
            "Lavagem",
            50m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(10, 0),
            45,
            BookingOrigin.WhatsAppBot);

        Assert.False(result.IsSuccess);
        Assert.Equal("booking.tenant.required", result.Error!.Code);
    }

    [Fact]
    public void Create_WithInvalidPlate_ShouldFail()
    {
        var result = Booking.Create(
            _tenantId,
            "João Silva",
            "11999998888",
            "123",
            null,
            VehicleSize.HatchSedan,
            _serviceId,
            "Lavagem",
            50m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(10, 0),
            45,
            BookingOrigin.WhatsAppBot);

        Assert.False(result.IsSuccess);
        Assert.Equal("booking.plate.invalid", result.Error!.Code);
    }

    [Fact]
    public void Confirm_WhenScheduled_ShouldTransitionToConfirmed()
    {
        var booking = Booking.Create(
            _tenantId,
            "Maria",
            "11999991111",
            "XYZ-9999",
            null,
            VehicleSize.Suv,
            _serviceId,
            "Polimento",
            200m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(14, 0),
            120,
            BookingOrigin.WhatsAppBot).Value!;

        var result = booking.Confirm();

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
    }

    [Fact]
    public void MarkAsArrived_ShouldLinkWorkOrderAndSetStatusArrived()
    {
        var booking = Booking.Create(
            _tenantId,
            "Carlos",
            "11988882222",
            "BRA2E19",
            "Corolla",
            VehicleSize.HatchSedan,
            _serviceId,
            "Ducha",
            40m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(9, 0),
            30,
            BookingOrigin.WhatsAppBot).Value!;

        var workOrderId = Guid.NewGuid();
        var result = booking.MarkAsArrived(workOrderId);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Arrived, booking.Status);
        Assert.Equal(workOrderId, booking.WorkOrderId);
    }

    [Fact]
    public void Cancel_WhenActive_ShouldSetStatusCancelledAndRecordReason()
    {
        var booking = Booking.Create(
            _tenantId,
            "Ana",
            "11977773333",
            "MER1234",
            null,
            VehicleSize.HatchSedan,
            _serviceId,
            "Lavagem",
            60m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(11, 0),
            60,
            BookingOrigin.WhatsAppBot).Value!;

        var result = booking.Cancel("Cliente desistiu por imprevisto");

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal("Cliente desistiu por imprevisto", booking.CancellationReason);
    }

    [Fact]
    public void Confirm_ShouldSetConfirmedAtUtc()
    {
        var booking = Booking.Create(
            _tenantId,
            "Lucas",
            "11966665555",
            "ABC-1234",
            null,
            VehicleSize.HatchSedan,
            _serviceId,
            "Lavagem",
            50m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(10, 0),
            60,
            BookingOrigin.WhatsAppBot).Value!;

        var result = booking.Confirm();

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.NotNull(booking.ConfirmedAtUtc);
    }

    [Fact]
    public void Reschedule_WithValidNewDateTime_ShouldUpdateScheduledDateAndTimeAndResetReminders()
    {
        var booking = Booking.Create(
            _tenantId,
            "Lucas",
            "11966665555",
            "ABC-1234",
            null,
            VehicleSize.HatchSedan,
            _serviceId,
            "Lavagem",
            50m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(10, 0),
            60,
            BookingOrigin.WhatsAppBot).Value!;

        booking.MarkReminder24hSent(DateTimeOffset.UtcNow.AddHours(-1));
        booking.MarkReminder2hSent(DateTimeOffset.UtcNow.AddMinutes(-30));
        Assert.NotNull(booking.Reminder24hSentAt);
        Assert.NotNull(booking.Reminder2hSentAt);

        var newDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3));
        var newTime = new TimeOnly(15, 30);

        var result = booking.Reschedule(newDate, newTime);

        Assert.True(result.IsSuccess);
        Assert.Equal(newDate, booking.ScheduledDate);
        Assert.Equal(newTime, booking.ScheduledTime);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Null(booking.Reminder24hSentAt);
        Assert.Null(booking.Reminder2hSentAt);
    }

    [Fact]
    public void Reschedule_WhenCancelled_ShouldFail()
    {
        var booking = Booking.Create(
            _tenantId,
            "Lucas",
            "11966665555",
            "ABC-1234",
            null,
            VehicleSize.HatchSedan,
            _serviceId,
            "Lavagem",
            50m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(10, 0),
            60,
            BookingOrigin.WhatsAppBot).Value!;

        booking.Cancel("Desistiu");

        var result = booking.Reschedule(DateOnly.FromDateTime(DateTime.Today.AddDays(2)), new TimeOnly(11, 0));

        Assert.False(result.IsSuccess);
        Assert.Equal("booking.cancelled", result.Error!.Code);
    }

    [Fact]
    public void Reschedule_WhenArrivedOrCompleted_ShouldFail()
    {
        var booking = Booking.Create(
            _tenantId,
            "Lucas",
            "11966665555",
            "ABC-1234",
            null,
            VehicleSize.HatchSedan,
            _serviceId,
            "Lavagem",
            50m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(10, 0),
            60,
            BookingOrigin.WhatsAppBot).Value!;

        booking.MarkAsArrived(Guid.NewGuid());

        var result = booking.Reschedule(DateOnly.FromDateTime(DateTime.Today.AddDays(2)), new TimeOnly(11, 0));

        Assert.False(result.IsSuccess);
        Assert.Equal("booking.invalid_status", result.Error!.Code);
    }

    [Fact]
    public void MarkReminderSent_ShouldSetCorrespondingTimestamps()
    {
        var booking = Booking.Create(
            _tenantId,
            "Lucas",
            "11966665555",
            "ABC-1234",
            null,
            VehicleSize.HatchSedan,
            _serviceId,
            "Lavagem",
            50m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(10, 0),
            60,
            BookingOrigin.WhatsAppBot).Value!;

        Assert.Null(booking.Reminder24hSentAt);
        Assert.Null(booking.Reminder2hSentAt);

        var now = DateTimeOffset.UtcNow;
        booking.MarkReminder24hSent(now);
        Assert.Equal(now, booking.Reminder24hSentAt);

        var later = now.AddHours(22);
        booking.MarkReminder2hSent(later);
        Assert.Equal(later, booking.Reminder2hSentAt);
    }
}
