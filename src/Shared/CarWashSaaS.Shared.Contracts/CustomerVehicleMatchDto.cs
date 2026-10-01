namespace CarWashSaaS.Shared.Contracts;

/// <summary>Customer details and matching vehicles returned by reception searches.</summary>
public sealed record CustomerVehicleMatchDto(
    Guid CustomerId,
    string CustomerName,
    string Phone,
    IReadOnlyCollection<VehicleSummaryDto> Vehicles);