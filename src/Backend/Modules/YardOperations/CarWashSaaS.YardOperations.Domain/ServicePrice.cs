using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class ServicePrice : IMustHaveTenant
{
    private ServicePrice()
    {
    }

    private ServicePrice(Guid tenantId, VehicleSize vehicleSize, decimal amount, int estimatedDurationMinutes)
    {
        TenantId = tenantId;
        VehicleSize = vehicleSize;
        Amount = amount;
        EstimatedDurationMinutes = estimatedDurationMinutes;
    }

    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public VehicleSize VehicleSize { get; private set; }
    public decimal Amount { get; private set; }
    public int EstimatedDurationMinutes { get; private set; }

    public static Result<ServicePrice> Create(Guid tenantId, VehicleSize vehicleSize, decimal amount, int estimatedDurationMinutes)
    {
        if (tenantId == Guid.Empty || !Enum.IsDefined(vehicleSize))
        {
            return Result<ServicePrice>.Failure(new Error("service_price.owner_or_size.invalid", "Tenant and vehicle size are required.", ErrorType.Validation));
        }

        if (amount <= 0 || estimatedDurationMinutes <= 0)
        {
            return Result<ServicePrice>.Failure(new Error("service_price.value.invalid", "Price and estimated duration must be greater than zero.", ErrorType.Validation));
        }

        return Result<ServicePrice>.Success(new ServicePrice(tenantId, vehicleSize, amount, estimatedDurationMinutes));
    }
}
