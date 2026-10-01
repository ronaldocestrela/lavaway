using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class Service : IMustHaveTenant
{
    private readonly List<ServicePrice> _prices = [];

    private Service()
    {
    }

    private Service(Guid id, Guid tenantId, string name, string category, IEnumerable<ServicePrice> prices)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Category = category;
        _prices.AddRange(prices);
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public string Name { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public IReadOnlyCollection<ServicePrice> Prices => _prices.AsReadOnly();

    public static Result<Service> Create(Guid tenantId, string name, string category, IEnumerable<ServicePrice>? prices)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            return Result<Service>.Failure(new Error("service.details.invalid", "Tenant and a service name of up to 200 characters are required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(category) || category.Trim().Length > 100)
        {
            return Result<Service>.Failure(new Error("service.category.invalid", "A service category of up to 100 characters is required.", ErrorType.Validation));
        }

        var normalizedPrices = prices?.ToArray() ?? [];
        if (normalizedPrices.Length == 0)
        {
            return Result<Service>.Failure(new Error("service.prices.required", "At least one vehicle-size price is required.", ErrorType.Validation));
        }

        if (normalizedPrices.Any(price => price.TenantId != tenantId))
        {
            return Result<Service>.Failure(new Error("service_price.tenant_mismatch", "All service prices must belong to the service tenant.", ErrorType.Conflict));
        }

        if (normalizedPrices.Select(price => price.VehicleSize).Distinct().Count() != normalizedPrices.Length)
        {
            return Result<Service>.Failure(new Error("service_price.duplicate_size", "Only one price per vehicle size is allowed.", ErrorType.Conflict));
        }

        return Result<Service>.Success(new Service(Guid.CreateVersion7(), tenantId, name.Trim(), category.Trim(), normalizedPrices));
    }

    public Result<Service> Update(string name, string category, IEnumerable<ServicePrice>? prices)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            return Result<Service>.Failure(new Error("service.details.invalid", "A service name of up to 200 characters is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(category) || category.Trim().Length > 100)
        {
            return Result<Service>.Failure(new Error("service.category.invalid", "A service category of up to 100 characters is required.", ErrorType.Validation));
        }

        var normalizedPrices = prices?.ToArray() ?? [];
        if (normalizedPrices.Length == 0)
        {
            return Result<Service>.Failure(new Error("service.prices.required", "At least one vehicle-size price is required.", ErrorType.Validation));
        }

        if (normalizedPrices.Any(price => price.TenantId != TenantId))
        {
            return Result<Service>.Failure(new Error("service_price.tenant_mismatch", "All service prices must belong to the service tenant.", ErrorType.Conflict));
        }

        if (normalizedPrices.Select(price => price.VehicleSize).Distinct().Count() != normalizedPrices.Length)
        {
            return Result<Service>.Failure(new Error("service_price.duplicate_size", "Only one price per vehicle size is allowed.", ErrorType.Conflict));
        }

        Name = name.Trim();
        Category = category.Trim();
        _prices.Clear();
        _prices.AddRange(normalizedPrices);

        return Result<Service>.Success(this);
    }
}