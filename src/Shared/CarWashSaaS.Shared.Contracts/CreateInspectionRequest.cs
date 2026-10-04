namespace CarWashSaaS.Shared.Contracts;

public sealed record CreateInspectionRequest(
    string? FuelLevel = null,
    int? OdometerKm = null,
    string? Notes = null);
