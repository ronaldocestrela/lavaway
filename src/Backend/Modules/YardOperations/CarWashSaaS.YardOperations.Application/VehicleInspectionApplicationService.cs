using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed class VehicleInspectionApplicationService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private const long MaxPhotoSizeBytes = 10 * 1024 * 1024; // 10 MB

    private readonly IVehicleInspectionRepository _inspectionRepository;
    private readonly IWorkOrderRepository _workOrderRepository;
    private readonly ITenantObjectStorage _storage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICustomerRepository? _customerRepository;
    private readonly IVehicleRepository? _vehicleRepository;
    private readonly ITenantStoreProfileLookup? _storeProfileLookup;
    private readonly IBackgroundQueue? _backgroundQueue;

    public VehicleInspectionApplicationService(
        IVehicleInspectionRepository inspectionRepository,
        IWorkOrderRepository workOrderRepository,
        ITenantObjectStorage storage,
        IUnitOfWork unitOfWork,
        ICustomerRepository? customerRepository = null,
        IVehicleRepository? vehicleRepository = null,
        ITenantStoreProfileLookup? storeProfileLookup = null,
        IBackgroundQueue? backgroundQueue = null)
    {
        _inspectionRepository = inspectionRepository;
        _workOrderRepository = workOrderRepository;
        _storage = storage;
        _unitOfWork = unitOfWork;
        _customerRepository = customerRepository;
        _vehicleRepository = vehicleRepository;
        _storeProfileLookup = storeProfileLookup;
        _backgroundQueue = backgroundQueue;
    }


    public async Task<Result<VehicleInspectionDto>> GetByWorkOrderIdAsync(
        Guid tenantId,
        Guid workOrderId,
        CancellationToken ct = default)
    {
        var inspection = await _inspectionRepository.GetByWorkOrderIdAsync(tenantId, workOrderId, ct);
        if (inspection is null)
        {
            return Result<VehicleInspectionDto>.Failure(new Error("inspection.not_found", "Vistoria não encontrada para esta ordem de serviço.", ErrorType.NotFound));
        }

        return Result<VehicleInspectionDto>.Success(MapToDto(inspection));
    }

    public async Task<Result<VehicleInspectionDto>> CreateOrGetInspectionAsync(
        Guid tenantId,
        Guid workOrderId,
        CreateInspectionRequest request,
        CancellationToken ct = default)
    {
        var workOrder = await _workOrderRepository.GetByIdAsync(tenantId, workOrderId, ct);
        if (workOrder is null)
        {
            return Result<VehicleInspectionDto>.Failure(new Error("work_order.not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        var existing = await _inspectionRepository.GetByWorkOrderIdAsync(tenantId, workOrderId, ct);
        if (existing is not null)
        {
            return Result<VehicleInspectionDto>.Success(MapToDto(existing));
        }

        var inspectionResult = VehicleInspection.Create(
            tenantId,
            workOrderId,
            workOrder.VehicleId,
            request.FuelLevel,
            request.OdometerKm,
            request.Notes);

        if (!inspectionResult.IsSuccess)
        {
            return Result<VehicleInspectionDto>.Failure(inspectionResult.Error!);
        }

        var inspection = inspectionResult.Value!;
        await _inspectionRepository.AddAsync(inspection, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<VehicleInspectionDto>.Success(MapToDto(inspection));
    }

    public async Task<Result<InspectionDamageDto>> AddDamageAsync(
        Guid tenantId,
        Guid workOrderId,
        AddInspectionDamageRequest request,
        CancellationToken ct = default)
    {
        var inspection = await _inspectionRepository.GetByWorkOrderIdAsync(tenantId, workOrderId, ct);
        if (inspection is null)
        {
            return Result<InspectionDamageDto>.Failure(new Error("inspection.not_found", "Vistoria não encontrada.", ErrorType.NotFound));
        }

        var damageResult = inspection.AddDamage(
            request.Type,
            request.View,
            request.CoordinateX,
            request.CoordinateY,
            request.Severity,
            request.Description);

        if (!damageResult.IsSuccess)
        {
            return Result<InspectionDamageDto>.Failure(damageResult.Error!);
        }

        _inspectionRepository.Update(inspection);
        await _unitOfWork.SaveChangesAsync(ct);

        var damage = damageResult.Value!;
        return Result<InspectionDamageDto>.Success(new InspectionDamageDto(
            damage.Id,
            damage.Type,
            damage.View,
            damage.CoordinateX,
            damage.CoordinateY,
            damage.Severity,
            damage.Description));
    }

    public async Task<Result> RemoveDamageAsync(
        Guid tenantId,
        Guid workOrderId,
        Guid damageId,
        CancellationToken ct = default)
    {
        var inspection = await _inspectionRepository.GetByWorkOrderIdAsync(tenantId, workOrderId, ct);
        if (inspection is null)
        {
            return Result.Failure(new Error("inspection.not_found", "Vistoria não encontrada.", ErrorType.NotFound));
        }

        var removeResult = inspection.RemoveDamage(damageId);
        if (!removeResult.IsSuccess)
        {
            return removeResult;
        }

        _inspectionRepository.Update(inspection);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<VehicleInspectionDto>> UpdateChecklistAsync(
        Guid tenantId,
        Guid workOrderId,
        UpdateInspectionChecklistRequest request,
        CancellationToken ct = default)
    {
        var inspection = await _inspectionRepository.GetByWorkOrderIdAsync(tenantId, workOrderId, ct);
        if (inspection is null)
        {
            return Result<VehicleInspectionDto>.Failure(new Error("inspection.not_found", "Vistoria não encontrada.", ErrorType.NotFound));
        }

        var updateResult = inspection.UpdateChecklist(request.FuelLevel, request.OdometerKm, request.Notes, request.Items);
        if (!updateResult.IsSuccess)
        {
            return Result<VehicleInspectionDto>.Failure(updateResult.Error!);
        }

        _inspectionRepository.Update(inspection);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<VehicleInspectionDto>.Success(MapToDto(inspection));
    }

    public async Task<Result<InspectionPhotoDto>> UploadPhotoAsync(
        Guid tenantId,
        Guid workOrderId,
        InspectionPhotoCategory category,
        Stream content,
        string fileName,
        string contentType,
        long sizeBytes,
        Guid? damageId = null,
        CancellationToken ct = default)
    {
        var inspection = await _inspectionRepository.GetByWorkOrderIdAsync(tenantId, workOrderId, ct);
        if (inspection is null)
        {
            return Result<InspectionPhotoDto>.Failure(new Error("inspection.not_found", "Vistoria não encontrada.", ErrorType.NotFound));
        }

        if (inspection.Status == InspectionStatus.Completed)
        {
            return Result<InspectionPhotoDto>.Failure(new Error("inspection.already_completed", "Vistoria já concluída não pode receber novas fotos.", ErrorType.Conflict));
        }

        if (!AllowedContentTypes.Contains(contentType))
        {
            return Result<InspectionPhotoDto>.Failure(new Error("photo.content_type.invalid", "Formato de imagem inválido. Formatos suportados: JPEG, PNG e WebP.", ErrorType.Validation));
        }

        if (sizeBytes <= 0 || sizeBytes > MaxPhotoSizeBytes)
        {
            return Result<InspectionPhotoDto>.Failure(new Error("photo.size.exceeded", $"O tamanho da foto deve ser entre 1 byte e {MaxPhotoSizeBytes / (1024 * 1024)}MB.", ErrorType.Validation));
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
        var storagePath = $"inspections/{inspection.Id}/{uniqueFileName}";

        await _storage.PutAsync(tenantId, "inspections", uniqueFileName, content, contentType, ct);

        var photoResult = inspection.AddPhoto(category, storagePath, uniqueFileName, contentType, sizeBytes, damageId);
        if (!photoResult.IsSuccess)
        {
            return Result<InspectionPhotoDto>.Failure(photoResult.Error!);
        }

        _inspectionRepository.Update(inspection);
        await _unitOfWork.SaveChangesAsync(ct);

        var photo = photoResult.Value!;
        return Result<InspectionPhotoDto>.Success(new InspectionPhotoDto(
            photo.Id,
            photo.Category,
            photo.FileName,
            photo.ContentType,
            photo.SizeBytes,
            photo.DamageId,
            photo.UploadedAtUtc,
            photo.IsPurged,
            photo.PurgedAtUtc));
    }

    public async Task<Result<StoredObject>> GetPhotoStreamAsync(
        Guid tenantId,
        Guid workOrderId,
        Guid photoId,
        CancellationToken ct = default)
    {
        var inspection = await _inspectionRepository.GetByWorkOrderIdAsync(tenantId, workOrderId, ct);
        if (inspection is null)
        {
            return Result<StoredObject>.Failure(new Error("inspection.not_found", "Vistoria não encontrada.", ErrorType.NotFound));
        }

        var photo = inspection.Photos.FirstOrDefault(p => p.Id == photoId);
        if (photo is null)
        {
            return Result<StoredObject>.Failure(new Error("photo.not_found", "Foto não encontrada.", ErrorType.NotFound));
        }

        if (photo.IsPurged)
        {
            return Result<StoredObject>.Failure(new Error("photo.purged", "A foto foi expurgada em conformidade com a política de retenção de dados da LGPD (Art. 16).", ErrorType.Validation));
        }

        var stored = await _storage.GetAsync(tenantId, "inspections", photo.FileName, ct);
        if (stored is null)
        {
            return Result<StoredObject>.Failure(new Error("photo.file.not_found", "Arquivo físico não encontrado no storage.", ErrorType.NotFound));
        }

        return Result<StoredObject>.Success(stored);
    }

    public async Task<Result<VehicleInspectionDto>> CompleteInspectionAsync(
        Guid tenantId,
        Guid workOrderId,
        CancellationToken ct = default)
    {
        var inspection = await _inspectionRepository.GetByWorkOrderIdAsync(tenantId, workOrderId, ct);
        if (inspection is null)
        {
            return Result<VehicleInspectionDto>.Failure(new Error("inspection.not_found", "Vistoria não encontrada.", ErrorType.NotFound));
        }

        var completeResult = inspection.Complete();
        if (!completeResult.IsSuccess)
        {
            return Result<VehicleInspectionDto>.Failure(completeResult.Error!);
        }

        _inspectionRepository.Update(inspection);
        await _unitOfWork.SaveChangesAsync(ct);

        if (_backgroundQueue is not null)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new WorkOrderReceiptEventPayload(workOrderId));
            await _backgroundQueue.EnqueueAsync(new TenantQueueMessage(tenantId, WorkOrderNotificationEvents.ReceiptRequested, payload, workOrderId), ct);
        }

        return Result<VehicleInspectionDto>.Success(MapToDto(inspection));

    }

    public async Task<Result<PublicInspectionDto>> GetPublicInspectionByTokenAsync(
        string trackingToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(trackingToken))
        {
            return Result<PublicInspectionDto>.Failure(new Error("token.required", "Token de rastreamento é obrigatório.", ErrorType.Validation));
        }

        var workOrder = await _workOrderRepository.GetByTrackingTokenAsync(trackingToken, ct);
        if (workOrder is null)
        {
            return Result<PublicInspectionDto>.Failure(new Error("work_order.not_found", "Ordem de serviço ou vistoria não encontrada.", ErrorType.NotFound));
        }

        var tenantId = workOrder.TenantId;
        var customer = _customerRepository != null
            ? await _customerRepository.GetByIdAsync(tenantId, workOrder.CustomerId, ct)
            : null;

        var vehicle = _vehicleRepository != null
            ? await _vehicleRepository.GetByIdAsync(tenantId, workOrder.VehicleId, ct)
            : null;

        StoreProfileDto? storeProfile = null;
        if (_storeProfileLookup != null)
        {
            var profileResult = await _storeProfileLookup.GetProfileAsync(tenantId, ct);
            if (profileResult.IsSuccess)
            {
                storeProfile = profileResult.Value;
            }
        }

        var inspection = await _inspectionRepository.GetByWorkOrderIdAsync(tenantId, workOrder.Id, ct);

        var photos = (inspection?.Photos ?? [])
            .OrderBy(p => p.Category)
            .ThenBy(p => p.UploadedAtUtc)
            .Select(p => new PublicInspectionPhotoDto(
                p.Id,
                p.Category.ToString(),
                GetCategoryLabel(p.Category),
                $"/public/inspections/{workOrder.TrackingToken}/photos/{p.Id}",
                p.IsPurged,
                p.UploadedAtUtc))
            .ToList();

        var damages = (inspection?.Damages ?? [])
            .Select(d => new PublicInspectionDamageDto(
                d.Type.ToString(),
                d.View.ToString(),
                d.CoordinateX,
                d.CoordinateY,
                d.Severity.ToString(),
                d.Description))
            .ToList();

        var checklistItems = (inspection?.ChecklistItems ?? [])
            .Select(c => new PublicInspectionChecklistItemDto(
                c.Title,
                c.Status.ToString(),
                c.Observation))
            .ToList();

        var storeTradeName = storeProfile?.TradeName ?? "Lava-Jato";
        var storePhone = storeProfile?.Phone;
        var storeAddress = BuildStoreAddress(storeProfile);
        var customerName = customer?.Name ?? "Cliente";
        var plate = vehicle?.Plate ?? "---";
        var size = vehicle?.Size.ToString() ?? "Padrão";
        var model = vehicle is not null ? $"Veículo ({vehicle.Size})" : "Veículo";

        var publicDto = new PublicInspectionDto(
            workOrder.Id,
            workOrder.TrackingToken,
            storeTradeName,
            storePhone,
            storeAddress,
            storeProfile?.LogoUrl,
            storeProfile?.BrandPrimaryColor,
            customerName,
            plate,
            model,
            size,
            workOrder.CreatedAtUtc,
            workOrder.Status.ToString(),
            inspection?.OdometerKm,
            inspection?.FuelLevel,
            inspection?.Notes,
            inspection?.CompletedAtUtc,
            photos,
            damages,
            checklistItems);

        return Result<PublicInspectionDto>.Success(publicDto);
    }

    public async Task<Result<StoredObject>> GetPublicPhotoStreamAsync(
        string trackingToken,
        Guid photoId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(trackingToken) || photoId == Guid.Empty)
        {
            return Result<StoredObject>.Failure(new Error("photo.not_found", "Foto não encontrada.", ErrorType.NotFound));
        }

        var workOrder = await _workOrderRepository.GetByTrackingTokenAsync(trackingToken, ct);
        if (workOrder is null)
        {
            return Result<StoredObject>.Failure(new Error("photo.not_found", "Foto não encontrada.", ErrorType.NotFound));
        }

        var inspection = await _inspectionRepository.GetByWorkOrderIdAsync(workOrder.TenantId, workOrder.Id, ct);
        if (inspection is null)
        {
            return Result<StoredObject>.Failure(new Error("photo.not_found", "Foto não encontrada.", ErrorType.NotFound));
        }

        var photo = inspection.Photos.FirstOrDefault(p => p.Id == photoId);
        if (photo is null)
        {
            return Result<StoredObject>.Failure(new Error("photo.not_found", "Foto não encontrada.", ErrorType.NotFound));
        }

        if (photo.IsPurged)
        {
            return Result<StoredObject>.Failure(new Error("photo.purged", "A foto foi expurgada em conformidade com a política de retenção de dados da LGPD (Art. 16).", ErrorType.Conflict));
        }

        var stored = await _storage.GetAsync(workOrder.TenantId, "inspections", photo.FileName, ct);
        if (stored is null)
        {
            return Result<StoredObject>.Failure(new Error("photo.file.not_found", "Arquivo físico não encontrado no storage.", ErrorType.NotFound));
        }

        return Result<StoredObject>.Success(stored);
    }

    private static string GetCategoryLabel(InspectionPhotoCategory category) => category switch
    {
        InspectionPhotoCategory.Front => "Frente / Placa Dianteira",
        InspectionPhotoCategory.Rear => "Traseira / Placa Traseira",
        InspectionPhotoCategory.LeftSide => "Lateral Esquerda",
        InspectionPhotoCategory.RightSide => "Lateral Direita",
        InspectionPhotoCategory.DamageDetail => "Detalhe de Avaria",
        InspectionPhotoCategory.Interior => "Interior / Painel",
        _ => "Foto do Veículo"
    };

    private static string? BuildStoreAddress(StoreProfileDto? profile)
    {
        if (profile is null) return null;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(profile.Street)) parts.Add(profile.Street);
        if (!string.IsNullOrWhiteSpace(profile.City)) parts.Add(profile.City);
        if (!string.IsNullOrWhiteSpace(profile.State)) parts.Add(profile.State);
        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }

    private static VehicleInspectionDto MapToDto(VehicleInspection inspection)
    {
        return new VehicleInspectionDto(
            inspection.Id,
            inspection.TenantId,
            inspection.WorkOrderId,
            inspection.VehicleId,
            inspection.FuelLevel,
            inspection.OdometerKm,
            inspection.Notes,
            inspection.Status,
            inspection.CreatedAtUtc,
            inspection.CompletedAtUtc,
            inspection.Damages.Select(d => new InspectionDamageDto(
                d.Id,
                d.Type,
                d.View,
                d.CoordinateX,
                d.CoordinateY,
                d.Severity,
                d.Description)).ToArray(),
            inspection.ChecklistItems.Select(c => new InspectionChecklistItemDto(
                c.Id,
                c.ItemKey,
                c.Title,
                c.Status,
                c.Observation)).ToArray(),
            inspection.Photos.Select(p => new InspectionPhotoDto(
                p.Id,
                p.Category,
                p.FileName,
                p.ContentType,
                p.SizeBytes,
                p.DamageId,
                p.UploadedAtUtc,
                p.IsPurged,
                p.PurgedAtUtc)).ToArray());
    }
}
