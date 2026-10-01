using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class Vehicle : IMustHaveTenant
{
    private Vehicle()
    {
    }

    private Vehicle(Guid id, Guid tenantId, Guid customerId, string plate, VehicleSize size)
    {
        Id = id;
        TenantId = tenantId;
        CustomerId = customerId;
        Plate = plate;
        Size = size;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public Guid CustomerId { get; private set; }
    public string Plate { get; private set; } = string.Empty;
    public VehicleSize Size { get; private set; }

    public static string NormalizePlate(string? plate) => new((plate ?? string.Empty)
        .Where(char.IsAsciiLetterOrDigit)
        .Select(char.ToUpperInvariant)
        .ToArray());

    public static Result<Vehicle> Create(Guid tenantId, Guid customerId, string plate, VehicleSize size)
    {
        if (tenantId == Guid.Empty || customerId == Guid.Empty)
        {
            return Result<Vehicle>.Failure(new Error("vehicle.owner.required", "Tenant and customer are required.", ErrorType.Validation));
        }

        if (!Enum.IsDefined(size))
        {
            return Result<Vehicle>.Failure(new Error("vehicle.size.invalid", "Vehicle size is invalid.", ErrorType.Validation));
        }

        var normalizedPlate = NormalizePlate(plate);

        if (normalizedPlate.Length != 7)
        {
            return Result<Vehicle>.Failure(new Error("vehicle.plate.invalid", "Vehicle plate must contain seven letters or digits.", ErrorType.Validation));
        }

        return Result<Vehicle>.Success(new Vehicle(Guid.CreateVersion7(), tenantId, customerId, normalizedPlate, size));
    }
}