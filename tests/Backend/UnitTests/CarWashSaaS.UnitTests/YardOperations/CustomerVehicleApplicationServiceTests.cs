using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class CustomerVehicleApplicationServiceTests
{
    [Fact]
    public async Task SearchAsync_ShouldNormalizePlateAndPhoneBeforeSearching()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        var result = await service.SearchAsync(
            Guid.CreateVersion7(),
            new SearchCustomerVehiclesQuery("abc-1d23", "+55 (11) 99999-9999"));

        Assert.True(result.IsSuccess);
        Assert.Equal("ABC1D23", repository.SearchedPlate);
        Assert.Equal("11999999999", repository.SearchedPhone);
    }

    [Fact]
    public async Task CreateAsync_ShouldAddCustomerAndVehicleAndSaveOnce()
    {
        var tenantId = Guid.CreateVersion7();
        var repository = new FakeRepository();
        var service = CreateService(repository);

        var result = await service.CreateAsync(tenantId, new CreateCustomerWithVehicleCommand(
            "Maria Silva", "+55 (11) 99999-9999", "abc-1d23", VehicleSize.HatchSedan));

        Assert.True(result.IsSuccess);
        Assert.Single(repository.Customers);
        Assert.Single(repository.Vehicles);
        Assert.Equal(1, repository.SaveChangesCount);
        Assert.Equal(tenantId, repository.Customers[0].TenantId);
        Assert.Equal(tenantId, repository.Vehicles[0].TenantId);
        Assert.Equal(repository.Customers[0].Id, repository.Vehicles[0].CustomerId);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectDuplicatePlateWithoutSaving()
    {
        var tenantId = Guid.CreateVersion7();
        var repository = new FakeRepository();
        repository.Vehicles.Add(Vehicle.Create(tenantId, Guid.CreateVersion7(), "ABC1D23", VehicleSize.Suv).Value!);
        var service = CreateService(repository);

        var result = await service.CreateAsync(tenantId, new CreateCustomerWithVehicleCommand(
            "Maria Silva", "5511999999999", "abc-1d23", VehicleSize.HatchSedan));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Empty(repository.Customers);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    [Fact]
    public async Task AddVehicleAsync_ShouldReturnAllVehiclesForTheCustomer()
    {
        var tenantId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Maria Silva", "5511999999999").Value!;
        var repository = new FakeRepository();
        repository.Customers.Add(customer);
        repository.Vehicles.Add(Vehicle.Create(tenantId, customer.Id, "ABC1D23", VehicleSize.HatchSedan).Value!);
        var service = CreateService(repository);

        var result = await service.AddVehicleAsync(tenantId, customer.Id,
            new AddVehicleToCustomerCommand("XYZ9Z99", VehicleSize.Suv));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Vehicles.Count);
        Assert.Contains(result.Value.Vehicles, vehicle => vehicle.Plate == "ABC1D23");
        Assert.Contains(result.Value.Vehicles, vehicle => vehicle.Plate == "XYZ9Z99");
    }

    private static CustomerVehicleApplicationService CreateService(FakeRepository repository) =>
        new(repository, repository, repository, repository);

    private sealed class FakeRepository : ICustomerVehicleSearchRepository, ICustomerRepository, IVehicleRepository, IUnitOfWork
    {
        public List<Customer> Customers { get; } = [];
        public List<Vehicle> Vehicles { get; } = [];
        public string? SearchedPlate { get; private set; }
        public string? SearchedPhone { get; private set; }
        public int SaveChangesCount { get; private set; }

        public Task<CustomerVehicleMatchDto?> GetByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
        {
            var customer = Customers.SingleOrDefault(value => value.TenantId == tenantId && value.Id == customerId);
            if (customer is null)
            {
                return Task.FromResult<CustomerVehicleMatchDto?>(null);
            }

            var vehicles = Vehicles
                .Where(value => value.TenantId == tenantId && value.CustomerId == customerId)
                .Select(value => new VehicleSummaryDto(value.Id, value.Plate, value.Size.ToString()))
                .ToArray();
            return Task.FromResult<CustomerVehicleMatchDto?>(new CustomerVehicleMatchDto(
                customer.Id, customer.Name, customer.Phone, vehicles));
        }

        public Task<IReadOnlyCollection<CustomerVehicleMatchDto>> SearchAsync(
            Guid tenantId, string? normalizedPlate, string? normalizedPhone, int limit, CancellationToken ct = default)
        {
            SearchedPlate = normalizedPlate;
            SearchedPhone = normalizedPhone;
            return Task.FromResult<IReadOnlyCollection<CustomerVehicleMatchDto>>([]);
        }

        public Task<Customer?> GetByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default) =>
            Task.FromResult(Customers.SingleOrDefault(customer => customer.TenantId == tenantId && customer.Id == customerId));

        public Task AddAsync(Customer customer, CancellationToken ct = default)
        {
            Customers.Add(customer);
            return Task.CompletedTask;
        }

        public Task<bool> IsPlateRegisteredAsync(Guid tenantId, string normalizedPlate, CancellationToken ct = default) =>
            Task.FromResult(Vehicles.Any(vehicle => vehicle.TenantId == tenantId && vehicle.Plate == normalizedPlate));

        public Task AddAsync(Vehicle vehicle, CancellationToken ct = default)
        {
            Vehicles.Add(vehicle);
            return Task.CompletedTask;
        }

        public Task<Result<int>> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveChangesCount++;
            return Task.FromResult(Result<int>.Success(1));
        }
    }
}