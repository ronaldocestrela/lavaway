using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class YardCapacity : IMustHaveTenant
{
    private YardCapacity()
    {
    }

    private YardCapacity(Guid id, Guid tenantId, int totalBoxes, string description)
    {
        Id = id;
        TenantId = tenantId;
        TotalBoxes = totalBoxes;
        Description = description;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public int TotalBoxes { get; private set; }
    public string Description { get; private set; } = string.Empty;

    public static Result<YardCapacity> Create(Guid tenantId, int totalBoxes, string? description)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<YardCapacity>.Failure(new Error("yard_capacity.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        if (totalBoxes <= 0)
        {
            return Result<YardCapacity>.Failure(new Error("yard_capacity.total_boxes.invalid", "Total boxes must be greater than zero.", ErrorType.Validation));
        }

        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? "Pátio principal" : description.Trim();
        if (normalizedDescription.Length > 200)
        {
            return Result<YardCapacity>.Failure(new Error("yard_capacity.description.invalid", "Description cannot exceed 200 characters.", ErrorType.Validation));
        }

        return Result<YardCapacity>.Success(new YardCapacity(Guid.CreateVersion7(), tenantId, totalBoxes, normalizedDescription));
    }

    public Result<YardCapacity> Update(int totalBoxes, string? description)
    {
        if (totalBoxes <= 0)
        {
            return Result<YardCapacity>.Failure(new Error("yard_capacity.total_boxes.invalid", "Total boxes must be greater than zero.", ErrorType.Validation));
        }

        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? "Pátio principal" : description.Trim();
        if (normalizedDescription.Length > 200)
        {
            return Result<YardCapacity>.Failure(new Error("yard_capacity.description.invalid", "Description cannot exceed 200 characters.", ErrorType.Validation));
        }

        TotalBoxes = totalBoxes;
        Description = normalizedDescription;

        return Result<YardCapacity>.Success(this);
    }
}
