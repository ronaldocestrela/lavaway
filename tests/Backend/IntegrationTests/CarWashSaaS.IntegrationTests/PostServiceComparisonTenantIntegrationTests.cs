using System.Text;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Domain;
using CarWashSaaS.Tenants.Infrastructure;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using CarWashSaaS.YardOperations.Infrastructure;

namespace CarWashSaaS.IntegrationTests;

[Collection(SqlServerFixture.CollectionName)]
public sealed class PostServiceComparisonTenantIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task UploadPostServicePhoto_AndGetComparisonGallery_ShouldPersistAndPairPhotos()
    {
        var tenantId = await CreateTenantAsync("Post-Service Photo Tenant");
        await using var context = CreateYardContext(tenantId);
        var (workOrderService, inspectionService) = CreateServices(context);

        // 1. Create Customer, Vehicle and Eligible Service (Vitrificação)
        var customer = Customer.Create(tenantId, "Rodrigo Detalhes", "11988887777").Value!;
        var vehicle = Vehicle.Create(tenantId, customer.Id, "XYZ9W88", VehicleSize.Suv).Value!;
        var price = ServicePrice.Create(tenantId, VehicleSize.Suv, 800m, 180).Value!;
        var service = Service.Create(tenantId, "Vitrificação 9H", ServiceCategoryConstants.Vitrificacao, [price]).Value!;

        context.Customers.Add(customer);
        context.Vehicles.Add(vehicle);
        context.Services.Add(service);
        await context.SaveChangesAsync();

        // 2. Open WorkOrder
        var woResult = await workOrderService.CreateAsync(tenantId, new CreateWorkOrderCommand(
            customer.Id,
            vehicle.Id,
            [new CreateWorkOrderItemInput(service.Id, 1)]));
        Assert.True(woResult.IsSuccess);
        var workOrderId = woResult.Value!.Id;
        var workOrderItemId = woResult.Value.Items.First().Id;

        // 3. Create Entrance Inspection and upload entrance photo
        await inspectionService.CreateOrGetInspectionAsync(tenantId, workOrderId, new CreateInspectionRequest());
        using var beforePhotoContent = new MemoryStream(Encoding.UTF8.GetBytes("fake-before-photo-binary"));
        var beforePhotoResult = await inspectionService.UploadPhotoAsync(
            tenantId,
            workOrderId,
            InspectionPhotoCategory.Front,
            beforePhotoContent,
            "front_before.jpg",
            "image/jpeg",
            beforePhotoContent.Length);
        Assert.True(beforePhotoResult.IsSuccess);
        var beforePhotoId = beforePhotoResult.Value!.Id;

        // 4. Advance WorkOrder status to Finishing
        await workOrderService.ChangeStatusAsync(tenantId, workOrderId, new ChangeWorkOrderStatusRequest("InWashing"));
        await workOrderService.ChangeStatusAsync(tenantId, workOrderId, new ChangeWorkOrderStatusRequest("Finishing"));

        // 5. Upload Post-Service photo paired with beforePhotoId
        using var afterPhotoContent = new MemoryStream(Encoding.UTF8.GetBytes("fake-after-photo-binary-vitrificacao"));
        var uploadResult = await workOrderService.UploadPostServicePhotoAsync(
            tenantId,
            workOrderId,
            workOrderItemId,
            beforePhotoId,
            InspectionPhotoCategory.Front,
            "Capô Vitrificado 9H",
            afterPhotoContent,
            "front_after.jpg",
            "image/jpeg",
            afterPhotoContent.Length,
            "Resultado com repelência hídrica e brilho profundo");
        Assert.True(uploadResult.IsSuccess);
        var postPhotoId = uploadResult.Value!.Id;
        Assert.Equal("Capô Vitrificado 9H", uploadResult.Value.Title);

        // 6. Retrieve Comparison Gallery
        var galleryResult = await workOrderService.GetComparisonGalleryAsync(tenantId, workOrderId);
        Assert.True(galleryResult.IsSuccess);
        var gallery = galleryResult.Value!;
        Assert.True(gallery.IsEligible);
        Assert.Single(gallery.EligibleServices);
        Assert.Equal("Vitrificação 9H", gallery.EligibleServices[0].ServiceName);

        Assert.Single(gallery.ComparisonPairs);
        var pair = gallery.ComparisonPairs[0];
        Assert.Equal("Capô Vitrificado 9H", pair.Title);
        Assert.NotNull(pair.BeforePhoto);
        Assert.Equal(beforePhotoId, pair.BeforePhoto!.Id);
        Assert.Equal(postPhotoId, pair.AfterPhoto.Id);
        Assert.Empty(gallery.UnpairedBeforePhotos);
        Assert.Empty(gallery.UnpairedAfterPhotos);

        // 7. Stream Photo Content
        var streamResult = await workOrderService.GetPostServicePhotoStreamAsync(tenantId, workOrderId, postPhotoId);
        Assert.True(streamResult.IsSuccess);
        Assert.Equal("image/jpeg", streamResult.Value!.ContentType);
    }

    [Fact]
    public async Task PostServicePhoto_TenantIsolation_ShouldPreventCrossTenantAccess()
    {
        var tenantA = await CreateTenantAsync("Tenant A Detailing");
        var tenantB = await CreateTenantAsync("Tenant B Detailing");

        Guid tenantAWorkOrderId;
        Guid tenantAPhotoId;

        // Tenant A creates OS with Detailing service and uploads photo
        await using (var contextA = CreateYardContext(tenantA))
        {
            var (workOrderServiceA, _) = CreateServices(contextA);
            var customer = Customer.Create(tenantA, "Carlos Silva", "11966665555").Value!;
            var vehicle = Vehicle.Create(tenantA, customer.Id, "ABC1234", VehicleSize.HatchSedan).Value!;
            var price = ServicePrice.Create(tenantA, VehicleSize.HatchSedan, 400m, 120).Value!;
            var service = Service.Create(tenantA, "Polimento Técnico", ServiceCategoryConstants.Polimento, [price]).Value!;

            contextA.Customers.Add(customer);
            contextA.Vehicles.Add(vehicle);
            contextA.Services.Add(service);
            await contextA.SaveChangesAsync();

            var wo = await workOrderServiceA.CreateAsync(tenantA, new CreateWorkOrderCommand(
                customer.Id,
                vehicle.Id,
                [new CreateWorkOrderItemInput(service.Id, 1)]));
            tenantAWorkOrderId = wo.Value!.Id;

            await workOrderServiceA.ChangeStatusAsync(tenantA, tenantAWorkOrderId, new ChangeWorkOrderStatusRequest("InWashing"));
            await workOrderServiceA.ChangeStatusAsync(tenantA, tenantAWorkOrderId, new ChangeWorkOrderStatusRequest("Finishing"));

            using var content = new MemoryStream(Encoding.UTF8.GetBytes("fake-tenant-a-photo"));
            var upload = await workOrderServiceA.UploadPostServicePhotoAsync(
                tenantA,
                tenantAWorkOrderId,
                wo.Value.Items.First().Id,
                null,
                InspectionPhotoCategory.LeftSide,
                "Polimento Lateral",
                content,
                "lateral_after.jpg",
                "image/jpeg",
                content.Length);
            Assert.True(upload.IsSuccess);
            tenantAPhotoId = upload.Value!.Id;
        }

        // Tenant B attempts to read or mutate Tenant A's post-service data
        await using (var contextB = CreateYardContext(tenantB))
        {
            var (workOrderServiceB, _) = CreateServices(contextB);

            // 1. Tenant B requests comparison gallery for Tenant A's work order
            var crossGallery = await workOrderServiceB.GetComparisonGalleryAsync(tenantB, tenantAWorkOrderId);
            Assert.False(crossGallery.IsSuccess);
            Assert.Equal(ErrorType.NotFound, crossGallery.Error!.Type);

            // 2. Tenant B attempts to stream Tenant A's photo
            var crossStream = await workOrderServiceB.GetPostServicePhotoStreamAsync(tenantB, tenantAWorkOrderId, tenantAPhotoId);
            Assert.False(crossStream.IsSuccess);
            Assert.Equal(ErrorType.NotFound, crossStream.Error!.Type);

            // 3. Tenant B attempts to upload photo to Tenant A's work order
            using var crossUploadStream = new MemoryStream(Encoding.UTF8.GetBytes("cross-upload"));
            var crossUpload = await workOrderServiceB.UploadPostServicePhotoAsync(
                tenantB,
                tenantAWorkOrderId,
                null,
                null,
                InspectionPhotoCategory.Front,
                "Invasão",
                crossUploadStream,
                "hack.jpg",
                "image/jpeg",
                crossUploadStream.Length);
            Assert.False(crossUpload.IsSuccess);
            Assert.Equal(ErrorType.NotFound, crossUpload.Error!.Type);

            // 4. Tenant B attempts to delete Tenant A's photo
            var crossDelete = await workOrderServiceB.RemovePostServicePhotoAsync(tenantB, tenantAWorkOrderId, tenantAPhotoId);
            Assert.False(crossDelete.IsSuccess);
            Assert.Equal(ErrorType.NotFound, crossDelete.Error!.Type);
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

        var woService = new WorkOrderApplicationService(
            woRepo,
            custRepo,
            vehRepo,
            srvRepo,
            uow,
            tenantObjectStorage: storage,
            vehicleInspectionRepository: inspRepo);

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
