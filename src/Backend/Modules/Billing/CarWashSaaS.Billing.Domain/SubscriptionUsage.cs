using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Domain;

public sealed class SubscriptionUsage : IMustHaveTenant
{
    private SubscriptionUsage()
    {
    }

    internal SubscriptionUsage(
        Guid id,
        Guid tenantId,
        Guid customerSubscriptionId,
        Guid? workOrderId,
        string plate,
        string? serviceName,
        DateTimeOffset consumedAtUtc,
        string? notes)
    {
        Id = id;
        TenantId = tenantId;
        CustomerSubscriptionId = customerSubscriptionId;
        WorkOrderId = workOrderId;
        Plate = plate;
        ServiceName = serviceName;
        ConsumedAtUtc = consumedAtUtc;
        Notes = notes;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public Guid CustomerSubscriptionId { get; private set; }
    public Guid? WorkOrderId { get; private set; }
    public string Plate { get; private set; } = string.Empty;
    public string? ServiceName { get; private set; }
    public DateTimeOffset ConsumedAtUtc { get; private set; }
    public string? Notes { get; private set; }

    public static Result<SubscriptionUsage> Create(
        Guid tenantId,
        Guid customerSubscriptionId,
        Guid? workOrderId,
        string plate,
        string? serviceName,
        DateTimeOffset consumedAtUtc,
        string? notes)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<SubscriptionUsage>.Failure(new Error("subscription_usage.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (customerSubscriptionId == Guid.Empty)
        {
            return Result<SubscriptionUsage>.Failure(new Error("subscription_usage.subscription_required", "Assinatura é obrigatória.", ErrorType.Validation));
        }

        var normalizedPlate = SubscriptionVehiclePlate.NormalizePlate(plate);
        if (normalizedPlate.Length != 7)
        {
            return Result<SubscriptionUsage>.Failure(new Error("subscription_usage.plate_invalid", "Placa do veículo deve conter 7 caracteres alfanuméricos.", ErrorType.Validation));
        }

        return Result<SubscriptionUsage>.Success(new SubscriptionUsage(
            Guid.CreateVersion7(),
            tenantId,
            customerSubscriptionId,
            workOrderId,
            normalizedPlate,
            string.IsNullOrWhiteSpace(serviceName) ? null : serviceName.Trim(),
            consumedAtUtc,
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()));
    }
}
