using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class ServiceCatalogApplicationServiceTests
{
    [Fact]
    public async Task CreateAsync_ShouldCreateServiceWithVehiclePricing()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryServiceRepository();
        var service = new ServiceCatalogApplicationService(repository);

        var command = new CreateServiceCommand(
            "Lavagem completa",
            "Exterior",
            [
                new ServicePriceInput(VehicleSize.HatchSedan, 80m, 45),
                new ServicePriceInput(VehicleSize.Suv, 120m, 60)
            ]);

        var result = await service.CreateAsync(tenantId, command);

        Assert.True(result.IsSuccess);
        Assert.Equal("Lavagem completa", result.Value!.Name);
        Assert.Equal(2, result.Value.Prices.Count);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectDuplicatePricePerVehicleSize()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryServiceRepository();
        var service = new ServiceCatalogApplicationService(repository);

        var command = new CreateServiceCommand(
            "Detalhamento",
            "Acabamento",
            [
                new ServicePriceInput(VehicleSize.HatchSedan, 80m, 45),
                new ServicePriceInput(VehicleSize.HatchSedan, 95m, 60)
            ]);

        var result = await service.CreateAsync(tenantId, command);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
    }

    private sealed class InMemoryServiceRepository : IServiceRepository
    {
        private readonly List<Service> _services = [];

        public Task<IReadOnlyCollection<Service>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyCollection<Service>>(_services.Where(service => service.TenantId == tenantId).ToList());
        }

        public Task<Service?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(_services.FirstOrDefault(service => service.TenantId == tenantId && service.Id == id));
        }

        public Task AddAsync(Service service, CancellationToken ct = default)
        {
            _services.Add(service);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Service service, CancellationToken ct = default)
        {
            var index = _services.FindIndex(existing => existing.Id == service.Id && existing.TenantId == service.TenantId);
            if (index >= 0)
            {
                _services[index] = service;
            }

            return Task.CompletedTask;
        }
    }
}
