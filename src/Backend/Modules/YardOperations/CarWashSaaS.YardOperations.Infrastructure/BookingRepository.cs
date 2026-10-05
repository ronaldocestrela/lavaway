using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class BookingRepository(YardOperationsDbContext dbContext) : IBookingRepository
{
    public async Task<Booking?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.Bookings
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<Booking?> GetByProtocolAsync(string protocol, CancellationToken ct = default)
    {
        var normalized = protocol.Trim();
        return await dbContext.Bookings
            .FirstOrDefaultAsync(b => b.Protocol == normalized, ct);
    }

    public async Task<IReadOnlyList<Booking>> GetByDateAsync(Guid tenantId, DateOnly date, CancellationToken ct = default)
    {
        return await dbContext.Bookings
            .Where(b => b.TenantId == tenantId && b.ScheduledDate == date)
            .OrderBy(b => b.ScheduledTime)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Booking>> GetActiveBookingsByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default)
    {
        var normalizedPhone = new string(customerPhone.Where(char.IsDigit).ToArray());
        return await dbContext.Bookings
            .Where(b => b.TenantId == tenantId &&
                        b.CustomerPhone == normalizedPhone &&
                        (b.Status == BookingStatus.Scheduled || b.Status == BookingStatus.Confirmed))
            .OrderByDescending(b => b.ScheduledDate)
            .ThenBy(b => b.ScheduledTime)
            .ToListAsync(ct);
    }

    public async Task<int> CountActiveBookingsInSlotAsync(
        Guid tenantId,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        CancellationToken ct = default)
    {
        return await dbContext.Bookings
            .CountAsync(b => b.TenantId == tenantId &&
                             b.ScheduledDate == date &&
                             (b.Status == BookingStatus.Scheduled ||
                              b.Status == BookingStatus.Confirmed ||
                              b.Status == BookingStatus.Arrived) &&
                             b.ScheduledTime >= startTime &&
                             b.ScheduledTime < endTime, ct);
    }

    public async Task AddAsync(Booking booking, CancellationToken ct = default)
    {
        await dbContext.Bookings.AddAsync(booking, ct);
    }

    public void Update(Booking booking)
    {
        dbContext.Bookings.Update(booking);
    }

    public async Task<IReadOnlyList<Booking>> GetBookingsPending24hReminderAsync(
        Guid tenantId,
        DateTimeOffset referenceTime,
        CancellationToken ct = default)
    {
        var targetStart = referenceTime.AddHours(23);
        var targetEnd = referenceTime.AddHours(25);
        var minDate = DateOnly.FromDateTime(targetStart.LocalDateTime);
        var maxDate = DateOnly.FromDateTime(targetEnd.LocalDateTime);

        var candidates = await dbContext.Bookings
            .Where(b => b.TenantId == tenantId &&
                        (b.Status == BookingStatus.Scheduled || b.Status == BookingStatus.Confirmed) &&
                        b.Reminder24hSentAt == null &&
                        b.ScheduledDate >= minDate &&
                        b.ScheduledDate <= maxDate)
            .ToListAsync(ct);

        return candidates
            .Where(b =>
            {
                var dt = b.ScheduledDate.ToDateTime(b.ScheduledTime);
                return dt >= targetStart.LocalDateTime && dt <= targetEnd.LocalDateTime;
            })
            .OrderBy(b => b.ScheduledDate)
            .ThenBy(b => b.ScheduledTime)
            .ToList();
    }

    public async Task<IReadOnlyList<Booking>> GetBookingsPending2hReminderAsync(
        Guid tenantId,
        DateTimeOffset referenceTime,
        CancellationToken ct = default)
    {
        var targetStart = referenceTime.AddMinutes(105);
        var targetEnd = referenceTime.AddMinutes(135);
        var minDate = DateOnly.FromDateTime(targetStart.LocalDateTime);
        var maxDate = DateOnly.FromDateTime(targetEnd.LocalDateTime);

        var candidates = await dbContext.Bookings
            .Where(b => b.TenantId == tenantId &&
                        (b.Status == BookingStatus.Scheduled || b.Status == BookingStatus.Confirmed) &&
                        b.Reminder2hSentAt == null &&
                        b.ScheduledDate >= minDate &&
                        b.ScheduledDate <= maxDate)
            .ToListAsync(ct);

        return candidates
            .Where(b =>
            {
                var dt = b.ScheduledDate.ToDateTime(b.ScheduledTime);
                return dt >= targetStart.LocalDateTime && dt <= targetEnd.LocalDateTime;
            })
            .OrderBy(b => b.ScheduledDate)
            .ThenBy(b => b.ScheduledTime)
            .ToList();
    }

    public async Task<Booking?> GetUpcomingActiveBookingByPhoneAsync(
        Guid tenantId,
        string customerPhone,
        CancellationToken ct = default)
    {
        var normalizedPhone = new string(customerPhone.Where(char.IsDigit).ToArray());
        var today = DateOnly.FromDateTime(DateTime.Today);

        var activeBookings = await dbContext.Bookings
            .Where(b => b.TenantId == tenantId &&
                        b.CustomerPhone == normalizedPhone &&
                        (b.Status == BookingStatus.Scheduled || b.Status == BookingStatus.Confirmed) &&
                        b.ScheduledDate >= today)
            .OrderBy(b => b.ScheduledDate)
            .ThenBy(b => b.ScheduledTime)
            .ToListAsync(ct);

        return activeBookings.FirstOrDefault();
    }
}
