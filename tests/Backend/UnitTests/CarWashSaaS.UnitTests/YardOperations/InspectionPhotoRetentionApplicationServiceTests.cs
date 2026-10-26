using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class InspectionPhotoRetentionApplicationServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _workOrderId = Guid.NewGuid();
    private readonly Guid _vehicleId = Guid.NewGuid();

    [Fact]
    public async Task PurgeExpiredInspectionPhotosAsync_Should_Delete_Binaries_And_Mark_Photos_As_Purged()
    {
        // Arrange
        var fakeEnv = new FakeRetentionEnvironment(_tenantId);
        var inspection = VehicleInspection.Create(_tenantId, _workOrderId, _vehicleId).Value!;
        var photo1 = inspection.AddPhoto(InspectionPhotoCategory.Front, "path/front.jpg", "front_123.jpg", "image/jpeg", 2048).Value!;
        var photo2 = inspection.AddPhoto(InspectionPhotoCategory.Rear, "path/rear.jpg", "rear_456.jpg", "image/jpeg", 2048).Value!;
        inspection.Complete();
        fakeEnv.Inspections.Add(inspection);
        fakeEnv.StoredFiles.Add("front_123.jpg");
        fakeEnv.StoredFiles.Add("rear_456.jpg");

        var service = new InspectionPhotoRetentionApplicationService(
            fakeEnv,
            fakeEnv,
            fakeEnv,
            fakeEnv,
            NullLogger<InspectionPhotoRetentionApplicationService>.Instance);

        // Act
        var result = await service.PurgeExpiredInspectionPhotosAsync(_tenantId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value);
        Assert.True(photo1.IsPurged);
        Assert.NotNull(photo1.PurgedAtUtc);
        Assert.True(photo2.IsPurged);
        Assert.NotNull(photo2.PurgedAtUtc);

        // Binários removidos do storage
        Assert.Contains("front_123.jpg", fakeEnv.DeletedFiles);
        Assert.Contains("rear_456.jpg", fakeEnv.DeletedFiles);
        Assert.Equal(1, fakeEnv.SavedChangesCount);
    }

    [Fact]
    public async Task PurgeExpiredInspectionPhotosAsync_Should_Enforce_Ceiling_Of_365_Days()
    {
        // Arrange
        var fakeEnv = new FakeRetentionEnvironment(_tenantId)
        {
            CustomRetentionDays = 500 // Acima do teto legal de 365
        };

        var service = new InspectionPhotoRetentionApplicationService(
            fakeEnv,
            fakeEnv,
            fakeEnv,
            fakeEnv,
            NullLogger<InspectionPhotoRetentionApplicationService>.Instance);

        // Act
        var result = await service.PurgeExpiredInspectionPhotosAsync(_tenantId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(fakeEnv.LastCutoffUtc);
        // O cutoff utilizado deve respeitar o teto máximo de 365 dias (não 500)
        var expectedMinCutoff = DateTimeOffset.UtcNow.AddDays(-366);
        var expectedMaxCutoff = DateTimeOffset.UtcNow.AddDays(-364);
        Assert.InRange(fakeEnv.LastCutoffUtc.Value, expectedMinCutoff, expectedMaxCutoff);
    }

    [Fact]
    public async Task PurgeExpiredInspectionPhotosAsync_Should_Return_Zero_When_No_Photos_Eligible()
    {
        // Arrange
        var fakeEnv = new FakeRetentionEnvironment(_tenantId);
        var service = new InspectionPhotoRetentionApplicationService(
            fakeEnv,
            fakeEnv,
            fakeEnv,
            fakeEnv,
            NullLogger<InspectionPhotoRetentionApplicationService>.Instance);

        // Act
        var result = await service.PurgeExpiredInspectionPhotosAsync(_tenantId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
        Assert.Empty(fakeEnv.DeletedFiles);
        Assert.Equal(0, fakeEnv.SavedChangesCount);
    }

    [Fact]
    public async Task PurgeExpiredInspectionPhotosAsync_Should_Fail_When_TenantId_Is_Empty()
    {
        // Arrange
        var fakeEnv = new FakeRetentionEnvironment(_tenantId);
        var service = new InspectionPhotoRetentionApplicationService(
            fakeEnv,
            fakeEnv,
            fakeEnv,
            fakeEnv,
            NullLogger<InspectionPhotoRetentionApplicationService>.Instance);

        // Act
        var result = await service.PurgeExpiredInspectionPhotosAsync(Guid.Empty);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("retention.tenant.required", result.Error?.Code);
    }

    private sealed class FakeRetentionEnvironment(Guid tenantId) :
        IVehicleInspectionRepository,
        ITenantObjectStorage,
        ITenantStoreProfileLookup,
        IUnitOfWork
    {
        public List<VehicleInspection> Inspections { get; } = [];
        public HashSet<string> StoredFiles { get; } = [];
        public List<string> DeletedFiles { get; } = [];
        public int? CustomRetentionDays { get; set; }
        public DateTimeOffset? LastCutoffUtc { get; private set; }
        public int SavedChangesCount { get; private set; }

        // IVehicleInspectionRepository
        public Task<VehicleInspection?> GetByIdAsync(Guid tId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Inspections.FirstOrDefault(i => i.TenantId == tId && i.Id == id));

        public Task<VehicleInspection?> GetByWorkOrderIdAsync(Guid tId, Guid workOrderId, CancellationToken ct = default) =>
            Task.FromResult(Inspections.FirstOrDefault(i => i.TenantId == tId && i.WorkOrderId == workOrderId));

        public Task AddAsync(VehicleInspection inspection, CancellationToken ct = default)
        {
            Inspections.Add(inspection);
            return Task.CompletedTask;
        }

        public void Update(VehicleInspection inspection) { }

        public Task<IReadOnlyList<VehicleInspection>> GetInspectionsWithPhotosEligibleForPurgeAsync(
            Guid tId,
            DateTimeOffset cutoffUtc,
            int limit = 100,
            CancellationToken ct = default)
        {
            LastCutoffUtc = cutoffUtc;
            var list = Inspections
                .Where(i => i.TenantId == tId && i.Photos.Any(p => !p.IsPurged))
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<VehicleInspection>>(list);
        }

        // ITenantObjectStorage
        public Task PutAsync(Guid tId, string category, string fileName, Stream content, string contentType, CancellationToken ct = default)
        {
            StoredFiles.Add(fileName);
            return Task.CompletedTask;
        }

        public Task<StoredObject?> GetAsync(Guid tId, string category, string fileName, CancellationToken ct = default)
        {
            if (StoredFiles.Contains(fileName))
            {
                return Task.FromResult<StoredObject?>(new StoredObject(new MemoryStream([1, 2, 3]), "image/jpeg"));
            }
            return Task.FromResult<StoredObject?>(null);
        }

        public Task DeleteAsync(Guid tId, string category, string fileName, CancellationToken ct = default)
        {
            DeletedFiles.Add(fileName);
            StoredFiles.Remove(fileName);
            return Task.CompletedTask;
        }

        // ITenantStoreProfileLookup
        public Task<Result<StoreProfileDto>> GetProfileAsync(Guid tId, CancellationToken ct = default)
        {
            if (CustomRetentionDays.HasValue)
            {
                return Task.FromResult(Result<StoreProfileDto>.Success(new StoreProfileDto(
                    Guid.NewGuid(),
                    tId,
                    "Loja Teste",
                    "Loja Teste",
                    "11222333000181",
                    "11999999999",
                    "Rua Teste",
                    "Sao Paulo",
                    "SP",
                    "01001000",
                    null,
                    null,
                    null,
                    CustomRetentionDays.Value)));
            }

            return Task.FromResult(Result<StoreProfileDto>.Failure(new Error("store_profile.not_found", "Not found", ErrorType.NotFound)));
        }

        // IUnitOfWork
        public Task<Result<int>> SaveChangesAsync(CancellationToken ct = default)
        {
            SavedChangesCount++;
            return Task.FromResult(Result<int>.Success(1));
        }
    }
}
