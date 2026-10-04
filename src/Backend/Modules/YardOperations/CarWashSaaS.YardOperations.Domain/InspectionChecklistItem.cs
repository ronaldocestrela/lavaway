using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class InspectionChecklistItem : IMustHaveTenant
{
    private InspectionChecklistItem()
    {
    }

    internal InspectionChecklistItem(
        Guid id,
        Guid tenantId,
        Guid vehicleInspectionId,
        string itemKey,
        string title,
        ChecklistItemStatus status,
        string? observation = null)
    {
        Id = id;
        TenantId = tenantId;
        VehicleInspectionId = vehicleInspectionId;
        ItemKey = itemKey.Trim().ToLowerInvariant();
        Title = title.Trim();
        Status = status;
        Observation = string.IsNullOrWhiteSpace(observation) ? null : observation.Trim();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public Guid VehicleInspectionId { get; private set; }
    public string ItemKey { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public ChecklistItemStatus Status { get; private set; }
    public string? Observation { get; private set; }

    public static Result<InspectionChecklistItem> Create(
        Guid tenantId,
        Guid vehicleInspectionId,
        string itemKey,
        string title,
        ChecklistItemStatus status = ChecklistItemStatus.Ok,
        string? observation = null)
    {
        if (tenantId == Guid.Empty || vehicleInspectionId == Guid.Empty)
        {
            return Result<InspectionChecklistItem>.Failure(new Error("inspection.owner.required", "Tenant e Vistoria são obrigatórios.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(itemKey))
        {
            return Result<InspectionChecklistItem>.Failure(new Error("checklist.key.required", "A chave do item é obrigatória.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return Result<InspectionChecklistItem>.Failure(new Error("checklist.title.required", "O título do item é obrigatório.", ErrorType.Validation));
        }

        return Result<InspectionChecklistItem>.Success(new InspectionChecklistItem(
            Guid.CreateVersion7(),
            tenantId,
            vehicleInspectionId,
            itemKey,
            title,
            status,
            observation));
    }

    public void Update(ChecklistItemStatus status, string? observation)
    {
        Status = status;
        Observation = string.IsNullOrWhiteSpace(observation) ? null : observation.Trim();
    }
}
