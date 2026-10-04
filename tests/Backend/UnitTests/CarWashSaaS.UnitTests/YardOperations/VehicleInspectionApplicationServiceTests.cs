using System.Text;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class VehicleInspectionApplicationServiceTests
{
    private readonly Guid _tenantId = Guid.CreateVersion7();
    private readonly Guid _workOrderId = Guid.CreateVersion7();
    private readonly Guid _vehicleId = Guid.CreateVersion7();

    [Fact]
    public async Task CreateOrGetInspectionAsync_ShouldCreateNewInspection_WhenNotExists()
    {
        var fake = new FakeInspectionEnvironment(_tenantId, _workOrderId, _vehicleId);
        var service = fake.CreateService();

        var request = new CreateInspectionRequest("Full", 35000, "Sem detalhes.");
        var result = await service.CreateOrGetInspectionAsync(_tenantId, _workOrderId, request);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(_workOrderId, dto.WorkOrderId);
        Assert.Equal(_vehicleId, dto.VehicleId);
        Assert.Equal("Full", dto.FuelLevel);
        Assert.Equal(35000, dto.OdometerKm);
        Assert.Equal(InspectionStatus.Draft, dto.Status);
        Assert.Equal(1, fake.CommitCount);
    }

    [Fact]
    public async Task CreateOrGetInspectionAsync_ShouldReturnExisting_WhenAlreadyCreated()
    {
        var fake = new FakeInspectionEnvironment(_tenantId, _workOrderId, _vehicleId);
        var existing = VehicleInspection.Create(_tenantId, _workOrderId, _vehicleId, "Half", 10000).Value!;
        fake.Inspections.Add(existing);
        var service = fake.CreateService();

        var request = new CreateInspectionRequest("Full", 20000);
        var result = await service.CreateOrGetInspectionAsync(_tenantId, _workOrderId, request);

        Assert.True(result.IsSuccess);
        Assert.Equal("Half", result.Value!.FuelLevel);
        Assert.Equal(10000, result.Value.OdometerKm);
        Assert.Equal(0, fake.CommitCount);
    }

    [Fact]
    public async Task AddDamageAsync_ShouldAddDamageAndCommit()
    {
        var fake = new FakeInspectionEnvironment(_tenantId, _workOrderId, _vehicleId);
        var inspection = VehicleInspection.Create(_tenantId, _workOrderId, _vehicleId).Value!;
        fake.Inspections.Add(inspection);
        var service = fake.CreateService();

        var request = new AddInspectionDamageRequest(DamageType.Scratch, VehicleView.LeftSide, 30.5m, 60.0m, DamageSeverity.High, "Risco lateral");
        var result = await service.AddDamageAsync(_tenantId, _workOrderId, request);

        Assert.True(result.IsSuccess);
        var damageDto = result.Value!;
        Assert.Equal(DamageType.Scratch, damageDto.Type);
        Assert.Equal(30.5m, damageDto.CoordinateX);
        Assert.Equal(1, fake.CommitCount);
    }

    [Fact]
    public async Task UploadPhotoAsync_ShouldStoreFileAndAttachPhoto()
    {
        var fake = new FakeInspectionEnvironment(_tenantId, _workOrderId, _vehicleId);
        var inspection = VehicleInspection.Create(_tenantId, _workOrderId, _vehicleId).Value!;
        fake.Inspections.Add(inspection);
        var service = fake.CreateService();

        var bytes = Encoding.UTF8.GetBytes("fake image data");
        using var stream = new MemoryStream(bytes);

        var result = await service.UploadPhotoAsync(
            _tenantId,
            _workOrderId,
            InspectionPhotoCategory.Front,
            stream,
            "frente.jpg",
            "image/jpeg",
            bytes.Length);

        Assert.True(result.IsSuccess);
        var photo = result.Value!;
        Assert.Equal(InspectionPhotoCategory.Front, photo.Category);
        Assert.True(fake.StoredFiles.ContainsKey(photo.FileName));
        Assert.Equal(1, fake.CommitCount);
    }

    [Fact]
    public async Task CompleteInspectionAsync_ShouldFail_WhenMissingPerimeterPhotos()
    {
        var fake = new FakeInspectionEnvironment(_tenantId, _workOrderId, _vehicleId);
        var inspection = VehicleInspection.Create(_tenantId, _workOrderId, _vehicleId).Value!;
        fake.Inspections.Add(inspection);
        var service = fake.CreateService();

        var result = await service.CompleteInspectionAsync(_tenantId, _workOrderId);

        Assert.False(result.IsSuccess);
        Assert.Equal("inspection.required_photos_missing", result.Error?.Code);
    }

    private sealed class FakeInspectionEnvironment : IVehicleInspectionRepository, IWorkOrderRepository, ITenantObjectStorage, IUnitOfWork
    {
        private readonly Guid _tenantId;
        private readonly Guid _workOrderId;
        private readonly Guid _vehicleId;

        public List<VehicleInspection> Inspections { get; } = [];
        public Dictionary<string, byte[]> StoredFiles { get; } = [];
        public int CommitCount { get; private set; }

        public FakeInspectionEnvironment(Guid tenantId, Guid workOrderId, Guid vehicleId)
        {
            _tenantId = tenantId;
            _workOrderId = workOrderId;
            _vehicleId = vehicleId;
        }

        public VehicleInspectionApplicationService CreateService() => new(this, this, this, this);

        // IVehicleInspectionRepository
        // IVehicleInspectionRepository
        Task<VehicleInspection?> IVehicleInspectionRepository.GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        {
            return Task.FromResult(Inspections.FirstOrDefault(i => i.TenantId == tenantId && i.Id == id));
        }

        public Task<VehicleInspection?> GetByWorkOrderIdAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default)
        {
            return Task.FromResult(Inspections.FirstOrDefault(i => i.TenantId == tenantId && i.WorkOrderId == workOrderId));
        }

        Task IVehicleInspectionRepository.AddAsync(VehicleInspection inspection, CancellationToken ct)
        {
            Inspections.Add(inspection);
            return Task.CompletedTask;
        }

        public void Update(VehicleInspection inspection)
        {
        }

        // IWorkOrderRepository
        Task<WorkOrder?> IWorkOrderRepository.GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        {
            if (tenantId != _tenantId || id != _workOrderId) return Task.FromResult<WorkOrder?>(null);

            var item = WorkOrderItem.Create(tenantId, Guid.NewGuid(), "Lavagem", 50m, 30).Value!;
            var wo = WorkOrder.Create(tenantId, Guid.NewGuid(), _vehicleId, [item]).Value!;
            // Set Id via reflection for test
            typeof(WorkOrder).GetProperty("Id")!.SetValue(wo, id);
            return Task.FromResult<WorkOrder?>(wo);
        }

        public Task<IReadOnlyCollection<WorkOrder>> ListRecentAsync(Guid tenantId, int limit = 20, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyCollection<WorkOrder>>([]);
        }

        public Task<IReadOnlyCollection<WorkOrder>> ListActiveAsync(Guid tenantId, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyCollection<WorkOrder>>([]);
        }

        Task IWorkOrderRepository.AddAsync(WorkOrder workOrder, CancellationToken ct) => Task.CompletedTask;

        // ITenantObjectStorage
        public Task PutAsync(Guid tenantId, string category, string fileName, Stream content, string contentType, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            StoredFiles[fileName] = ms.ToArray();
            return Task.CompletedTask;
        }

        public Task<StoredObject?> GetAsync(Guid tenantId, string category, string fileName, CancellationToken ct = default)
        {
            if (!StoredFiles.TryGetValue(fileName, out var data)) return Task.FromResult<StoredObject?>(null);
            return Task.FromResult<StoredObject?>(new StoredObject(new MemoryStream(data), "image/jpeg"));
        }

        // IUnitOfWork
        public Task<Result<int>> SaveChangesAsync(CancellationToken ct = default)
        {
            CommitCount++;
            return Task.FromResult(Result<int>.Success(1));
        }
    }
}
