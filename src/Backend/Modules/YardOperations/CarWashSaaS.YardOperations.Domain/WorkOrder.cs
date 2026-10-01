using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class WorkOrder : IMustHaveTenant
{
    private readonly List<WorkOrderItem> _items = [];

    private WorkOrder()
    {
    }

    private WorkOrder(Guid id, Guid tenantId, Guid customerId, Guid vehicleId, IEnumerable<WorkOrderItem> items)
    {
        Id = id;
        TenantId = tenantId;
        CustomerId = customerId;
        VehicleId = vehicleId;
        Status = WorkOrderStatus.Waiting;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        _items.AddRange(items);
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
    public IReadOnlyCollection<WorkOrderItem> Items => _items.AsReadOnly();
    public decimal TotalAmount => _items.Sum(item => item.TotalAmount);
    public int EstimatedDurationMinutes => _items.Sum(item => item.TotalDurationMinutes);

    public static Result<WorkOrder> Create(Guid tenantId, Guid customerId, Guid vehicleId, IEnumerable<WorkOrderItem>? items)
    {
        if (tenantId == Guid.Empty || customerId == Guid.Empty || vehicleId == Guid.Empty)
        {
            return Result<WorkOrder>.Failure(new Error("work_order.owner.required", "Tenant, customer and vehicle are required.", ErrorType.Validation));
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

        return Result<WorkOrder>.Success(new WorkOrder(Guid.CreateVersion7(), tenantId, customerId, vehicleId, normalizedItems));
    }
}