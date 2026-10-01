using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed class ServiceCatalogApplicationService(IServiceRepository repository)
{
    public async Task<Result<IReadOnlyCollection<Service>>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyCollection<Service>>.Failure(new Error("service.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var services = await repository.ListByTenantAsync(tenantId, ct);
        return Result<IReadOnlyCollection<Service>>.Success(services);
    }

    public async Task<Result<Service>> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<Service>.Failure(new Error("service.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var service = await repository.GetByIdAsync(tenantId, id, ct);
        if (service is null)
        {
            return Result<Service>.Failure(new Error("service.not_found", "Service was not found.", ErrorType.NotFound));
        }

        return Result<Service>.Success(service);
    }

    public async Task<Result<Service>> CreateAsync(Guid tenantId, CreateServiceCommand command, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<Service>.Failure(new Error("service.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var prices = CreateServicePrices(tenantId, command.Prices);
        if (!prices.IsSuccess)
        {
            return Result<Service>.Failure(prices.Error!);
        }

        var result = Service.Create(tenantId, command.Name, command.Category, prices.Value!);
        if (!result.IsSuccess)
        {
            return result;
        }

        await repository.AddAsync(result.Value!, ct);
        return result;
    }

    public async Task<Result<Service>> UpdateAsync(Guid tenantId, Guid id, UpdateServiceCommand command, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<Service>.Failure(new Error("service.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var existing = await repository.GetByIdAsync(tenantId, id, ct);
        if (existing is null)
        {
            return Result<Service>.Failure(new Error("service.not_found", "Service was not found.", ErrorType.NotFound));
        }

        var prices = CreateServicePrices(tenantId, command.Prices);
        if (!prices.IsSuccess)
        {
            return Result<Service>.Failure(prices.Error!);
        }

        var result = existing.Update(command.Name, command.Category, prices.Value!);
        if (!result.IsSuccess)
        {
            return result;
        }

        await repository.UpdateAsync(existing, ct);
        return result;
    }

    private static Result<ServicePrice[]> CreateServicePrices(Guid tenantId, IReadOnlyCollection<ServicePriceInput> prices)
    {
        if (prices is null || prices.Count == 0)
        {
            return Result<ServicePrice[]>.Failure(new Error("service.prices.required", "At least one vehicle-size price is required.", ErrorType.Validation));
        }

        var normalizedPrices = new List<ServicePrice>();
        foreach (var price in prices)
        {
            var result = ServicePrice.Create(tenantId, price.VehicleSize, price.Amount, price.EstimatedDurationMinutes);
            if (!result.IsSuccess)
            {
                return Result<ServicePrice[]>.Failure(result.Error!);
            }

            normalizedPrices.Add(result.Value!);
        }

        return Result<ServicePrice[]>.Success(normalizedPrices.ToArray());
    }
}
