namespace CarWashSaaS.Shared.Contracts;

public sealed record InspectionDamageDto(
    Guid Id,
    DamageType Type,
    VehicleView View,
    decimal CoordinateX,
    decimal CoordinateY,
    DamageSeverity Severity,
    string? Description);
