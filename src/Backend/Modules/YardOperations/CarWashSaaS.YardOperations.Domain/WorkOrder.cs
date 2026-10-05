using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class WorkOrder : IMustHaveTenant
{
    private readonly List<WorkOrderItem> _items = [];
    private readonly List<WorkOrderStatusHistory> _statusHistory = [];
    private readonly List<PostServicePhoto> _postServicePhotos = [];

    private WorkOrder()
    {
    }

    private WorkOrder(Guid id, Guid tenantId, Guid customerId, Guid vehicleId, IEnumerable<WorkOrderItem> items, string? notes = null)
    {
        Id = id;
        TenantId = tenantId;
        CustomerId = customerId;
        VehicleId = vehicleId;
        Status = WorkOrderStatus.Waiting;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        _items.AddRange(items);

        _statusHistory.Add(new WorkOrderStatusHistory(
            Guid.CreateVersion7(),
            tenantId,
            id,
            null,
            WorkOrderStatus.Waiting,
            CreatedAtUtc,
            null,
            null,
            "Check-in realizado"));
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public WorkOrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string? Notes { get; private set; }
    public Guid? AssignedOperatorId { get; private set; }
    public string? AssignedOperatorName { get; private set; }
    public DateTimeOffset? PickedUpAtUtc { get; private set; }
    public DateTimeOffset? SurveySentAtUtc { get; private set; }
    public int? SurveyRating { get; private set; }
    public string? SurveyFeedback { get; private set; }
    public DateTimeOffset? SurveyRespondedAtUtc { get; private set; }

    public IReadOnlyCollection<WorkOrderItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<WorkOrderStatusHistory> StatusHistory => _statusHistory.AsReadOnly();
    public IReadOnlyCollection<PostServicePhoto> PostServicePhotos => _postServicePhotos.AsReadOnly();

    public decimal TotalAmount => _items.Sum(item => item.TotalAmount);
    public int EstimatedDurationMinutes => _items.Sum(item => item.TotalDurationMinutes);
    public DateTimeOffset EstimatedCompletionAtUtc => CreatedAtUtc.AddMinutes(EstimatedDurationMinutes);

    public static Result<WorkOrder> Create(Guid tenantId, Guid customerId, Guid vehicleId, IEnumerable<WorkOrderItem>? items, string? notes = null)
    {
        if (tenantId == Guid.Empty || customerId == Guid.Empty || vehicleId == Guid.Empty)
        {
            return Result<WorkOrder>.Failure(new Error("work_order.owner.required", "Tenant, customer and vehicle are required.", ErrorType.Validation));
        }

        if (notes is not null && notes.Trim().Length > 500)
        {
            return Result<WorkOrder>.Failure(new Error("work_order.notes.invalid", "Notes must not exceed 500 characters.", ErrorType.Validation));
        }

        var normalizedItems = items?.ToArray() ?? [];
        if (normalizedItems.Length == 0)
        {
            return Result<WorkOrder>.Failure(new Error("work_order.items.required", "At least one service item is required.", ErrorType.Validation));
        }

        if (normalizedItems.Any(item => item.TenantId != tenantId))
        {
            return Result<WorkOrder>.Failure(new Error("work_order_item.tenant_mismatch", "All work-order items must belong to the work-order tenant.", ErrorType.Conflict));
        }

        if (normalizedItems.Select(item => item.ServiceId).Distinct().Count() != normalizedItems.Length)
        {
            return Result<WorkOrder>.Failure(new Error("work_order.duplicate_service", "Duplicate services are not allowed in the same work order.", ErrorType.Conflict));
        }

        return Result<WorkOrder>.Success(new WorkOrder(Guid.CreateVersion7(), tenantId, customerId, vehicleId, normalizedItems, notes));
    }

    public Result<WorkOrder> AssignOperator(Guid? operatorId, string? operatorName)
    {
        if (operatorId.HasValue && operatorId.Value == Guid.Empty)
        {
            return Result<WorkOrder>.Failure(new Error("work_order.operator.invalid", "Identificador de operador inválido.", ErrorType.Validation));
        }

        AssignedOperatorId = operatorId;
        AssignedOperatorName = string.IsNullOrWhiteSpace(operatorName) ? null : operatorName.Trim();
        return Result<WorkOrder>.Success(this);
    }

    public Result<WorkOrder> ChangeStatus(
        WorkOrderStatus targetStatus,
        Guid? operatorId = null,
        string? operatorName = null,
        string? notes = null)
    {
        if (Status == WorkOrderStatus.ReadyForPickup)
        {
            return Result<WorkOrder>.Failure(new Error(
                "work_order.already_ready_for_pickup",
                "A ordem de serviço já está pronta para retirada e não permite alteração de status.",
                ErrorType.Conflict));
        }

        if (targetStatus == Status)
        {
            return Result<WorkOrder>.Failure(new Error(
                "work_order.same_status",
                $"A ordem de serviço já se encontra no status '{Status}'.",
                ErrorType.Validation));
        }

        var isRework = (Status == WorkOrderStatus.QualityControl && (targetStatus == WorkOrderStatus.Finishing || targetStatus == WorkOrderStatus.InWashing))
            || (Status == WorkOrderStatus.Finishing && targetStatus == WorkOrderStatus.InWashing);

        if (isRework && string.IsNullOrWhiteSpace(notes))
        {
            return Result<WorkOrder>.Failure(new Error(
                "work_order.rework_notes_required",
                "É obrigatório registrar o motivo para retornar a etapa da ordem de serviço.",
                ErrorType.Validation));
        }

        var isValidTransition = (Status, targetStatus) switch
        {
            (WorkOrderStatus.Waiting, WorkOrderStatus.InWashing) => true,
            (WorkOrderStatus.InWashing, WorkOrderStatus.Finishing) => true,
            (WorkOrderStatus.Finishing, WorkOrderStatus.QualityControl) => true,
            (WorkOrderStatus.Finishing, WorkOrderStatus.InWashing) => true,
            (WorkOrderStatus.QualityControl, WorkOrderStatus.ReadyForPickup) => true,
            (WorkOrderStatus.QualityControl, WorkOrderStatus.Finishing) => true,
            (WorkOrderStatus.QualityControl, WorkOrderStatus.InWashing) => true,
            _ => false
        };

        if (!isValidTransition)
        {
            return Result<WorkOrder>.Failure(new Error(
                "work_order.invalid_status_transition",
                $"Transição não permitida do status '{Status}' para '{targetStatus}'.",
                ErrorType.Validation));
        }

        var oldStatus = Status;
        Status = targetStatus;

        if (operatorId.HasValue)
        {
            AssignedOperatorId = operatorId;
            AssignedOperatorName = string.IsNullOrWhiteSpace(operatorName) ? AssignedOperatorName : operatorName.Trim();
        }

        _statusHistory.Add(new WorkOrderStatusHistory(
            Guid.CreateVersion7(),
            TenantId,
            Id,
            oldStatus,
            targetStatus,
            DateTimeOffset.UtcNow,
            operatorId ?? AssignedOperatorId,
            operatorName ?? AssignedOperatorName,
            notes));

        return Result<WorkOrder>.Success(this);
    }

    public bool IsEligibleForPostServicePhotos() =>
        _items.Any(PostServiceEligibilityRule.IsEligible);

    public Result<PostServicePhoto> AddPostServicePhoto(
        Guid? workOrderItemId,
        Guid? beforeInspectionPhotoId,
        InspectionPhotoCategory category,
        string title,
        string storagePath,
        string fileName,
        string contentType,
        long sizeBytes,
        string? notes = null)
    {
        if (Status == WorkOrderStatus.Waiting)
        {
            return Result<PostServicePhoto>.Failure(new Error(
                "work_order.invalid_status_for_post_service_photos",
                "Fotos pós-serviço não podem ser registradas enquanto a ordem de serviço estiver aguardando início.",
                ErrorType.Conflict));
        }

        if (!IsEligibleForPostServicePhotos())
        {
            return Result<PostServicePhoto>.Failure(new Error(
                "work_order.no_eligible_services",
                "A ordem de serviço não possui serviços elegíveis para fotos pós-serviço (detalhamento, vitrificação ou bancos).",
                ErrorType.Validation));
        }

        if (workOrderItemId.HasValue && workOrderItemId.Value != Guid.Empty)
        {
            var item = _items.FirstOrDefault(i => i.Id == workOrderItemId.Value);
            if (item is null)
            {
                return Result<PostServicePhoto>.Failure(new Error(
                    "work_order.item_not_found",
                    "O item de serviço especificado não pertence a esta ordem de serviço.",
                    ErrorType.NotFound));
            }

            if (!PostServiceEligibilityRule.IsEligible(item))
            {
                return Result<PostServicePhoto>.Failure(new Error(
                    "work_order.item_not_eligible",
                    $"O serviço '{item.ServiceName}' não é elegível para fotos pós-serviço.",
                    ErrorType.Validation));
            }
        }

        var photoResult = PostServicePhoto.Create(
            TenantId,
            Id,
            workOrderItemId,
            beforeInspectionPhotoId,
            category,
            title,
            storagePath,
            fileName,
            contentType,
            sizeBytes,
            notes);

        if (!photoResult.IsSuccess)
        {
            return photoResult;
        }

        _postServicePhotos.Add(photoResult.Value!);
        return photoResult;
    }

    public Result RemovePostServicePhoto(Guid photoId)
    {
        var photo = _postServicePhotos.FirstOrDefault(p => p.Id == photoId);
        if (photo is null)
        {
            return Result.Failure(new Error("post_service_photo.not_found", "Foto pós-serviço não encontrada.", ErrorType.NotFound));
        }

        _postServicePhotos.Remove(photo);
        return Result.Success();
    }

    public Result<WorkOrder> RegisterPickup(DateTimeOffset? pickedUpAtUtc = null, string? notes = null)
    {
        if (Status != WorkOrderStatus.ReadyForPickup)
        {
            return Result<WorkOrder>.Failure(new Error(
                "work_order.not_ready_for_pickup",
                "A retirada só pode ser registrada quando a ordem de serviço estiver pronta para retirada.",
                ErrorType.Conflict));
        }

        if (PickedUpAtUtc.HasValue)
        {
            return Result<WorkOrder>.Failure(new Error(
                "work_order.already_picked_up",
                "A retirada do veículo já foi registrada para esta ordem de serviço.",
                ErrorType.Conflict));
        }

        var timestamp = pickedUpAtUtc ?? DateTimeOffset.UtcNow;
        PickedUpAtUtc = timestamp;

        _statusHistory.Add(new WorkOrderStatusHistory(
            Guid.CreateVersion7(),
            TenantId,
            Id,
            WorkOrderStatus.ReadyForPickup,
            WorkOrderStatus.ReadyForPickup,
            timestamp,
            AssignedOperatorId,
            AssignedOperatorName,
            string.IsNullOrWhiteSpace(notes) ? "Veículo entregue ao cliente (Retirada registrada)" : notes.Trim()));

        return Result<WorkOrder>.Success(this);
    }

    public Result<WorkOrder> RecordSatisfactionSurveySent(DateTimeOffset sentAtUtc)
    {
        if (!PickedUpAtUtc.HasValue)
        {
            return Result<WorkOrder>.Failure(new Error(
                "work_order.survey_before_pickup",
                "A pesquisa de satisfação só pode ser enviada após a retirada do veículo.",
                ErrorType.Validation));
        }

        if (SurveySentAtUtc.HasValue)
        {
            return Result<WorkOrder>.Failure(new Error(
                "work_order.survey_already_sent",
                "A pesquisa de satisfação já foi enviada para esta ordem de serviço.",
                ErrorType.Conflict));
        }

        SurveySentAtUtc = sentAtUtc;
        return Result<WorkOrder>.Success(this);
    }

    public Result<WorkOrder> MarkSurveySkippedOptOut()
    {
        if (SurveySentAtUtc.HasValue)
        {
            return Result<WorkOrder>.Success(this);
        }

        SurveySentAtUtc = DateTimeOffset.UtcNow;
        SurveyFeedback = "[Ignorado: Cliente com Opt-out ativo]";
        return Result<WorkOrder>.Success(this);
    }

    public Result<WorkOrder> RecordSatisfactionRating(int rating, string? feedback = null, DateTimeOffset? respondedAtUtc = null)
    {
        if (rating < 1 || rating > 5)
        {
            return Result<WorkOrder>.Failure(new Error(
                "work_order.survey_rating_invalid",
                "A avaliação deve ser entre 1 e 5 estrelas.",
                ErrorType.Validation));
        }

        SurveyRating = rating;
        SurveyFeedback = string.IsNullOrWhiteSpace(feedback) ? SurveyFeedback : feedback.Trim();
        SurveyRespondedAtUtc = respondedAtUtc ?? DateTimeOffset.UtcNow;
        return Result<WorkOrder>.Success(this);
    }
}
