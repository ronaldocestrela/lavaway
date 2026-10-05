using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed class WorkOrderApplicationService(
    IWorkOrderRepository workOrderRepository,
    ICustomerRepository customerRepository,
    IVehicleRepository vehicleRepository,
    IServiceRepository serviceRepository,
    IUnitOfWork unitOfWork,
    ITeamMemberRepository? teamMemberRepository = null,
    IYardCapacityRepository? yardCapacityRepository = null,
    IYardRealtimeNotifier? realtimeNotifier = null,
    ITenantObjectStorage? tenantObjectStorage = null,
    IVehicleInspectionRepository? vehicleInspectionRepository = null,
    IBackgroundQueue? backgroundQueue = null)
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private const long MaxPhotoSizeBytes = 10 * 1024 * 1024; // 10 MB

    public async Task<Result<WorkOrderDto>> CreateAsync(
        Guid tenantId,
        CreateWorkOrderCommand command,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<WorkOrderDto>.Failure(new Error("tenant.required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (command.CustomerId == Guid.Empty || command.VehicleId == Guid.Empty)
        {
            return Result<WorkOrderDto>.Failure(new Error("work_order.owner.required", "Cliente e veículo são obrigatórios.", ErrorType.Validation));
        }

        if (command.Items is null || command.Items.Count == 0)
        {
            return Result<WorkOrderDto>.Failure(new Error("work_order.items.required", "Pelo menos um serviço é obrigatório.", ErrorType.Validation));
        }

        if (command.Items.Any(i => i.ServiceId == Guid.Empty || i.Quantity <= 0))
        {
            return Result<WorkOrderDto>.Failure(new Error("work_order.item.invalid", "Serviço e quantidade válida são obrigatórios.", ErrorType.Validation));
        }

        if (command.Items.Select(i => i.ServiceId).Distinct().Count() != command.Items.Count)
        {
            return Result<WorkOrderDto>.Failure(new Error("work_order.duplicate_service", "Não é permitido adicionar serviços duplicados na mesma ordem de serviço.", ErrorType.Conflict));
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, command.CustomerId, ct);
        if (customer is null)
        {
            return Result<WorkOrderDto>.Failure(new Error("customer.not_found", "Cliente não encontrado.", ErrorType.NotFound));
        }

        var vehicle = await vehicleRepository.GetByIdAsync(tenantId, command.VehicleId, ct);
        if (vehicle is null)
        {
            return Result<WorkOrderDto>.Failure(new Error("vehicle.not_found", "Veículo não encontrado.", ErrorType.NotFound));
        }

        if (vehicle.CustomerId != customer.Id)
        {
            return Result<WorkOrderDto>.Failure(new Error("vehicle.not_owned_by_customer", "O veículo informado não pertence a este cliente.", ErrorType.Conflict));
        }

        var workOrderItems = new List<WorkOrderItem>();
        foreach (var itemInput in command.Items)
        {
            var service = await serviceRepository.GetByIdAsync(tenantId, itemInput.ServiceId, ct);
            if (service is null)
            {
                return Result<WorkOrderDto>.Failure(new Error("service.not_found", $"Serviço '{itemInput.ServiceId}' não encontrado.", ErrorType.NotFound));
            }

            var matchingPrice = service.Prices.FirstOrDefault(p => p.VehicleSize == vehicle.Size);
            if (matchingPrice is null)
            {
                return Result<WorkOrderDto>.Failure(new Error(
                    "service.price_not_configured_for_size",
                    $"O serviço '{service.Name}' não possui preço configurado para o porte {vehicle.Size}.",
                    ErrorType.Validation));
            }

            var itemResult = WorkOrderItem.Create(
                tenantId,
                service.Id,
                service.Name,
                matchingPrice.Amount,
                matchingPrice.EstimatedDurationMinutes,
                itemInput.Quantity,
                service.Category);

            if (!itemResult.IsSuccess)
            {
                return Result<WorkOrderDto>.Failure(itemResult.Error!);
            }

            workOrderItems.Add(itemResult.Value!);
        }

        var workOrderResult = WorkOrder.Create(
            tenantId,
            customer.Id,
            vehicle.Id,
            workOrderItems,
            command.Notes);

        if (!workOrderResult.IsSuccess)
        {
            return Result<WorkOrderDto>.Failure(workOrderResult.Error!);
        }

        var workOrder = workOrderResult.Value!;
        await workOrderRepository.AddAsync(workOrder, ct);
        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<WorkOrderDto>.Failure(saveResult.Error!);
        }

        return Result<WorkOrderDto>.Success(ToDto(workOrder, customer.Name, vehicle.Plate, vehicle.Size.ToString()));
    }

    public async Task<Result<WorkOrderDto>> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || id == Guid.Empty)
        {
            return Result<WorkOrderDto>.Failure(new Error("work_order.id.required", "Tenant e ID da ordem de serviço são obrigatórios.", ErrorType.Validation));
        }

        var workOrder = await workOrderRepository.GetByIdAsync(tenantId, id, ct);
        if (workOrder is null)
        {
            return Result<WorkOrderDto>.Failure(new Error("work_order.not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, workOrder.CustomerId, ct);
        var vehicle = await vehicleRepository.GetByIdAsync(tenantId, workOrder.VehicleId, ct);

        var customerName = customer?.Name ?? "Cliente";
        var plate = vehicle?.Plate ?? string.Empty;
        var size = vehicle?.Size.ToString() ?? "HatchSedan";

        return Result<WorkOrderDto>.Success(ToDto(workOrder, customerName, plate, size));
    }

    public async Task<Result<IReadOnlyCollection<WorkOrderDto>>> ListRecentAsync(Guid tenantId, int limit = 20, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyCollection<WorkOrderDto>>.Failure(new Error("tenant.required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var boundedLimit = Math.Clamp(limit, 1, 100);
        var orders = await workOrderRepository.ListRecentAsync(tenantId, boundedLimit, ct);

        var list = new List<WorkOrderDto>(orders.Count);
        foreach (var order in orders)
        {
            var customer = await customerRepository.GetByIdAsync(tenantId, order.CustomerId, ct);
            var vehicle = await vehicleRepository.GetByIdAsync(tenantId, order.VehicleId, ct);
            list.Add(ToDto(order, customer?.Name ?? "Cliente", vehicle?.Plate ?? string.Empty, vehicle?.Size.ToString() ?? "HatchSedan"));
        }

        return Result<IReadOnlyCollection<WorkOrderDto>>.Success(list);
    }

    public async Task<Result<WorkOrderDto>> ChangeStatusAsync(
        Guid tenantId,
        Guid workOrderId,
        ChangeWorkOrderStatusRequest request,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || workOrderId == Guid.Empty)
        {
            return Result<WorkOrderDto>.Failure(new Error("work_order.id.required", "Tenant e ID da ordem de serviço são obrigatórios.", ErrorType.Validation));
        }

        if (!Enum.TryParse<WorkOrderStatus>(request.TargetStatus, true, out var targetStatus))
        {
            return Result<WorkOrderDto>.Failure(new Error("work_order.target_status.invalid", $"Status '{request.TargetStatus}' inválido.", ErrorType.Validation));
        }

        var workOrder = await workOrderRepository.GetByIdAsync(tenantId, workOrderId, ct);
        if (workOrder is null)
        {
            return Result<WorkOrderDto>.Failure(new Error("work_order.not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        string? operatorName = null;
        if (request.OperatorId.HasValue && request.OperatorId.Value != Guid.Empty && teamMemberRepository is not null)
        {
            var member = await teamMemberRepository.GetByIdAsync(tenantId, request.OperatorId.Value, ct);
            if (member is null)
            {
                return Result<WorkOrderDto>.Failure(new Error("team_member.not_found", "Colaborador não encontrado.", ErrorType.NotFound));
            }

            if (!member.IsActive)
            {
                return Result<WorkOrderDto>.Failure(new Error("team_member.inactive", "Colaborador está inativo e não pode receber ordens de serviço.", ErrorType.Validation));
            }

            operatorName = member.FullName;
        }

        var previousStatus = workOrder.Status.ToString();
        var changeResult = workOrder.ChangeStatus(targetStatus, request.OperatorId, operatorName, request.Notes);
        if (!changeResult.IsSuccess)
        {
            return Result<WorkOrderDto>.Failure(changeResult.Error!);
        }

        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<WorkOrderDto>.Failure(saveResult.Error!);
        }

        if (realtimeNotifier is not null)
        {
            var notification = new WorkOrderMovedNotification(
                workOrder.Id,
                previousStatus,
                workOrder.Status.ToString(),
                workOrder.AssignedOperatorId,
                workOrder.AssignedOperatorName,
                DateTimeOffset.UtcNow,
                request.Notes);
            await realtimeNotifier.NotifyWorkOrderMovedAsync(tenantId, notification, ct);
        }

        if (targetStatus == WorkOrderStatus.ReadyForPickup && backgroundQueue is not null)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new WorkOrderReadyEventPayload(workOrder.Id));
            await backgroundQueue.EnqueueAsync(new TenantQueueMessage(tenantId, WorkOrderNotificationEvents.ReadyForPickup, payload, workOrder.Id), ct);
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, workOrder.CustomerId, ct);
        var vehicle = await vehicleRepository.GetByIdAsync(tenantId, workOrder.VehicleId, ct);
        return Result<WorkOrderDto>.Success(ToDto(workOrder, customer?.Name ?? "Cliente", vehicle?.Plate ?? string.Empty, vehicle?.Size.ToString() ?? "HatchSedan"));
    }

    public async Task<Result<WorkOrderDto>> AssignOperatorAsync(
        Guid tenantId,
        Guid workOrderId,
        Guid? operatorId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || workOrderId == Guid.Empty)
        {
            return Result<WorkOrderDto>.Failure(new Error("work_order.id.required", "Tenant e ID da ordem de serviço são obrigatórios.", ErrorType.Validation));
        }

        var workOrder = await workOrderRepository.GetByIdAsync(tenantId, workOrderId, ct);
        if (workOrder is null)
        {
            return Result<WorkOrderDto>.Failure(new Error("work_order.not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        string? operatorName = null;
        if (operatorId.HasValue && operatorId.Value != Guid.Empty && teamMemberRepository is not null)
        {
            var member = await teamMemberRepository.GetByIdAsync(tenantId, operatorId.Value, ct);
            if (member is null)
            {
                return Result<WorkOrderDto>.Failure(new Error("team_member.not_found", "Colaborador não encontrado.", ErrorType.NotFound));
            }

            if (!member.IsActive)
            {
                return Result<WorkOrderDto>.Failure(new Error("team_member.inactive", "Colaborador está inativo e não pode receber ordens de serviço.", ErrorType.Validation));
            }

            operatorName = member.FullName;
        }

        var assignResult = workOrder.AssignOperator(operatorId, operatorName);
        if (!assignResult.IsSuccess)
        {
            return Result<WorkOrderDto>.Failure(assignResult.Error!);
        }

        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<WorkOrderDto>.Failure(saveResult.Error!);
        }

        if (realtimeNotifier is not null)
        {
            await realtimeNotifier.NotifyOperatorAssignedAsync(tenantId, workOrder.Id, workOrder.AssignedOperatorId, workOrder.AssignedOperatorName, ct);
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, workOrder.CustomerId, ct);
        var vehicle = await vehicleRepository.GetByIdAsync(tenantId, workOrder.VehicleId, ct);
        return Result<WorkOrderDto>.Success(ToDto(workOrder, customer?.Name ?? "Cliente", vehicle?.Plate ?? string.Empty, vehicle?.Size.ToString() ?? "HatchSedan"));
    }

    public async Task<Result<IReadOnlyCollection<WorkOrderStatusHistoryDto>>> GetStatusHistoryAsync(
        Guid tenantId,
        Guid workOrderId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || workOrderId == Guid.Empty)
        {
            return Result<IReadOnlyCollection<WorkOrderStatusHistoryDto>>.Failure(new Error("work_order.id.required", "Tenant e ID da ordem de serviço são obrigatórios.", ErrorType.Validation));
        }

        var workOrder = await workOrderRepository.GetByIdAsync(tenantId, workOrderId, ct);
        if (workOrder is null)
        {
            return Result<IReadOnlyCollection<WorkOrderStatusHistoryDto>>.Failure(new Error("work_order.not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        var history = workOrder.StatusHistory
            .OrderBy(h => h.ChangedAtUtc)
            .Select(h => new WorkOrderStatusHistoryDto(
                h.Id,
                h.FromStatus?.ToString(),
                h.ToStatus.ToString(),
                h.ChangedAtUtc,
                h.ChangedByOperatorId,
                h.ChangedByOperatorName,
                h.Notes))
            .ToList();

        return Result<IReadOnlyCollection<WorkOrderStatusHistoryDto>>.Success(history);
    }

    public async Task<Result<YardKanbanBoardDto>> GetKanbanBoardAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<YardKanbanBoardDto>.Failure(new Error("tenant.required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var totalBoxes = 0;
        if (yardCapacityRepository is not null)
        {
            var capacity = await yardCapacityRepository.GetByTenantAsync(tenantId, ct);
            totalBoxes = capacity?.TotalBoxes ?? 0;
        }

        var activeOrders = await workOrderRepository.ListActiveAsync(tenantId, ct);

        var cards = new List<WorkOrderKanbanCardDto>(activeOrders.Count);
        foreach (var order in activeOrders)
        {
            var customer = await customerRepository.GetByIdAsync(tenantId, order.CustomerId, ct);
            var vehicle = await vehicleRepository.GetByIdAsync(tenantId, order.VehicleId, ct);
            var lastHistory = order.StatusHistory.OrderByDescending(h => h.ChangedAtUtc).FirstOrDefault();

            cards.Add(new WorkOrderKanbanCardDto(
                order.Id,
                order.CustomerId,
                customer?.Name ?? "Cliente",
                order.VehicleId,
                vehicle?.Plate ?? string.Empty,
                vehicle?.Size.ToString() ?? "HatchSedan",
                order.Status.ToString(),
                order.TotalAmount,
                order.EstimatedDurationMinutes,
                order.CreatedAtUtc,
                order.EstimatedCompletionAtUtc,
                order.AssignedOperatorId,
                order.AssignedOperatorName,
                order.Notes,
                order.Items.Select(i => i.ServiceName).ToList(),
                order.Items.Count,
                lastHistory?.ChangedAtUtc ?? order.CreatedAtUtc));
        }

        var columns = new List<YardKanbanColumnDto>(WorkOrderStatusConstants.OrderedStatuses.Count);
        foreach (var status in WorkOrderStatusConstants.OrderedStatuses)
        {
            var columnCards = cards.Where(c => c.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
            columns.Add(new YardKanbanColumnDto(
                status,
                WorkOrderStatusConstants.ToDisplayName(status),
                columnCards.Count,
                columnCards));
        }

        var occupiedBoxes = cards.Count(c =>
            c.Status == WorkOrderStatusConstants.InWashing ||
            c.Status == WorkOrderStatusConstants.Finishing ||
            c.Status == WorkOrderStatusConstants.QualityControl);

        return Result<YardKanbanBoardDto>.Success(new YardKanbanBoardDto(
            columns,
            cards.Count,
            totalBoxes,
            occupiedBoxes));
    }

    public async Task<Result<WorkOrderComparisonGalleryDto>> GetComparisonGalleryAsync(
        Guid tenantId,
        Guid workOrderId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || workOrderId == Guid.Empty)
        {
            return Result<WorkOrderComparisonGalleryDto>.Failure(new Error("work_order.id.required", "Tenant e ID da ordem de serviço são obrigatórios.", ErrorType.Validation));
        }

        var workOrder = await workOrderRepository.GetByIdAsync(tenantId, workOrderId, ct);
        if (workOrder is null)
        {
            return Result<WorkOrderComparisonGalleryDto>.Failure(new Error("work_order.not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, workOrder.CustomerId, ct);
        var vehicle = await vehicleRepository.GetByIdAsync(tenantId, workOrder.VehicleId, ct);

        VehicleInspection? inspection = null;
        if (vehicleInspectionRepository is not null)
        {
            inspection = await vehicleInspectionRepository.GetByWorkOrderIdAsync(tenantId, workOrderId, ct);
        }

        var isEligible = workOrder.IsEligibleForPostServicePhotos();
        var eligibleServices = workOrder.Items
            .Where(PostServiceEligibilityRule.IsEligible)
            .Select(i => new WorkOrderItemDto(i.Id, i.ServiceId, i.ServiceName, i.UnitPrice, i.EstimatedDurationMinutes, i.Quantity, i.TotalAmount, i.TotalDurationMinutes))
            .ToList();

        var allBeforePhotos = inspection?.Photos ?? [];
        var comparisonPairs = new List<PostServiceComparisonPairDto>();

        foreach (var postPhoto in workOrder.PostServicePhotos)
        {
            InspectionPhoto? matchedBefore = null;
            if (postPhoto.BeforeInspectionPhotoId.HasValue)
            {
                matchedBefore = allBeforePhotos.FirstOrDefault(p => p.Id == postPhoto.BeforeInspectionPhotoId.Value);
            }

            if (matchedBefore is null && postPhoto.Category != InspectionPhotoCategory.Other)
            {
                matchedBefore = allBeforePhotos.FirstOrDefault(p => p.Category == postPhoto.Category);
            }

            var serviceItem = postPhoto.WorkOrderItemId.HasValue
                ? workOrder.Items.FirstOrDefault(i => i.Id == postPhoto.WorkOrderItemId.Value)
                : null;

            InspectionPhotoDto? beforeDto = matchedBefore is null
                ? null
                : new InspectionPhotoDto(
                    matchedBefore.Id,
                    matchedBefore.Category,
                    matchedBefore.FileName,
                    matchedBefore.ContentType,
                    matchedBefore.SizeBytes,
                    matchedBefore.DamageId,
                    matchedBefore.UploadedAtUtc);

            var afterDto = new PostServicePhotoDto(
                postPhoto.Id,
                postPhoto.WorkOrderId,
                postPhoto.WorkOrderItemId,
                serviceItem?.ServiceName,
                postPhoto.BeforeInspectionPhotoId,
                postPhoto.Category,
                postPhoto.Title,
                postPhoto.FileName,
                postPhoto.ContentType,
                postPhoto.SizeBytes,
                postPhoto.UploadedAtUtc,
                postPhoto.Notes);

            comparisonPairs.Add(new PostServiceComparisonPairDto(
                postPhoto.WorkOrderItemId,
                serviceItem?.ServiceName,
                postPhoto.Category,
                postPhoto.Title,
                beforeDto,
                afterDto,
                postPhoto.Notes));
        }

        var pairedBeforeIds = comparisonPairs
            .Where(cp => cp.BeforePhoto is not null)
            .Select(cp => cp.BeforePhoto!.Id)
            .ToHashSet();

        var unpairedBefore = allBeforePhotos
            .Where(p => !pairedBeforeIds.Contains(p.Id))
            .Select(p => new InspectionPhotoDto(p.Id, p.Category, p.FileName, p.ContentType, p.SizeBytes, p.DamageId, p.UploadedAtUtc))
            .ToList();

        var unpairedAfter = comparisonPairs
            .Where(cp => cp.BeforePhoto is null)
            .Select(cp => cp.AfterPhoto)
            .ToList();

        var gallery = new WorkOrderComparisonGalleryDto(
            workOrder.Id,
            customer?.Name ?? "Cliente",
            vehicle?.Plate ?? string.Empty,
            vehicle?.Size.ToString() ?? "HatchSedan",
            workOrder.Status.ToString(),
            isEligible,
            eligibleServices,
            comparisonPairs,
            unpairedBefore,
            unpairedAfter);

        return Result<WorkOrderComparisonGalleryDto>.Success(gallery);
    }

    public async Task<Result<PostServicePhotoDto>> UploadPostServicePhotoAsync(
        Guid tenantId,
        Guid workOrderId,
        Guid? workOrderItemId,
        Guid? beforeInspectionPhotoId,
        InspectionPhotoCategory category,
        string title,
        Stream content,
        string fileName,
        string contentType,
        long sizeBytes,
        string? notes = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || workOrderId == Guid.Empty)
        {
            return Result<PostServicePhotoDto>.Failure(new Error("work_order.id.required", "Tenant e ID da ordem de serviço são obrigatórios.", ErrorType.Validation));
        }

        if (tenantObjectStorage is null)
        {
            return Result<PostServicePhotoDto>.Failure(new Error("storage.unavailable", "Serviço de armazenamento não disponível.", ErrorType.Validation));
        }

        var workOrder = await workOrderRepository.GetByIdAsync(tenantId, workOrderId, ct);
        if (workOrder is null)
        {
            return Result<PostServicePhotoDto>.Failure(new Error("work_order.not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        if (!AllowedContentTypes.Contains(contentType))
        {
            return Result<PostServicePhotoDto>.Failure(new Error("photo.content_type.invalid", "Formato de imagem inválido. Formatos suportados: JPEG, PNG e WebP.", ErrorType.Validation));
        }

        if (sizeBytes <= 0 || sizeBytes > MaxPhotoSizeBytes)
        {
            return Result<PostServicePhotoDto>.Failure(new Error("photo.size.exceeded", $"O tamanho da foto deve ser entre 1 byte e {MaxPhotoSizeBytes / (1024 * 1024)}MB.", ErrorType.Validation));
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            extension = contentType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }

        var uniqueFileName = $"{Guid.CreateVersion7():N}{extension}";
        var storagePath = $"post-service-photos/{workOrder.Id}/{uniqueFileName}";

        await tenantObjectStorage.PutAsync(tenantId, "post-service-photos", uniqueFileName, content, contentType, ct);

        var addResult = workOrder.AddPostServicePhoto(
            workOrderItemId,
            beforeInspectionPhotoId,
            category,
            title,
            storagePath,
            uniqueFileName,
            contentType,
            sizeBytes,
            notes);

        if (!addResult.IsSuccess)
        {
            return Result<PostServicePhotoDto>.Failure(addResult.Error!);
        }

        workOrderRepository.Update(workOrder);
        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<PostServicePhotoDto>.Failure(saveResult.Error!);
        }

        var photo = addResult.Value!;
        var serviceItem = photo.WorkOrderItemId.HasValue
            ? workOrder.Items.FirstOrDefault(i => i.Id == photo.WorkOrderItemId.Value)
            : null;

        return Result<PostServicePhotoDto>.Success(new PostServicePhotoDto(
            photo.Id,
            photo.WorkOrderId,
            photo.WorkOrderItemId,
            serviceItem?.ServiceName,
            photo.BeforeInspectionPhotoId,
            photo.Category,
            photo.Title,
            photo.FileName,
            photo.ContentType,
            photo.SizeBytes,
            photo.UploadedAtUtc,
            photo.Notes));
    }

    public async Task<Result<StoredObject>> GetPostServicePhotoStreamAsync(
        Guid tenantId,
        Guid workOrderId,
        Guid photoId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || workOrderId == Guid.Empty || photoId == Guid.Empty)
        {
            return Result<StoredObject>.Failure(new Error("work_order.id.required", "Tenant, ID da OS e ID da foto são obrigatórios.", ErrorType.Validation));
        }

        if (tenantObjectStorage is null)
        {
            return Result<StoredObject>.Failure(new Error("storage.unavailable", "Serviço de armazenamento não disponível.", ErrorType.Validation));
        }

        var workOrder = await workOrderRepository.GetByIdAsync(tenantId, workOrderId, ct);
        if (workOrder is null)
        {
            return Result<StoredObject>.Failure(new Error("work_order.not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        var photo = workOrder.PostServicePhotos.FirstOrDefault(p => p.Id == photoId);
        if (photo is null)
        {
            return Result<StoredObject>.Failure(new Error("post_service_photo.not_found", "Foto pós-serviço não encontrada.", ErrorType.NotFound));
        }

        var stored = await tenantObjectStorage.GetAsync(tenantId, "post-service-photos", photo.FileName, ct);
        if (stored is null)
        {
            return Result<StoredObject>.Failure(new Error("post_service_photo.file_not_found", "Arquivo da foto não encontrado no armazenamento.", ErrorType.NotFound));
        }

        return Result<StoredObject>.Success(stored);
    }

    public async Task<Result> RemovePostServicePhotoAsync(
        Guid tenantId,
        Guid workOrderId,
        Guid photoId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || workOrderId == Guid.Empty || photoId == Guid.Empty)
        {
            return Result.Failure(new Error("work_order.id.required", "Tenant, ID da OS e ID da foto são obrigatórios.", ErrorType.Validation));
        }

        var workOrder = await workOrderRepository.GetByIdAsync(tenantId, workOrderId, ct);
        if (workOrder is null)
        {
            return Result.Failure(new Error("work_order.not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        var removeResult = workOrder.RemovePostServicePhoto(photoId);
        if (!removeResult.IsSuccess)
        {
            return removeResult;
        }

        workOrderRepository.Update(workOrder);
        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        return saveResult.IsSuccess ? Result.Success() : Result.Failure(saveResult.Error!);
    }

    private static WorkOrderDto ToDto(WorkOrder order, string customerName, string plate, string vehicleSize) =>
        new(
            order.Id,
            order.CustomerId,
            customerName,
            order.VehicleId,
            plate,
            vehicleSize,
            order.Status.ToString(),
            order.TotalAmount,
            order.EstimatedDurationMinutes,
            order.CreatedAtUtc,
            order.EstimatedCompletionAtUtc,
            order.Notes,
            order.Items.Select(item => new WorkOrderItemDto(
                item.Id,
                item.ServiceId,
                item.ServiceName,
                item.UnitPrice,
                item.EstimatedDurationMinutes,
                item.Quantity,
                item.TotalAmount,
                item.TotalDurationMinutes)).ToList(),
            order.AssignedOperatorId,
            order.AssignedOperatorName,
            order.StatusHistory.Select(h => new WorkOrderStatusHistoryDto(
                h.Id,
                h.FromStatus?.ToString(),
                h.ToStatus.ToString(),
                h.ChangedAtUtc,
                h.ChangedByOperatorId,
                h.ChangedByOperatorName,
                h.Notes)).ToList());
}
