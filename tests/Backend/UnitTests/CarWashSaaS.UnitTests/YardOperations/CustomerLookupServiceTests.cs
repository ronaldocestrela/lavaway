using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class CustomerLookupServiceTests
{
    [Fact]
    public async Task GetByPhoneAsync_ShouldReturnNull_WhenCustomerDoesNotExist()
    {
        var tenantId = Guid.CreateVersion7();
        var repo = new FakeCustomerVehicleEnvironment();
        var sut = new CustomerLookupService(repo, repo, repo, repo);

        var result = await sut.GetByPhoneAsync(tenantId, "11999998888");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetByPhoneAsync_ShouldReturnCustomer_WhenCustomerExists()
    {
        var tenantId = Guid.CreateVersion7();
        var repo = new FakeCustomerVehicleEnvironment();
        var customer = Customer.Create(tenantId, "Ana Souza", "11999998888").Value!;
        repo.Customers.Add(customer);
        var sut = new CustomerLookupService(repo, repo, repo, repo);

        var result = await sut.GetByPhoneAsync(tenantId, "11999998888");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Ana Souza", result.Value!.CustomerName);
        Assert.Equal(customer.Id, result.Value.CustomerId);
    }

    [Fact]
    public async Task RegisterCustomerAsync_ShouldCreateCustomer_WhenNotExists()
    {
        var tenantId = Guid.CreateVersion7();
        var repo = new FakeCustomerVehicleEnvironment();
        var sut = new CustomerLookupService(repo, repo, repo, repo);

        var result = await sut.RegisterCustomerAsync(tenantId, "Bruno Mendes", "+55 (11) 98888-7777");

        Assert.True(result.IsSuccess);
        Assert.Equal("Bruno Mendes", result.Value!.CustomerName);
        Assert.Single(repo.Customers);
        Assert.Equal(1, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task RegisterCustomerAsync_ShouldReturnExisting_WhenAlreadyExists()
    {
        var tenantId = Guid.CreateVersion7();
        var repo = new FakeCustomerVehicleEnvironment();
        var customer = Customer.Create(tenantId, "Bruno Mendes", "11988887777").Value!;
        repo.Customers.Add(customer);
        var sut = new CustomerLookupService(repo, repo, repo, repo);

        var result = await sut.RegisterCustomerAsync(tenantId, "Bruno Mendes", "11988887777");

        Assert.True(result.IsSuccess);
        Assert.Equal(customer.Id, result.Value!.CustomerId);
        Assert.Single(repo.Customers);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task RegisterVehicleForCustomerAsync_ShouldAddVehicle_WhenValid()
    {
        var tenantId = Guid.CreateVersion7();
        var repo = new FakeCustomerVehicleEnvironment();
        var customer = Customer.Create(tenantId, "Carlos Lima", "11977776666").Value!;
        repo.Customers.Add(customer);
        var sut = new CustomerLookupService(repo, repo, repo, repo);

        var result = await sut.RegisterVehicleForCustomerAsync(tenantId, customer.Id, "ABC-1234", "Suv");

        Assert.True(result.IsSuccess);
        Assert.Equal("ABC1234", result.Value!.Plate);
        Assert.Single(repo.Vehicles);
        Assert.Equal(customer.Id, repo.Vehicles[0].CustomerId);
        Assert.Equal(1, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task RegisterVehicleForCustomerAsync_ShouldReject_WhenPlateBelongsToAnotherTenantOrVehicle()
    {
        var tenantId = Guid.CreateVersion7();
        var repo = new FakeCustomerVehicleEnvironment();
        var customer1 = Customer.Create(tenantId, "Cliente 1", "11911111111").Value!;
        var customer2 = Customer.Create(tenantId, "Cliente 2", "11922222222").Value!;
        repo.Customers.Add(customer1);
        repo.Customers.Add(customer2);

        var existingVehicle = Vehicle.Create(tenantId, customer1.Id, "ABC1234", VehicleSize.HatchSedan).Value!;
        repo.Vehicles.Add(existingVehicle);

        var sut = new CustomerLookupService(repo, repo, repo, repo);

        var result = await sut.RegisterVehicleForCustomerAsync(tenantId, customer2.Id, "ABC-1234", "Suv");

        Assert.False(result.IsSuccess);
        Assert.Equal("vehicle.plate.duplicate", result.Error!.Code);
    }

    private sealed class FakeCustomerVehicleEnvironment :
        ICustomerVehicleSearchRepository,
        ICustomerRepository,
        IVehicleRepository,
        IUnitOfWork
    {
        public List<Customer> Customers { get; } = [];
        public List<Vehicle> Vehicles { get; } = [];
        public int SaveChangesCalls { get; private set; }

        public Task<CustomerVehicleMatchDto?> GetByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
        {
            var customer = Customers.FirstOrDefault(c => c.TenantId == tenantId && c.Id == customerId);
            if (customer is null) return Task.FromResult<CustomerVehicleMatchDto?>(null);

            var vehicles = Vehicles.Where(v => v.TenantId == tenantId && v.CustomerId == customerId)
                .Select(v => new VehicleSummaryDto(v.Id, v.Plate, v.Size.ToString()))
                .ToList();

            return Task.FromResult<CustomerVehicleMatchDto?>(new CustomerVehicleMatchDto(customer.Id, customer.Name, customer.Phone, vehicles));
        }

        public Task<IReadOnlyCollection<CustomerVehicleMatchDto>> SearchAsync(
            Guid tenantId,
            string? normalizedPlate,
            string? normalizedPhone,
            int limit,
            CancellationToken ct = default)
        {
            return SearchAsync(tenantId, normalizedPlate, normalizedPhone, limit, null, ct);
        }

        public Task<IReadOnlyCollection<CustomerVehicleMatchDto>> SearchAsync(
            Guid tenantId,
            string? normalizedPlate,
            string? normalizedPhone,
            int limit,
            string? query,
            CancellationToken ct = default)
        {
            var matches = Customers
                .Where(c => c.TenantId == tenantId && (normalizedPhone is null || c.NormalizedPhone == normalizedPhone))
                .Select(c =>
                {
                    var vehicles = Vehicles.Where(v => v.TenantId == tenantId && v.CustomerId == c.Id)
                        .Select(v => new VehicleSummaryDto(v.Id, v.Plate, v.Size.ToString()))
                        .ToList();
                    return new CustomerVehicleMatchDto(c.Id, c.Name, c.Phone, vehicles);
                })
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyCollection<CustomerVehicleMatchDto>>(matches);
        }

        public Task<Customer?> GetByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default) =>
            Task.FromResult(Customers.FirstOrDefault(c => c.TenantId == tenantId && c.Id == customerId));

        public Task<Customer?> GetByNormalizedPhoneAsync(Guid tenantId, string normalizedPhone, CancellationToken ct = default) =>
            Task.FromResult(Customers.FirstOrDefault(c => c.TenantId == tenantId && c.NormalizedPhone == normalizedPhone));

        public Task AddAsync(Customer customer, CancellationToken ct = default)
        {
            Customers.Add(customer);
            return Task.CompletedTask;
        }

        Task<Vehicle?> IVehicleRepository.GetByIdAsync(Guid tenantId, Guid vehicleId, CancellationToken ct) =>
            Task.FromResult(Vehicles.FirstOrDefault(v => v.TenantId == tenantId && v.Id == vehicleId));

        public Task<bool> IsPlateRegisteredAsync(Guid tenantId, string plate, CancellationToken ct = default) =>
            Task.FromResult(Vehicles.Any(v => v.TenantId == tenantId && v.Plate.Equals(plate, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> IsPlateRegisteredAsync(Guid tenantId, string plate, Guid excludedVehicleId, CancellationToken ct = default) =>
            Task.FromResult(Vehicles.Any(v => v.TenantId == tenantId && v.Id != excludedVehicleId && v.Plate.Equals(plate, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(Vehicle vehicle, CancellationToken ct = default)
        {
            Vehicles.Add(vehicle);
            return Task.CompletedTask;
        }

        public Task<Result<int>> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(Result<int>.Success(1));
        }
    }
}
