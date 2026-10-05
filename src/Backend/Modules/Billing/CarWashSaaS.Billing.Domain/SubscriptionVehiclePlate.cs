using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Domain;

public sealed class SubscriptionVehiclePlate : IMustHaveTenant
{
    private SubscriptionVehiclePlate()
    {
    }

    internal SubscriptionVehiclePlate(
        Guid id,
        Guid tenantId,
        Guid customerSubscriptionId,
        string plate,
        DateTimeOffset addedAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        CustomerSubscriptionId = customerSubscriptionId;
        Plate = plate;
        AddedAtUtc = addedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public Guid CustomerSubscriptionId { get; private set; }
    public string Plate { get; private set; } = string.Empty;
    public DateTimeOffset AddedAtUtc { get; private set; }

    public static string NormalizePlate(string? plate) => new((plate ?? string.Empty)
        .Where(char.IsAsciiLetterOrDigit)
        .Select(char.ToUpperInvariant)
        .ToArray());

    public static Result<SubscriptionVehiclePlate> Create(
        Guid tenantId,
        Guid customerSubscriptionId,
        string plate,
        DateTimeOffset addedAtUtc)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<SubscriptionVehiclePlate>.Failure(new Error("subscription_plate.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (customerSubscriptionId == Guid.Empty)
        {
            return Result<SubscriptionVehiclePlate>.Failure(new Error("subscription_plate.subscription_required", "Assinatura é obrigatória.", ErrorType.Validation));
        }

        var normalized = NormalizePlate(plate);
        if (normalized.Length != 7)
        {
            return Result<SubscriptionVehiclePlate>.Failure(new Error("subscription_plate.invalid", "Placa do veículo deve conter 7 caracteres alfanuméricos.", ErrorType.Validation));
        }

        return Result<SubscriptionVehiclePlate>.Success(new SubscriptionVehiclePlate(
            Guid.CreateVersion7(),
            tenantId,
            customerSubscriptionId,
            normalized,
            addedAtUtc));
    }
}
