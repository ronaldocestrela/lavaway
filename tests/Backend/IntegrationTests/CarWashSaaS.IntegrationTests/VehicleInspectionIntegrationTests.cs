using System.Text;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Domain;
using CarWashSaaS.Tenants.Infrastructure;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using CarWashSaaS.YardOperations.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.IntegrationTests;

[Collection(SqlServerFixture.CollectionName)]
public sealed class VehicleInspectionIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task CreateOrGetAsync_ShouldPersistInspectionWithDamagesChecklistAndPhotos()
    {
        var tenantId = await CreateTenantAsync("Inspection Persist Tenant");
        await using var context = CreateYardContext(tenantId);
        var (workOrderService, inspectionService) = CreateServices(context);

        // 1. Create Customer & Vehicle & WorkOrder
        var customer = Customer.Create(tenantId, "Mariana Lima", "11977776666").Value!;
        var vehicle = Vehicle.Create(tenantId, customer.Id, "ABC1D23", VehicleSize.HatchSedan).Value!;
        var price = ServicePrice.Create(tenantId, VehicleSize.HatchSedan, 50m, 30).Value!;
        var service = Service.Create(tenantId, "Ducha Simples", "Lavagem", [price]).Value!;
        context.Customers.Add(customer);
        context.Vehicles.Add(vehicle);
        context.Services.Add(service);
        await context.SaveChangesAsync();

        var woResult = await workOrderService.CreateAsync(tenantId, new CreateWorkOrderCommand(
            customer.Id,
            vehicle.Id,
            [new CreateWorkOrderItemInput(service.Id, 1)]));
        Assert.True(woResult.IsSuccess);
        var workOrderId = woResult.Value!.Id;

        // 2. Create or Get Inspection
        var createResult = await inspectionService.CreateOrGetInspectionAsync(tenantId, workOrderId, new CreateInspectionRequest("Half", 32000, "Sem pertences"));
        Assert.True(createResult.IsSuccess);
        Assert.Equal("Half", createResult.Value!.FuelLevel);
        Assert.Equal(32000, createResult.Value.OdometerKm);
        Assert.Equal(InspectionStatus.Draft, createResult.Value.Status);

        // 3. Add Damage
        var damageResult = await inspectionService.AddDamageAsync(tenantId, workOrderId, new AddInspectionDamageRequest(
            DamageType.Scratch,
            VehicleView.LeftSide,
            20.5m,
            45.0m,
            DamageSeverity.Medium,
            "Risco lateral"));
        Assert.True(damageResult.IsSuccess);

        // 4. Update Checklist
        var checklistResult = await inspectionService.UpdateChecklistAsync(tenantId, workOrderId, new UpdateInspectionChecklistRequest(
            "ThreeQuarters",
            32050,
            "Atualizado",
            [new UpdateChecklistItemRequest("spare_tire", "Estepe", ChecklistItemStatus.Ok)]));
        Assert.True(checklistResult.IsSuccess);

        // 5. Upload Required Photos (Front, Rear, Left, Right)
        var bytes = Encoding.UTF8.GetBytes("fake photo stream");
        foreach (var cat in new[] { InspectionPhotoCategory.Front, InspectionPhotoCategory.Rear, InspectionPhotoCategory.LeftSide, InspectionPhotoCategory.RightSide })
        {
            using var ms = new MemoryStream(bytes);
            var photoResult = await inspectionService.UploadPhotoAsync(
                tenantId,
                workOrderId,
                cat,
                ms,
                $"{cat}.jpg",
                "image/jpeg",
                bytes.Length);
            Assert.True(photoResult.IsSuccess);
        }

        // 6. Complete Inspection
        var completeResult = await inspectionService.CompleteInspectionAsync(tenantId, workOrderId);
        Assert.True(completeResult.IsSuccess);
        Assert.Equal(InspectionStatus.Completed, completeResult.Value!.Status);

        // 7. Read back from clean context
        await using var readContext = CreateYardContext(tenantId);
        var (_, readInspectionService) = CreateServices(readContext);
        var persisted = await readInspectionService.GetByWorkOrderIdAsync(tenantId, workOrderId);

        Assert.True(persisted.IsSuccess);
        var dto = persisted.Value!;
        Assert.Equal(InspectionStatus.Completed, dto.Status);
        Assert.Single(dto.Damages);
        Assert.Equal("ThreeQuarters", dto.FuelLevel);
        Assert.Equal(32050, dto.OdometerKm);
        Assert.Equal(4, dto.Photos.Count);
    }

    [Fact]
    public async Task CrossTenant_InspectionIsolation_ShouldReturnNotFound()
    {
        var tenantA = await CreateTenantAsync("Tenant A Inspection");
        var tenantB = await CreateTenantAsync("Tenant B Inspection");

        Guid tenantAWorkOrderId;

        // Setup Tenant A
        await using (var contextA = CreateYardContext(tenantA))
        {
            var (workOrderServiceA, inspectionServiceA) = CreateServices(contextA);
            var customer = Customer.Create(tenantA, "Carlos Silva", "11988887777").Value!;
            var vehicle = Vehicle.Create(tenantA, customer.Id, "XYZ9A88", VehicleSize.HatchSedan).Value!;
            var price = ServicePrice.Create(tenantA, VehicleSize.HatchSedan, 40m, 20).Value!;
            var service = Service.Create(tenantA, "Ducha", "Lavagem", [price]).Value!;
            contextA.Customers.Add(customer);
            contextA.Vehicles.Add(vehicle);
            contextA.Services.Add(service);
            await contextA.SaveChangesAsync();

            var wo = await workOrderServiceA.CreateAsync(tenantA, new CreateWorkOrderCommand(
                customer.Id,
                vehicle.Id,
                [new CreateWorkOrderItemInput(service.Id, 1)]));
            tenantAWorkOrderId = wo.Value!.Id;

            await inspectionServiceA.CreateOrGetInspectionAsync(tenantA, tenantAWorkOrderId, new CreateInspectionRequest());
        }

        // Tenant B attempts to read Tenant A's inspection
        await using (var contextB = CreateYardContext(tenantB))
        {
            var (_, inspectionServiceB) = CreateServices(contextB);
            var crossGet = await inspectionServiceB.GetByWorkOrderIdAsync(tenantB, tenantAWorkOrderId);
            Assert.False(crossGet.IsSuccess);
            Assert.Equal(ErrorType.NotFound, crossGet.Error!.Type);

            // Tenant B attempts to add damage to Tenant A's inspection
            var crossAddDamage = await inspectionServiceB.AddDamageAsync(tenantB, tenantAWorkOrderId, new AddInspectionDamageRequest(
                DamageType.Scratch, VehicleView.Front, 50m, 50m, DamageSeverity.Low));
            Assert.False(crossAddDamage.IsSuccess);
            Assert.Equal(ErrorType.NotFound, crossAddDamage.Error!.Type);
        }
    }

    [Fact]
    public async Task CrossTenant_InspectionPhotoStream_ShouldReturnNotFound()
    {
        var tenantA = await CreateTenantAsync("Tenant A Photo Stream");
        var tenantB = await CreateTenantAsync("Tenant B Photo Stream");

        Guid tenantAWorkOrderId;
        Guid photoId;

        await using (var contextA = CreateYardContext(tenantA))
        {
            var (workOrderServiceA, inspectionServiceA) = CreateServices(contextA);
            var customer = Customer.Create(tenantA, "Alice", "11999990000").Value!;
            var vehicle = Vehicle.Create(tenantA, customer.Id, "XYZ9999", VehicleSize.HatchSedan).Value!;
            var price = ServicePrice.Create(tenantA, VehicleSize.HatchSedan, 40m, 20).Value!;
            var service = Service.Create(tenantA, "Ducha", "Lavagem", [price]).Value!;
            contextA.Customers.Add(customer);
            contextA.Vehicles.Add(vehicle);
            contextA.Services.Add(service);
            await contextA.SaveChangesAsync();

            var wo = await workOrderServiceA.CreateAsync(tenantA, new CreateWorkOrderCommand(
                customer.Id,
                vehicle.Id,
                [new CreateWorkOrderItemInput(service.Id, 1)]));
            tenantAWorkOrderId = wo.Value!.Id;

            await inspectionServiceA.CreateOrGetInspectionAsync(tenantA, tenantAWorkOrderId, new CreateInspectionRequest());
            var bytes = Encoding.UTF8.GetBytes("sample image payload");
            using var ms = new MemoryStream(bytes);
            var uploadRes = await inspectionServiceA.UploadPhotoAsync(
                tenantA, tenantAWorkOrderId, InspectionPhotoCategory.Front, ms, "front.jpg", "image/jpeg", bytes.Length);
            Assert.True(uploadRes.IsSuccess);
            photoId = uploadRes.Value!.Id;

            // Tenant A can read its own photo stream
            var photoStreamA = await inspectionServiceA.GetPhotoStreamAsync(tenantA, tenantAWorkOrderId, photoId);
            Assert.True(photoStreamA.IsSuccess);
            Assert.NotNull(photoStreamA.Value);
        }

        // Tenant B attempts to read Tenant A's photo stream
        await using (var contextB = CreateYardContext(tenantB))
        {
            var (_, inspectionServiceB) = CreateServices(contextB);
            var crossPhotoStream = await inspectionServiceB.GetPhotoStreamAsync(tenantB, tenantAWorkOrderId, photoId);
            Assert.False(crossPhotoStream.IsSuccess);
            Assert.Equal(ErrorType.NotFound, crossPhotoStream.Error!.Type);
        }
    }

    [Fact]
    public async Task PurgeExpiredPhotos_Should_DeleteStorageBinary_And_MarkAsPurged_WhenRetirePastRetentionPeriod()
    {
        var tenantId = await CreateTenantAsync("Tenant Retention Purge Test");
        var storage = new InMemoryTenantStorage();

        Guid expiredWorkOrderId;
        Guid expiredPhotoId;
        Guid recentWorkOrderId;
        Guid recentPhotoId;

        await using (var context = CreateYardContext(tenantId))
        {
            var uow = new YardOperationsUnitOfWork(context);
            var woRepo = new WorkOrderRepository(context);
            var custRepo = new CustomerRepository(context);
            var vehRepo = new VehicleRepository(context);
            var srvRepo = new ServiceRepository(context);
            var inspRepo = new VehicleInspectionRepository(context);

            var woService = new WorkOrderApplicationService(woRepo, custRepo, vehRepo, srvRepo, uow);
            var inspService = new VehicleInspectionApplicationService(inspRepo, woRepo, storage, uow);

            var customer = Customer.Create(tenantId, "Cliente Expurgo", "11988887777").Value!;
            var vehicle = Vehicle.Create(tenantId, customer.Id, "PUR1234", VehicleSize.HatchSedan).Value!;
            var price = ServicePrice.Create(tenantId, VehicleSize.HatchSedan, 70m, 40).Value!;
            var service = Service.Create(tenantId, "Lavagem Completa", "Lavagem", [price]).Value!;
            await custRepo.AddAsync(customer);
            await vehRepo.AddAsync(vehicle);
            await srvRepo.AddAsync(service);
            await uow.SaveChangesAsync();

            // 1. Ordem de serviço entregue há 100 dias (fora da retenção padrão de 90 dias)
            var expiredWoDto = (await woService.CreateAsync(tenantId, new CreateWorkOrderCommand(
                customer.Id, vehicle.Id, [new CreateWorkOrderItemInput(service.Id, 1)]))).Value!;
            expiredWorkOrderId = expiredWoDto.Id;

            var expiredWoEntity = (await woRepo.GetByIdAsync(tenantId, expiredWorkOrderId))!;
            expiredWoEntity.ChangeStatus(WorkOrderStatus.InWashing);
            expiredWoEntity.ChangeStatus(WorkOrderStatus.Finishing);
            expiredWoEntity.ChangeStatus(WorkOrderStatus.QualityControl);
            expiredWoEntity.ChangeStatus(WorkOrderStatus.ReadyForPickup);
            expiredWoEntity.RegisterPickup(DateTimeOffset.UtcNow.AddDays(-100));
            woRepo.Update(expiredWoEntity);

            await inspService.CreateOrGetInspectionAsync(tenantId, expiredWorkOrderId, new CreateInspectionRequest());
            var bytes1 = Encoding.UTF8.GetBytes("expired inspection image");
            using var ms1 = new MemoryStream(bytes1);
            var upload1 = await inspService.UploadPhotoAsync(
                tenantId, expiredWorkOrderId, InspectionPhotoCategory.Front, ms1, "front_expired.jpg", "image/jpeg", bytes1.Length);
            expiredPhotoId = upload1.Value!.Id;

            // 2. Ordem de serviço recente entregue há 15 dias (dentro do prazo de 90 dias)
            var recentWoDto = (await woService.CreateAsync(tenantId, new CreateWorkOrderCommand(
                customer.Id, vehicle.Id, [new CreateWorkOrderItemInput(service.Id, 1)]))).Value!;
            recentWorkOrderId = recentWoDto.Id;

            var recentWoEntity = (await woRepo.GetByIdAsync(tenantId, recentWorkOrderId))!;
            recentWoEntity.ChangeStatus(WorkOrderStatus.InWashing);
            recentWoEntity.ChangeStatus(WorkOrderStatus.Finishing);
            recentWoEntity.ChangeStatus(WorkOrderStatus.QualityControl);
            recentWoEntity.ChangeStatus(WorkOrderStatus.ReadyForPickup);
            recentWoEntity.RegisterPickup(DateTimeOffset.UtcNow.AddDays(-15));
            woRepo.Update(recentWoEntity);

            await inspService.CreateOrGetInspectionAsync(tenantId, recentWorkOrderId, new CreateInspectionRequest());
            var bytes2 = Encoding.UTF8.GetBytes("recent inspection image");
            using var ms2 = new MemoryStream(bytes2);
            var upload2 = await inspService.UploadPhotoAsync(
                tenantId, recentWorkOrderId, InspectionPhotoCategory.Front, ms2, "front_recent.jpg", "image/jpeg", bytes2.Length);
            recentPhotoId = upload2.Value!.Id;

            await uow.SaveChangesAsync();
        }

        // Executar rotina de expurgo de fotos de vistoria
        await using (var context = CreateYardContext(tenantId))
        {
            var uow = new YardOperationsUnitOfWork(context);
            var inspRepo = new VehicleInspectionRepository(context);
            var mockLookup = new FakeTenantLookup(90);

            var retentionService = new InspectionPhotoRetentionApplicationService(
                inspRepo,
                storage,
                uow,
                mockLookup,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<InspectionPhotoRetentionApplicationService>.Instance);

            var purgeResult = await retentionService.PurgeExpiredInspectionPhotosAsync(tenantId);
            Assert.True(purgeResult.IsSuccess);
            Assert.Equal(1, purgeResult.Value);
        }

        // Verificar estados após expurgo
        await using (var context = CreateYardContext(tenantId))
        {
            var uow = new YardOperationsUnitOfWork(context);
            var woRepo = new WorkOrderRepository(context);
            var inspRepo = new VehicleInspectionRepository(context);
            var inspService = new VehicleInspectionApplicationService(inspRepo, woRepo, storage, uow);

            // Foto expirada deve falhar com código photo.purged
            var expiredPhotoResult = await inspService.GetPhotoStreamAsync(tenantId, expiredWorkOrderId, expiredPhotoId);
            Assert.False(expiredPhotoResult.IsSuccess);
            Assert.Equal("photo.purged", expiredPhotoResult.Error!.Code);

            // Foto recente deve permanecer íntegra e legível
            var recentPhotoResult = await inspService.GetPhotoStreamAsync(tenantId, recentWorkOrderId, recentPhotoId);
            Assert.True(recentPhotoResult.IsSuccess);
            Assert.NotNull(recentPhotoResult.Value);
        }
    }

    private sealed class FakeTenantLookup(int retentionDays) : ITenantStoreProfileLookup
    {
        public Task<Result<StoreProfileDto>> GetProfileAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<StoreProfileDto>.Success(new StoreProfileDto(
                Guid.NewGuid(),
                tenantId,
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
                retentionDays)));
    }

    private async Task<Guid> CreateTenantAsync(string name)
    {
        await using var context = new TenantsDbContext(fixture.CreateTenantsOptions(), new CurrentTenantAccessor());
        var tenant = Tenant.Create(name).Value!;
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return tenant.Id;
    }

    private YardOperationsDbContext CreateYardContext(Guid tenantId)
    {
        var accessor = new CurrentTenantAccessor();
        accessor.SetTenant(tenantId);
        return new YardOperationsDbContext(fixture.CreateYardOperationsOptions(), accessor);
    }

    private static (WorkOrderApplicationService, VehicleInspectionApplicationService) CreateServices(YardOperationsDbContext context)
    {
        var uow = new YardOperationsUnitOfWork(context);
        var woRepo = new WorkOrderRepository(context);
        var custRepo = new CustomerRepository(context);
        var vehRepo = new VehicleRepository(context);
        var srvRepo = new ServiceRepository(context);
        var inspRepo = new VehicleInspectionRepository(context);
        var storage = new InMemoryTenantStorage();

        var woService = new WorkOrderApplicationService(woRepo, custRepo, vehRepo, srvRepo, uow);
        var inspService = new VehicleInspectionApplicationService(inspRepo, woRepo, storage, uow);

        return (woService, inspService);
    }

    private sealed class InMemoryTenantStorage : ITenantObjectStorage
    {
        private readonly Dictionary<string, byte[]> _files = [];

        public Task PutAsync(Guid tenantId, string category, string fileName, Stream content, string contentType, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            _files[$"{tenantId}/{category}/{fileName}"] = ms.ToArray();
            return Task.CompletedTask;
        }

        public Task<StoredObject?> GetAsync(Guid tenantId, string category, string fileName, CancellationToken ct = default)
        {
            var key = $"{tenantId}/{category}/{fileName}";
            if (!_files.TryGetValue(key, out var bytes)) return Task.FromResult<StoredObject?>(null);
            return Task.FromResult<StoredObject?>(new StoredObject(new MemoryStream(bytes), "image/jpeg"));
        }

        public Task DeleteAsync(Guid tenantId, string category, string fileName, CancellationToken ct = default)
        {
            var key = $"{tenantId}/{category}/{fileName}";
            _files.Remove(key);
            return Task.CompletedTask;
        }
    }
}
