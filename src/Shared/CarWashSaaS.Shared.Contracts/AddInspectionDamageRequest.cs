namespace CarWashSaaS.Shared.Contracts;

public sealed record AddInspectionDamageRequest(
    DamageType Type,
    VehicleView View,
    decimal CoordinateX,
    decimal CoordinateY,
    DamageSeverity Severity = DamageSeverity.Low,
    string? Description = null);
