using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class InspectionPhoto : IMustHaveTenant
{
    private InspectionPhoto()
    {
    }

    internal InspectionPhoto(
        Guid id,
        Guid tenantId,
        Guid vehicleInspectionId,
        InspectionPhotoCategory category,
        string storagePath,
        string fileName,
        string contentType,
        long sizeBytes,
        Guid? damageId = null)
    {
        Id = id;
        TenantId = tenantId;
        VehicleInspectionId = vehicleInspectionId;
        Category = category;
        StoragePath = storagePath;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        DamageId = damageId;
        UploadedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public Guid VehicleInspectionId { get; private set; }
    public InspectionPhotoCategory Category { get; private set; }
    public string StoragePath { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public Guid? DamageId { get; private set; }
    public DateTimeOffset UploadedAtUtc { get; private set; }

    public static Result<InspectionPhoto> Create(
        Guid tenantId,
        Guid vehicleInspectionId,
        InspectionPhotoCategory category,
        string storagePath,
        string fileName,
        string contentType,
        long sizeBytes,
        Guid? damageId = null)
    {
        if (tenantId == Guid.Empty || vehicleInspectionId == Guid.Empty)
        {
            return Result<InspectionPhoto>.Failure(new Error("inspection.owner.required", "Tenant e Vistoria são obrigatórios.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return Result<InspectionPhoto>.Failure(new Error("photo.storage_path.required", "StoragePath é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Result<InspectionPhoto>.Failure(new Error("photo.filename.required", "Nome do arquivo é obrigatório.", ErrorType.Validation));
        }

        if (sizeBytes <= 0)
        {
            return Result<InspectionPhoto>.Failure(new Error("photo.size.invalid", "Tamanho do arquivo inválido.", ErrorType.Validation));
        }

        return Result<InspectionPhoto>.Success(new InspectionPhoto(
            Guid.CreateVersion7(),
            tenantId,
            vehicleInspectionId,
            category,
            storagePath,
            fileName,
            contentType,
            sizeBytes,
            damageId));
    }
}
