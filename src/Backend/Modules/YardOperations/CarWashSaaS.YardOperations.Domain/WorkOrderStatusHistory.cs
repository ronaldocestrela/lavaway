using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class WorkOrderStatusHistory : IMustHaveTenant
{
    private WorkOrderStatusHistory()
    {
    }

    internal WorkOrderStatusHistory(
        Guid id,
        Guid tenantId,
        Guid workOrderId,
        WorkOrderStatus? fromStatus,
        WorkOrderStatus toStatus,
        DateTimeOffset changedAtUtc,
        Guid? changedByOperatorId = null,
        string? changedByOperatorName = null,
        string? notes = null)
    {
        Id = id;
        TenantId = tenantId;
        WorkOrderId = workOrderId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedAtUtc = changedAtUtc;
        ChangedByOperatorId = changedByOperatorId;
        ChangedByOperatorName = string.IsNullOrWhiteSpace(changedByOperatorName) ? null : changedByOperatorName.Trim();
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
    public WorkOrderStatus? FromStatus { get; private set; }
    public WorkOrderStatus ToStatus { get; private set; }
    public DateTimeOffset ChangedAtUtc { get; private set; }
    public Guid? ChangedByOperatorId { get; private set; }
    public string? ChangedByOperatorName { get; private set; }
    public string? Notes { get; private set; }
}
