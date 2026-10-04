using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class WorkOrderItem : IMustHaveTenant
{
    private WorkOrderItem()
    {
    }

    private WorkOrderItem(Guid tenantId, Guid serviceId, string serviceName, decimal unitPrice, int estimatedDurationMinutes, int quantity, string? serviceCategory = null)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ServiceId = serviceId;
        ServiceName = serviceName;
        UnitPrice = unitPrice;
        EstimatedDurationMinutes = estimatedDurationMinutes;
        Quantity = quantity;
        ServiceCategory = string.IsNullOrWhiteSpace(serviceCategory) ? null : serviceCategory.Trim();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public Guid ServiceId { get; private set; }
    public string ServiceName { get; private set; } = string.Empty;
    public string? ServiceCategory { get; private set; }
    public decimal UnitPrice { get; private set; }
    public int EstimatedDurationMinutes { get; private set; }
    public int Quantity { get; private set; }
    public decimal TotalAmount => UnitPrice * Quantity;
    public int TotalDurationMinutes => EstimatedDurationMinutes * Quantity;

    public static Result<WorkOrderItem> Create(
        Guid tenantId,
        Guid serviceId,
        string serviceName,
        decimal unitPrice,
        int estimatedDurationMinutes,
        int quantity = 1,
        string? serviceCategory = null)
    {
        if (tenantId == Guid.Empty || serviceId == Guid.Empty || string.IsNullOrWhiteSpace(serviceName) || serviceName.Trim().Length > 200)
        {
            return Result<WorkOrderItem>.Failure(new Error("work_order_item.details.invalid", "Tenant, service and service name are required.", ErrorType.Validation));
        }

        if (unitPrice <= 0 || estimatedDurationMinutes <= 0 || quantity <= 0)
        {
            return Result<WorkOrderItem>.Failure(new Error("work_order_item.value.invalid", "Price, duration and quantity must be greater than zero.", ErrorType.Validation));
        }

        return Result<WorkOrderItem>.Success(new WorkOrderItem(tenantId, serviceId, serviceName.Trim(), unitPrice, estimatedDurationMinutes, quantity, serviceCategory));
    }
}
