using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class WorkOrderApplicationServiceTests
{
    [Fact]
    public async Task CreateAsync_ShouldCreateWorkOrderWithCorrectServicePricesAndDurations()
    {
        var tenantId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Carlos Silva", "11988887777").Value!;
        var vehicle = Vehicle.Create(tenantId, customer.Id, "ABC1D23", VehicleSize.Suv).Value!;

        var price1 = ServicePrice.Create(tenantId, VehicleSize.Suv, 80m, 45).Value!;
        var price2 = ServicePrice.Create(tenantId, VehicleSize.Suv, 50m, 30).Value!;
        var service1 = Service.Create(tenantId, "Lavagem Completa", "Lavagem", [price1]).Value!;
        var service2 = Service.Create(tenantId, "Cera Protetora", "Acabamento", [price2]).Value!;

        var repo = new FakeWorkOrderRepository();
        repo.Customers.Add(customer);
        repo.Vehicles.Add(vehicle);
        repo.Services.Add(service1);
        repo.Services.Add(service2);

        var appService = new WorkOrderApplicationService(repo, repo, repo, repo, repo);

        var command = new CreateWorkOrderCommand(
            customer.Id,
            vehicle.Id,
            [
                new CreateWorkOrderItemInput(service1.Id, 1),
                new CreateWorkOrderItemInput(service2.Id, 1)
            ],
            "Cliente pediu secagem caprichada.");

        var result = await appService.CreateAsync(tenantId, command);

        Assert.True(result.IsSuccess);
        var workOrder = result.Value!;
        Assert.Equal(130m, workOrder.TotalAmount);
        Assert.Equal(75, workOrder.EstimatedDurationMinutes);
        Assert.Equal(2, workOrder.Items.Count);
        Assert.Equal(customer.Id, workOrder.CustomerId);
        Assert.Equal(vehicle.Id, workOrder.VehicleId);
        Assert.Equal("Cliente pediu secagem caprichada.", workOrder.Notes);
        Assert.Single(repo.SavedWorkOrders);
        Assert.Equal(1, repo.CommitCount);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectWhenCustomerNotFound()
    {
        var tenantId = Guid.CreateVersion7();
        var repo = new FakeWorkOrderRepository();
        var appService = new WorkOrderApplicationService(repo, repo, repo, repo, repo);

        var command = new CreateWorkOrderCommand(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            [new CreateWorkOrderItemInput(Guid.CreateVersion7(), 1)]);

        var result = await appService.CreateAsync(tenantId, command);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("customer.not_found", result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectWhenVehicleNotFound()
    {
        var tenantId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Carlos Silva", "11988887777").Value!;
        var repo = new FakeWorkOrderRepository();
        repo.Customers.Add(customer);
        var appService = new WorkOrderApplicationService(repo, repo, repo, repo, repo);

        var command = new CreateWorkOrderCommand(
            customer.Id,
            Guid.CreateVersion7(),
            [new CreateWorkOrderItemInput(Guid.CreateVersion7(), 1)]);

        var result = await appService.CreateAsync(tenantId, command);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("vehicle.not_found", result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectWhenVehicleDoesNotBelongToCustomer()
    {
        var tenantId = Guid.CreateVersion7();
        var customer1 = Customer.Create(tenantId, "Carlos Silva", "11988887777").Value!;
        var customer2 = Customer.Create(tenantId, "Ana Souza", "11977776666").Value!;
        var vehicle = Vehicle.Create(tenantId, customer2.Id, "ABC1D23", VehicleSize.Suv).Value!;

        var repo = new FakeWorkOrderRepository();
        repo.Customers.Add(customer1);
        repo.Customers.Add(customer2);
        repo.Vehicles.Add(vehicle);

        var appService = new WorkOrderApplicationService(repo, repo, repo, repo, repo);

        var command = new CreateWorkOrderCommand(
            customer1.Id,
            vehicle.Id,
            [new CreateWorkOrderItemInput(Guid.CreateVersion7(), 1)]);

        var result = await appService.CreateAsync(tenantId, command);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("vehicle.not_owned_by_customer", result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectWhenServiceNotFound()
    {
        var tenantId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Carlos Silva", "11988887777").Value!;
        var vehicle = Vehicle.Create(tenantId, customer.Id, "ABC1D23", VehicleSize.Suv).Value!;

        var repo = new FakeWorkOrderRepository();
        repo.Customers.Add(customer);
        repo.Vehicles.Add(vehicle);

        var appService = new WorkOrderApplicationService(repo, repo, repo, repo, repo);

        var missingServiceId = Guid.CreateVersion7();
        var command = new CreateWorkOrderCommand(
            customer.Id,
            vehicle.Id,
            [new CreateWorkOrderItemInput(missingServiceId, 1)]);

        var result = await appService.CreateAsync(tenantId, command);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("service.not_found", result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectWhenServiceHasNoPriceForVehicleSize()
    {
        var tenantId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Carlos Silva", "11988887777").Value!;
        var vehicle = Vehicle.Create(tenantId, customer.Id, "ABC1D23", VehicleSize.PickupVan).Value!;

        // Service only has price for HatchSedan
        var price = ServicePrice.Create(tenantId, VehicleSize.HatchSedan, 60m, 40).Value!;
        var service = Service.Create(tenantId, "Lavagem Simples", "Lavagem", [price]).Value!;

        var repo = new FakeWorkOrderRepository();
        repo.Customers.Add(customer);
        repo.Vehicles.Add(vehicle);
        repo.Services.Add(service);

        var appService = new WorkOrderApplicationService(repo, repo, repo, repo, repo);

        var command = new CreateWorkOrderCommand(
            customer.Id,
            vehicle.Id,
            [new CreateWorkOrderItemInput(service.Id, 1)]);

        var result = await appService.CreateAsync(tenantId, command);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("service.price_not_configured_for_size", result.Error.Code);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnWorkOrderWhenExists()
    {
        var tenantId = Guid.CreateVersion7();
        var item = WorkOrderItem.Create(tenantId, Guid.CreateVersion7(), "Polimento", 200m, 90).Value!;
        var workOrder = WorkOrder.Create(tenantId, Guid.CreateVersion7(), Guid.CreateVersion7(), [item]).Value!;

        var repo = new FakeWorkOrderRepository();
        repo.SavedWorkOrders.Add(workOrder);

        var appService = new WorkOrderApplicationService(repo, repo, repo, repo, repo);

        var result = await appService.GetAsync(tenantId, workOrder.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(workOrder.Id, result.Value!.Id);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNotFoundWhenMissing()
    {
        var tenantId = Guid.CreateVersion7();
        var repo = new FakeWorkOrderRepository();
        var appService = new WorkOrderApplicationService(repo, repo, repo, repo, repo);

        var result = await appService.GetAsync(tenantId, Guid.CreateVersion7());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    private sealed class FakeWorkOrderRepository : IWorkOrderRepository, ICustomerRepository, IVehicleRepository, IServiceRepository, IUnitOfWork
    {
        public List<WorkOrder> SavedWorkOrders { get; } = [];
        public List<Customer> Customers { get; } = [];
        public List<Vehicle> Vehicles { get; } = [];
        public List<Service> Services { get; } = [];
        public int CommitCount { get; private set; }

        Task<WorkOrder?> IWorkOrderRepository.GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(SavedWorkOrders.FirstOrDefault(w => w.TenantId == tenantId && w.Id == id));

        public Task<IReadOnlyCollection<WorkOrder>> ListRecentAsync(Guid tenantId, int limit = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>(SavedWorkOrders.Where(w => w.TenantId == tenantId).Take(limit).ToList());

        public Task<IReadOnlyCollection<WorkOrder>> ListActiveAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>(SavedWorkOrders.Where(w => w.TenantId == tenantId).ToList());

        public Task AddAsync(WorkOrder workOrder, CancellationToken ct = default)
        {
            SavedWorkOrders.Add(workOrder);
            return Task.CompletedTask;
        }

        public void Update(WorkOrder workOrder)
        {
        }

        Task<Customer?> ICustomerRepository.GetByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct) =>
            Task.FromResult(Customers.FirstOrDefault(c => c.TenantId == tenantId && c.Id == customerId));

        public Task AddAsync(Customer customer, CancellationToken ct = default)
        {
            Customers.Add(customer);
            return Task.CompletedTask;
        }

        Task<Vehicle?> IVehicleRepository.GetByIdAsync(Guid tenantId, Guid vehicleId, CancellationToken ct) =>
            Task.FromResult(Vehicles.FirstOrDefault(v => v.TenantId == tenantId && v.Id == vehicleId));

        public Task<bool> IsPlateRegisteredAsync(Guid tenantId, string normalizedPlate, CancellationToken ct = default) =>
            Task.FromResult(Vehicles.Any(v => v.TenantId == tenantId && v.Plate == normalizedPlate));

        public Task AddAsync(Vehicle vehicle, CancellationToken ct = default)
        {
            Vehicles.Add(vehicle);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<Service>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<Service>>(Services.Where(s => s.TenantId == tenantId).ToList());

        Task<Service?> IServiceRepository.GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(Services.FirstOrDefault(s => s.TenantId == tenantId && s.Id == id));

        public Task AddAsync(Service service, CancellationToken ct = default)
        {
            Services.Add(service);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Service service, CancellationToken ct = default) => Task.CompletedTask;

        public Task<Result<int>> SaveChangesAsync(CancellationToken ct = default)
        {
            CommitCount++;
            return Task.FromResult(Result<int>.Success(1));
        }
    }
}
