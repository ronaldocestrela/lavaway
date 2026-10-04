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
    }
}
