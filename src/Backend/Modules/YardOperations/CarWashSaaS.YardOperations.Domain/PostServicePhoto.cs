using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class PostServicePhoto : IMustHaveTenant
{
    private PostServicePhoto()
    {
    }

    internal PostServicePhoto(
        Guid id,
        Guid tenantId,
        Guid workOrderId,
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
        Id = id;
        TenantId = tenantId;
        WorkOrderId = workOrderId;
        WorkOrderItemId = workOrderItemId;
        BeforeInspectionPhotoId = beforeInspectionPhotoId;
        Category = category;
        Title = string.IsNullOrWhiteSpace(title) ? "Foto Pós-Serviço" : title.Trim();
        StoragePath = storagePath;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        UploadedAtUtc = DateTimeOffset.UtcNow;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public Guid WorkOrderId { get; private set; }
    public Guid? WorkOrderItemId { get; private set; }
    public Guid? BeforeInspectionPhotoId { get; private set; }
    public InspectionPhotoCategory Category { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public DateTimeOffset UploadedAtUtc { get; private set; }
    public string? Notes { get; private set; }

    public static Result<PostServicePhoto> Create(
        Guid tenantId,
        Guid workOrderId,
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
        if (tenantId == Guid.Empty || workOrderId == Guid.Empty)
        {
            return Result<PostServicePhoto>.Failure(new Error("post_service_photo.owner.required", "Tenant e Ordem de Serviço são obrigatórios.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return Result<PostServicePhoto>.Failure(new Error("post_service_photo.storage_path.required", "StoragePath é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Result<PostServicePhoto>.Failure(new Error("post_service_photo.filename.required", "Nome do arquivo é obrigatório.", ErrorType.Validation));
        }

        if (sizeBytes <= 0)
        {
            return Result<PostServicePhoto>.Failure(new Error("post_service_photo.size.invalid", "Tamanho do arquivo inválido.", ErrorType.Validation));
        }

        return Result<PostServicePhoto>.Success(new PostServicePhoto(
            Guid.CreateVersion7(),
            tenantId,
            workOrderId,
            workOrderItemId,
            beforeInspectionPhotoId,
            category,
            title,
            storagePath,
            fileName,
            contentType,
            sizeBytes,
            notes));
    }
}
