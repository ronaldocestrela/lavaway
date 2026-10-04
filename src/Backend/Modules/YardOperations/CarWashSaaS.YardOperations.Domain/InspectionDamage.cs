using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class InspectionDamage : IMustHaveTenant
{
    private InspectionDamage()
    {
    }

    internal InspectionDamage(
        Guid id,
        Guid tenantId,
        Guid vehicleInspectionId,
        DamageType type,
        VehicleView view,
        decimal coordinateX,
        decimal coordinateY,
        DamageSeverity severity,
        string? description = null)
    {
        Id = id;
        TenantId = tenantId;
        VehicleInspectionId = vehicleInspectionId;
        Type = type;
        View = view;
        CoordinateX = coordinateX;
        CoordinateY = coordinateY;
        Severity = severity;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public Guid VehicleInspectionId { get; private set; }
    public DamageType Type { get; private set; }
    public VehicleView View { get; private set; }
    public decimal CoordinateX { get; private set; }
    public decimal CoordinateY { get; private set; }
    public DamageSeverity Severity { get; private set; }
    public string? Description { get; private set; }

    public static Result<InspectionDamage> Create(
        Guid tenantId,
        Guid vehicleInspectionId,
        DamageType type,
        VehicleView view,
        decimal coordinateX,
        decimal coordinateY,
        DamageSeverity severity,
        string? description = null)
    {
        if (tenantId == Guid.Empty || vehicleInspectionId == Guid.Empty)
        {
            return Result<InspectionDamage>.Failure(new Error("inspection.owner.required", "Tenant e Vistoria são obrigatórios.", ErrorType.Validation));
        }

        if (coordinateX < 0m || coordinateX > 100m || coordinateY < 0m || coordinateY > 100m)
        {
            return Result<InspectionDamage>.Failure(new Error("inspection.coordinates.invalid", "Coordenadas devem estar no intervalo de 0.00 a 100.00%.", ErrorType.Validation));
        }

        if (description is not null && description.Trim().Length > 300)
        {
            return Result<InspectionDamage>.Failure(new Error("inspection.description.too_long", "A descrição não deve exceder 300 caracteres.", ErrorType.Validation));
        }

        return Result<InspectionDamage>.Success(new InspectionDamage(
            Guid.CreateVersion7(),
            tenantId,
            vehicleInspectionId,
            type,
            view,
            coordinateX,
            coordinateY,
            severity,
            description));
    }
}
