using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed class PlatformYardMetricsService(
    IWorkOrderRepository workOrderRepository) : IPlatformYardMetricsLookup
{
    public async Task<PlatformYardOperationalMetricsDto> GetYardMetricsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Guid? tenantId = null,
        CancellationToken ct = default)
    {
        var workOrders = await workOrderRepository.ListForPlatformMetricsAsync(fromUtc, toUtc, tenantId, ct);

        var attendedOrders = workOrders
            .Where(w => w.PickedUpAtUtc.HasValue &&
                        w.PickedUpAtUtc.Value >= fromUtc &&
                        w.PickedUpAtUtc.Value <= toUtc)
            .ToList();

        var totalAttended = attendedOrders.Count;

        var inProgress = workOrders.Count(w =>
            !w.PickedUpAtUtc.HasValue &&
            w.Status != WorkOrderStatus.ReadyForPickup);

        var days = Math.Max(1, (int)Math.Ceiling((toUtc.Date - fromUtc.Date).TotalDays) + 1);
        var averageDaily = Math.Round((decimal)totalAttended / days, 1);

        var dailyVolume = attendedOrders
            .GroupBy(w => DateOnly.FromDateTime(w.PickedUpAtUtc!.Value.UtcDateTime))
            .OrderBy(g => g.Key)
            .Select(g => new DailyVehicleVolumeDto(
                Date: g.Key,
                VehiclesAttendedCount: g.Count()))
            .ToList();

        return new PlatformYardOperationalMetricsDto(
            TotalAttendedVehicles: totalAttended,
            InProgressWorkOrders: inProgress,
            AverageDailyAttendedVehicles: averageDaily,
            DailyVolume: dailyVolume);
    }
}
