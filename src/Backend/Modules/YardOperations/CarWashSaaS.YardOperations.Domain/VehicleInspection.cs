using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class VehicleInspection : IMustHaveTenant
{
    private readonly List<InspectionDamage> _damages = [];
    private readonly List<InspectionChecklistItem> _checklistItems = [];
    private readonly List<InspectionPhoto> _photos = [];

    private VehicleInspection()
    {
    }

    private VehicleInspection(
        Guid id,
        Guid tenantId,
        Guid workOrderId,
        Guid vehicleId,
        string? fuelLevel = null,
        int? odometerKm = null,
        string? notes = null)
    {
        Id = id;
        TenantId = tenantId;
        WorkOrderId = workOrderId;
        VehicleId = vehicleId;
        FuelLevel = string.IsNullOrWhiteSpace(fuelLevel) ? null : fuelLevel.Trim();
        OdometerKm = odometerKm;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Status = InspectionStatus.Draft;
        CreatedAtUtc = DateTimeOffset.UtcNow;

        InitializeDefaultChecklist();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public Guid WorkOrderId { get; private set; }
    public Guid VehicleId { get; private set; }
    public string? FuelLevel { get; private set; }
    public int? OdometerKm { get; private set; }
    public string? Notes { get; private set; }
    public InspectionStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public IReadOnlyCollection<InspectionDamage> Damages => _damages.AsReadOnly();
    public IReadOnlyCollection<InspectionChecklistItem> ChecklistItems => _checklistItems.AsReadOnly();
    public IReadOnlyCollection<InspectionPhoto> Photos => _photos.AsReadOnly();

    public static Result<VehicleInspection> Create(
        Guid tenantId,
        Guid workOrderId,
        Guid vehicleId,
        string? fuelLevel = null,
        int? odometerKm = null,
        string? notes = null)
    {
        if (tenantId == Guid.Empty || workOrderId == Guid.Empty || vehicleId == Guid.Empty)
        {
            return Result<VehicleInspection>.Failure(new Error("inspection.owner.required", "Tenant, Ordem de Serviço e Veículo são obrigatórios.", ErrorType.Validation));
        }

        if (odometerKm.HasValue && odometerKm.Value < 0)
        {
            return Result<VehicleInspection>.Failure(new Error("inspection.odometer.invalid", "Odômetro não pode ser negativo.", ErrorType.Validation));
        }

        if (notes is not null && notes.Trim().Length > 500)
        {
            return Result<VehicleInspection>.Failure(new Error("inspection.notes.too_long", "Observações não devem exceder 500 caracteres.", ErrorType.Validation));
        }

        return Result<VehicleInspection>.Success(new VehicleInspection(
            Guid.CreateVersion7(),
            tenantId,
            workOrderId,
            vehicleId,
            fuelLevel,
            odometerKm,
            notes));
    }

    public Result<InspectionDamage> AddDamage(
        DamageType type,
        VehicleView view,
        decimal coordinateX,
        decimal coordinateY,
        DamageSeverity severity,
        string? description = null)
    {
        if (Status == InspectionStatus.Completed)
        {
            return Result<InspectionDamage>.Failure(new Error("inspection.already_completed", "Vistoria já concluída não pode ser alterada.", ErrorType.Conflict));
        }

        var damageResult = InspectionDamage.Create(TenantId, Id, type, view, coordinateX, coordinateY, severity, description);
        if (!damageResult.IsSuccess)
        {
            return damageResult;
        }

        _damages.Add(damageResult.Value!);
        return damageResult;
    }

    public Result RemoveDamage(Guid damageId)
    {
        if (Status == InspectionStatus.Completed)
        {
            return Result.Failure(new Error("inspection.already_completed", "Vistoria já concluída não pode ser alterada.", ErrorType.Conflict));
        }

        var damage = _damages.FirstOrDefault(d => d.Id == damageId);
        if (damage is null)
        {
            return Result.Failure(new Error("inspection.damage.not_found", "Avaria não encontrada.", ErrorType.NotFound));
        }

        _damages.Remove(damage);
        return Result.Success();
    }

    public Result<InspectionPhoto> AddPhoto(
        InspectionPhotoCategory category,
        string storagePath,
        string fileName,
        string contentType,
        long sizeBytes,
        Guid? damageId = null)
    {
        if (Status == InspectionStatus.Completed)
        {
            return Result<InspectionPhoto>.Failure(new Error("inspection.already_completed", "Vistoria já concluída não pode receber novas fotos.", ErrorType.Conflict));
        }

        var photoResult = InspectionPhoto.Create(TenantId, Id, category, storagePath, fileName, contentType, sizeBytes, damageId);
        if (!photoResult.IsSuccess)
        {
            return photoResult;
        }

        _photos.Add(photoResult.Value!);
        return photoResult;
    }

    public Result RemovePhoto(Guid photoId)
    {
        if (Status == InspectionStatus.Completed)
        {
            return Result.Failure(new Error("inspection.already_completed", "Vistoria já concluída não permite remover fotos.", ErrorType.Conflict));
        }

        var photo = _photos.FirstOrDefault(p => p.Id == photoId);
        if (photo is null)
        {
            return Result.Failure(new Error("inspection.photo.not_found", "Foto não encontrada.", ErrorType.NotFound));
        }

        _photos.Remove(photo);
        return Result.Success();
    }

    public Result UpdateChecklist(
        string? fuelLevel,
        int? odometerKm,
        string? notes,
        IEnumerable<UpdateChecklistItemRequest> items)
    {
        if (Status == InspectionStatus.Completed)
        {
            return Result.Failure(new Error("inspection.already_completed", "Vistoria já concluída não pode ser alterada.", ErrorType.Conflict));
        }

        if (odometerKm.HasValue && odometerKm.Value < 0)
        {
            return Result.Failure(new Error("inspection.odometer.invalid", "Odômetro não pode ser negativo.", ErrorType.Validation));
        }

        if (notes is not null && notes.Trim().Length > 500)
        {
            return Result.Failure(new Error("inspection.notes.too_long", "Observações não devem exceder 500 caracteres.", ErrorType.Validation));
        }

        FuelLevel = string.IsNullOrWhiteSpace(fuelLevel) ? null : fuelLevel.Trim();
        OdometerKm = odometerKm;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        foreach (var req in items)
        {
            var existing = _checklistItems.FirstOrDefault(i => i.ItemKey == req.ItemKey.Trim().ToLowerInvariant());
            if (existing is not null)
            {
                existing.Update(req.Status, req.Observation);
            }
            else
            {
                var created = InspectionChecklistItem.Create(TenantId, Id, req.ItemKey, req.Title, req.Status, req.Observation);
                if (created.IsSuccess)
                {
                    _checklistItems.Add(created.Value!);
                }
            }
        }

        return Result.Success();
    }

    public Result Complete()
    {
        if (Status == InspectionStatus.Completed)
        {
            return Result.Success();
        }

        var hasFront = _photos.Any(p => p.Category == InspectionPhotoCategory.Front);
        var hasRear = _photos.Any(p => p.Category == InspectionPhotoCategory.Rear);
        var hasLeft = _photos.Any(p => p.Category == InspectionPhotoCategory.LeftSide);
        var hasRight = _photos.Any(p => p.Category == InspectionPhotoCategory.RightSide);

        if (!hasFront || !hasRear || !hasLeft || !hasRight)
        {
            return Result.Failure(new Error(
                "inspection.required_photos_missing",
                "As 4 fotos de perímetro (Frente, Traseira, Lateral Esquerda e Lateral Direita) são obrigatórias para concluir a vistoria.",
                ErrorType.Validation));
        }

        Status = InspectionStatus.Completed;
        CompletedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    private void InitializeDefaultChecklist()
    {
        _checklistItems.Add(new InspectionChecklistItem(Guid.CreateVersion7(), TenantId, Id, "spare_tire", "Estepe", ChecklistItemStatus.Ok));
        _checklistItems.Add(new InspectionChecklistItem(Guid.CreateVersion7(), TenantId, Id, "wheel_wrench", "Chave de Roda e Macaco", ChecklistItemStatus.Ok));
        _checklistItems.Add(new InspectionChecklistItem(Guid.CreateVersion7(), TenantId, Id, "interior_belongings", "Pertences no Interior", ChecklistItemStatus.Ok));
        _checklistItems.Add(new InspectionChecklistItem(Guid.CreateVersion7(), TenantId, Id, "floor_mats", "Jogo de Tapetes", ChecklistItemStatus.Ok));
        _checklistItems.Add(new InspectionChecklistItem(Guid.CreateVersion7(), TenantId, Id, "dashboard_lights", "Luzes de Alerta no Painel", ChecklistItemStatus.Ok));
    }
}
