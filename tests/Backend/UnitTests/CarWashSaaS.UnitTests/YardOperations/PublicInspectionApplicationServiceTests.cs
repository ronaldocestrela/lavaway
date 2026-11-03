using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class PublicInspectionApplicationServiceTests
{
    [Theory]
    [InlineData("http://localhost:5199/work-orders/0192e443-433b-72ba-97eb-3fec72c1c1f1/inspection", true)]
    [InlineData("http://localhost:5199/work-orders/0192e443-433b-72ba-97eb-3fec72c1c1f1/inspection/", true)]
    [InlineData("http://localhost:5199/work-orders/0192e443-433b-72ba-97eb-3fec72c1c1f1/inspection?tab=photos", true)]
    [InlineData("http://localhost:5199/work-orders/0192e443-433b-72ba-97eb-3fec72c1c1f1/inspection#photos", true)]
    [InlineData("http://localhost:5199/reception", false)]
    [InlineData("http://localhost:5199/yard", false)]
    public void IsInspectionFlow_Matching(string url, bool expected)
    {
        var uri = new Uri(url);
        var baseUri = new Uri("http://localhost:5199/");
        var relative = baseUri.MakeRelativeUri(uri).ToString();
        var clean = Uri.UnescapeDataString(relative).Split('?')[0].Split('#')[0].TrimEnd('/');
        var isInspection = clean.EndsWith("/inspection", StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expected, isInspection);
    }
    {
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var item = WorkOrderItem.Create(tenantId, Guid.NewGuid(), "Lavagem Simples", 50m, 30, 1).Value!;

        var order1 = WorkOrder.Create(tenantId, customerId, vehicleId, [item]).Value!;
        var order2 = WorkOrder.Create(tenantId, customerId, vehicleId, [item]).Value!;

        Assert.False(string.IsNullOrWhiteSpace(order1.TrackingToken));
        Assert.False(string.IsNullOrWhiteSpace(order2.TrackingToken));
        Assert.NotEqual(order1.TrackingToken, order2.TrackingToken);
        Assert.Equal(32, order1.TrackingToken.Length);
    }

    [Fact]
    public async Task GetPublicInspectionByTokenAsync_Should_Return_Full_Details_When_Valid()
    {
        var tenantId = Guid.NewGuid();
        var customer = Customer.Create(tenantId, "Ana Souza", "11988887777").Value!;
        var vehicle = Vehicle.Create(tenantId, customer.Id, "ABC1D23", VehicleSize.HatchSedan).Value!;
        var item = WorkOrderItem.Create(tenantId, Guid.NewGuid(), "Lavagem Completa", 80m, 45, 1).Value!;
        var workOrder = WorkOrder.Create(tenantId, customer.Id, vehicle.Id, [item]).Value!;

        var inspection = VehicleInspection.Create(tenantId, workOrder.Id, vehicle.Id, "Full", 25000, "Veículo bem cuidado").Value!;
        inspection.AddDamage(DamageType.Scratch, VehicleView.Front, 10.5m, 20.0m, DamageSeverity.Low, "Pequeno arranhão");

        inspection.AddPhoto(InspectionPhotoCategory.Front, "inspections/front.jpg", "front.jpg", "image/jpeg", 3);

        var workOrderRepo = new FakeWorkOrderRepository([workOrder]);
        var customerRepo = new FakeCustomerRepository([customer]);
        var vehicleRepo = new FakeVehicleRepository([vehicle]);
        var inspectionRepo = new FakeVehicleInspectionRepository([inspection]);
        var storage = new FakeTenantObjectStorage();
        var uow = new FakeUnitOfWork();
        var storeProfileLookup = new FakeStoreProfileLookup(new StoreProfileDto(
            Guid.NewGuid(), tenantId, "Top Clean Car Wash", "Top Clean", "12345678000199", "1199998888", "Rua A", "São Paulo", "SP", "01000-000", null, "#10b981", null));

        var service = new VehicleInspectionApplicationService(
            inspectionRepo,
            workOrderRepo,
            storage,
            uow,
            customerRepo,
            vehicleRepo,
            storeProfileLookup);

        var result = await service.GetPublicInspectionByTokenAsync(workOrder.TrackingToken);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(workOrder.Id, dto.WorkOrderId);
        Assert.Equal(workOrder.TrackingToken, dto.TrackingToken);
        Assert.Equal("Top Clean", dto.StoreTradeName);
        Assert.Equal("Ana Souza", dto.CustomerName);
        Assert.Equal("ABC1D23", dto.VehiclePlate);
        Assert.Equal(25000, dto.OdometerKm);
        Assert.Equal("Full", dto.FuelLevel);
        Assert.Single(dto.Photos);
        Assert.Equal("Frente / Placa Dianteira", dto.Photos[0].CategoryLabel);
        Assert.Contains(workOrder.TrackingToken, dto.Photos[0].Url);
        Assert.Single(dto.Damages);
        Assert.Equal("Scratch", dto.Damages[0].Type);
    }

    [Fact]
    public async Task GetPublicInspectionByTokenAsync_Should_Return_NotFound_When_Token_Invalid()
    {
        var workOrderRepo = new FakeWorkOrderRepository([]);
        var inspectionRepo = new FakeVehicleInspectionRepository([]);
        var storage = new FakeTenantObjectStorage();
        var uow = new FakeUnitOfWork();

        var service = new VehicleInspectionApplicationService(
            inspectionRepo,
            workOrderRepo,
            storage,
            uow);

        var result = await service.GetPublicInspectionByTokenAsync("non_existing_token");

        Assert.False(result.IsSuccess);
        Assert.Equal("work_order.not_found", result.Error!.Code);
    }

    [Fact]
    public async Task GetPublicPhotoStreamAsync_Should_Return_Conflict_When_Photo_Purged()
    {
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var item = WorkOrderItem.Create(tenantId, Guid.NewGuid(), "Lavagem", 40m, 30, 1).Value!;
        var workOrder = WorkOrder.Create(tenantId, customerId, vehicleId, [item]).Value!;

        var inspection = VehicleInspection.Create(tenantId, workOrder.Id, vehicleId).Value!;
        var photoResult = inspection.AddPhoto(InspectionPhotoCategory.Rear, "inspections/rear.jpg", "rear.jpg", "image/jpeg", 100);
        var photoId = photoResult.Value!.Id;

        // Simula expurgo LGPD
        inspection.PurgePhoto(photoId, DateTimeOffset.UtcNow, "LGPD Art. 16");

        var workOrderRepo = new FakeWorkOrderRepository([workOrder]);
        var inspectionRepo = new FakeVehicleInspectionRepository([inspection]);
        var storage = new FakeTenantObjectStorage();
        var uow = new FakeUnitOfWork();

        var service = new VehicleInspectionApplicationService(
            inspectionRepo,
            workOrderRepo,
            storage,
            uow);

        var result = await service.GetPublicPhotoStreamAsync(workOrder.TrackingToken, photoId);

        Assert.False(result.IsSuccess);
        Assert.Equal("photo.purged", result.Error!.Code);
    }

    private sealed class FakeWorkOrderRepository(IEnumerable<WorkOrder> orders) : IWorkOrderRepository
    {
        private readonly List<WorkOrder> _orders = orders.ToList();
        public Task AddAsync(WorkOrder workOrder, CancellationToken ct = default) { _orders.Add(workOrder); return Task.CompletedTask; }
        public Task<WorkOrder?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) => Task.FromResult(_orders.FirstOrDefault(o => o.TenantId == tenantId && o.Id == id));
        public Task<IReadOnlyCollection<WorkOrder>> ListRecentAsync(Guid tenantId, int limit = 20, CancellationToken ct = default) => Task.FromResult<IReadOnlyCollection<WorkOrder>>(_orders.Where(o => o.TenantId == tenantId).Take(limit).ToList());
        public Task<IReadOnlyCollection<WorkOrder>> ListActiveAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult<IReadOnlyCollection<WorkOrder>>(_orders.Where(o => o.TenantId == tenantId && o.Status != WorkOrderStatus.ReadyForPickup).ToList());
        public Task<IReadOnlyCollection<WorkOrder>> GetWorkOrdersPendingSurveyAsync(Guid tenantId, DateTimeOffset cutoff, CancellationToken ct = default) => Task.FromResult<IReadOnlyCollection<WorkOrder>>([]);
        public Task<WorkOrder?> GetLatestCompletedOrderByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default) => Task.FromResult<WorkOrder?>(null);
        public Task<WorkOrder?> GetActiveOrderByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default) => Task.FromResult<WorkOrder?>(null);
        public Task<IReadOnlyCollection<WorkOrder>> ListSurveysAsync(Guid tenantId, int limit = 50, CancellationToken ct = default) => Task.FromResult<IReadOnlyCollection<WorkOrder>>([]);
        public Task<WorkOrder?> GetByTrackingTokenAsync(string trackingToken, CancellationToken ct = default) => Task.FromResult(_orders.FirstOrDefault(o => o.TrackingToken == trackingToken));
        public void Update(WorkOrder workOrder) { }
    }

    private sealed class FakeCustomerRepository(IEnumerable<Customer> customers) : ICustomerRepository
    {
        private readonly List<Customer> _customers = customers.ToList();
        public Task AddAsync(Customer customer, CancellationToken ct = default) { _customers.Add(customer); return Task.CompletedTask; }
        public Task<Customer?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) => Task.FromResult(_customers.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id));
        public Task<Customer?> GetByPhoneAsync(Guid tenantId, string phone, CancellationToken ct = default) => Task.FromResult(_customers.FirstOrDefault(c => c.TenantId == tenantId && c.Phone == phone));
        public void Update(Customer customer) { }
    }

    private sealed class FakeVehicleRepository(IEnumerable<Vehicle> vehicles) : IVehicleRepository
    {
        private readonly List<Vehicle> _vehicles = vehicles.ToList();
        public Task AddAsync(Vehicle vehicle, CancellationToken ct = default) { _vehicles.Add(vehicle); return Task.CompletedTask; }
        public Task<Vehicle?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) => Task.FromResult(_vehicles.FirstOrDefault(v => v.TenantId == tenantId && v.Id == id));
        public Task<Vehicle?> GetByPlateAsync(Guid tenantId, string plate, CancellationToken ct = default) => Task.FromResult(_vehicles.FirstOrDefault(v => v.TenantId == tenantId && v.Plate == plate));
        public Task<bool> IsPlateRegisteredAsync(Guid tenantId, string plate, CancellationToken ct = default) => Task.FromResult(_vehicles.Any(v => v.TenantId == tenantId && v.Plate == plate));
        public Task<IReadOnlyCollection<Vehicle>> ListByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default) => Task.FromResult<IReadOnlyCollection<Vehicle>>(_vehicles.Where(v => v.TenantId == tenantId && v.CustomerId == customerId).ToList());
    }

    private sealed class FakeVehicleInspectionRepository(IEnumerable<VehicleInspection> inspections) : IVehicleInspectionRepository
    {
        private readonly List<VehicleInspection> _inspections = inspections.ToList();
        public Task AddAsync(VehicleInspection inspection, CancellationToken ct = default) { _inspections.Add(inspection); return Task.CompletedTask; }
        public Task<VehicleInspection?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) => Task.FromResult(_inspections.FirstOrDefault(i => i.TenantId == tenantId && i.Id == id));
        public Task<VehicleInspection?> GetByWorkOrderIdAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default) => Task.FromResult(_inspections.FirstOrDefault(i => i.TenantId == tenantId && i.WorkOrderId == workOrderId));
        public void Update(VehicleInspection inspection) { }
        public Task<IReadOnlyList<VehicleInspection>> GetInspectionsWithPhotosEligibleForPurgeAsync(Guid tenantId, DateTimeOffset cutoffUtc, int limit = 100, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<VehicleInspection>>([]);
    }

    private sealed class FakeTenantObjectStorage : ITenantObjectStorage
    {
        public Task PutAsync(Guid tenantId, string bucket, string objectName, Stream data, string contentType, CancellationToken ct = default) => Task.CompletedTask;
        public Task<StoredObject?> GetAsync(Guid tenantId, string bucket, string objectName, CancellationToken ct = default) => Task.FromResult<StoredObject?>(new StoredObject(new MemoryStream(new byte[] { 1, 2 }), "image/jpeg"));
        public Task DeleteAsync(Guid tenantId, string bucket, string objectName, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> ExistsAsync(Guid tenantId, string bucket, string objectName, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Result<int>.Success(1));
    }

    private sealed class FakeStoreProfileLookup(StoreProfileDto? profile) : ITenantStoreProfileLookup
    {
        public Task<Result<StoreProfileDto>> GetProfileAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(profile != null ? Result<StoreProfileDto>.Success(profile) : Result<StoreProfileDto>.Failure(new Error("profile.not_found", "Profile not found", ErrorType.NotFound)));
    }
}
