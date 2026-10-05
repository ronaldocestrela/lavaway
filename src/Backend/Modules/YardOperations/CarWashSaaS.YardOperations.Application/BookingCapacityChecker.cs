using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Application;

public sealed class BookingCapacityChecker(
    IYardCapacityRepository capacityRepository,
    IBookingRepository bookingRepository)
{
    private const int DefaultTotalBoxes = 2;
    private static readonly TimeOnly[] StandardOperatingHours =
    [
        new(8, 0),
        new(9, 0),
        new(10, 0),
        new(11, 0),
        new(13, 0),
        new(14, 0),
        new(15, 0),
        new(16, 0),
        new(17, 0)
    ];

    public async Task<int> GetTotalBoxesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var capacity = await capacityRepository.GetByTenantAsync(tenantId, ct);
        return capacity is not null && capacity.TotalBoxes > 0 ? capacity.TotalBoxes : DefaultTotalBoxes;
    }

    public async Task<IReadOnlyList<AvailableTimeSlotDto>> GetAvailableSlotsAsync(
        Guid tenantId,
        DateOnly date,
        int durationMinutes,
        CancellationToken ct = default)
    {
        var totalBoxes = await GetTotalBoxesAsync(tenantId, ct);
        var slots = new List<AvailableTimeSlotDto>();
        var now = DateTimeOffset.Now;
        var today = DateOnly.FromDateTime(now.Date);
        var currentTime = TimeOnly.FromTimeSpan(now.TimeOfDay);

        foreach (var start in StandardOperatingHours)
        {
            if (date == today && start <= currentTime.AddMinutes(15))
            {
                continue;
            }

            var end = start.AddMinutes(durationMinutes <= 0 ? 60 : durationMinutes);
            var activeCount = await bookingRepository.CountActiveBookingsInSlotAsync(tenantId, date, start, end, ct);
            var available = Math.Max(0, totalBoxes - activeCount);

            if (available > 0)
            {
                slots.Add(new AvailableTimeSlotDto(start, end, available, totalBoxes));
            }
        }

        return slots;
    }

    public async Task<bool> HasCapacityAsync(
        Guid tenantId,
        DateOnly date,
        TimeOnly startTime,
        int durationMinutes,
        CancellationToken ct = default)
    {
        var totalBoxes = await GetTotalBoxesAsync(tenantId, ct);
        var endTime = startTime.AddMinutes(durationMinutes <= 0 ? 60 : durationMinutes);
        var activeCount = await bookingRepository.CountActiveBookingsInSlotAsync(tenantId, date, startTime, endTime, ct);
        return activeCount < totalBoxes;
    }
}
