using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class WorkOrderNotificationApplicationServiceTests
{
    [Fact]
    public async Task SendReceiptNotificationAsync_Should_Generate_Pdf_And_Dispatch_Via_WhatsApp()
    {
        var tenantId = Guid.NewGuid();
        var customer = Customer.Create(tenantId, "Carlos Silva", "11987654321").Value!;
        var vehicle = Vehicle.Create(tenantId, customer.Id, "BRA2E19", VehicleSize.HatchSedan).Value!;
        var item = WorkOrderItem.Create(tenantId, Guid.NewGuid(), "Lavagem Completa", 80m, 60, 1).Value!;
        var workOrder = WorkOrder.Create(tenantId, customer.Id, vehicle.Id, [item]).Value!;

        var workOrderRepo = new FakeWorkOrderRepository([workOrder]);
        var customerRepo = new FakeCustomerRepository([customer]);
        var vehicleRepo = new FakeVehicleRepository([vehicle]);
        var inspectionRepo = new FakeVehicleInspectionRepository([]);
        var pdfGenerator = new FakePdfGenerator();
        var storage = new FakeTenantObjectStorage();
        var whatsAppDispatcher = new FakeWhatsAppDispatcher();
        var storeProfileLookup = new FakeStoreProfileLookup(new StoreProfileDto(
            Guid.NewGuid(), tenantId, "Lava Car Ltda", "Lava Car Top", "12345678000199", "1133334444", "Rua das Flores, 123", "São Paulo", "SP", "01234-000", null, null, null));

        var service = new WorkOrderNotificationApplicationService(
            workOrderRepo,
            customerRepo,
            vehicleRepo,
            inspectionRepo,
            pdfGenerator,
            storage,
            whatsAppDispatcher,
            storeProfileLookup);

        var result = await service.SendReceiptNotificationAsync(tenantId, workOrder.Id);

        Assert.True(result.IsSuccess);
        Assert.Single(whatsAppDispatcher.MediaDispatches);
        var dispatch = whatsAppDispatcher.MediaDispatches.First();
        Assert.Equal("11987654321", dispatch.Phone);
        Assert.Equal("document", dispatch.MediaType);
        Assert.Equal("application/pdf", dispatch.MimeType);
        Assert.Equal($"receipt-{workOrder.Id:N}", dispatch.IdempotencyKey);
        Assert.True(storage.StoredFiles.ContainsKey($"work-orders/{workOrder.Id:N}/receipt.pdf"));
    }

    [Fact]
    public async Task SendReadyForPickupNotificationAsync_Should_Dispatch_Text_Message()
    {
        var tenantId = Guid.NewGuid();
        var customer = Customer.Create(tenantId, "Mariana Lima", "11999998888").Value!;
        var vehicle = Vehicle.Create(tenantId, customer.Id, "XYZ9988", VehicleSize.HatchSedan).Value!;
        var item = WorkOrderItem.Create(tenantId, Guid.NewGuid(), "Polimento Técnico", 350m, 120, 1).Value!;
        var workOrder = WorkOrder.Create(tenantId, customer.Id, vehicle.Id, [item]).Value!;



        var workOrderRepo = new FakeWorkOrderRepository([workOrder]);
        var customerRepo = new FakeCustomerRepository([customer]);
        var vehicleRepo = new FakeVehicleRepository([vehicle]);
        var inspectionRepo = new FakeVehicleInspectionRepository([]);
        var pdfGenerator = new FakePdfGenerator();
        var storage = new FakeTenantObjectStorage();
        var whatsAppDispatcher = new FakeWhatsAppDispatcher();
        var storeProfileLookup = new FakeStoreProfileLookup(new StoreProfileDto(
            Guid.NewGuid(), tenantId, "Estetica Auto", "Estética Auto", "12345678000199", "1133334444", "Av. Brasil, 500", "Campinas", "SP", "13000-000", null, null, null));

        var service = new WorkOrderNotificationApplicationService(
            workOrderRepo,
            customerRepo,
            vehicleRepo,
            inspectionRepo,
            pdfGenerator,
            storage,
            whatsAppDispatcher,
            storeProfileLookup);

        var result = await service.SendReadyForPickupNotificationAsync(tenantId, workOrder.Id);

        Assert.True(result.IsSuccess);
        Assert.Single(whatsAppDispatcher.TextDispatches);
        var dispatch = whatsAppDispatcher.TextDispatches.First();
        Assert.Equal("11999998888", dispatch.Phone);
        Assert.Contains("pronto para retirada", dispatch.Message);
        Assert.Contains("Mariana Lima", dispatch.Message);
        Assert.Equal($"ready-{workOrder.Id:N}", dispatch.IdempotencyKey);
    }

    [Fact]
    public async Task SendComparisonPhotosNotificationAsync_Should_Reject_When_Not_Eligible()
    {
        var tenantId = Guid.NewGuid();
        var customer = Customer.Create(tenantId, "Pedro", "11977776666").Value!;
        var vehicle = Vehicle.Create(tenantId, customer.Id, "ABC1234", VehicleSize.HatchSedan).Value!;
        // Serviço simples não elegível para fotos
        var item = WorkOrderItem.Create(tenantId, Guid.NewGuid(), "Ducha Rápida", 30m, 15, 1).Value!;
        var workOrder = WorkOrder.Create(tenantId, customer.Id, vehicle.Id, [item]).Value!;

        var service = new WorkOrderNotificationApplicationService(
            new FakeWorkOrderRepository([workOrder]),
            new FakeCustomerRepository([customer]),
            new FakeVehicleRepository([vehicle]),
            new FakeVehicleInspectionRepository([]),
            new FakePdfGenerator(),
            new FakeTenantObjectStorage(),
            new FakeWhatsAppDispatcher(),
            new FakeStoreProfileLookup(null));

        var result = await service.SendComparisonPhotosNotificationAsync(tenantId, workOrder.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("work_order.not_eligible_for_photos", result.Error!.Code);
    }

    [Fact]
    public async Task SendComparisonPhotosNotificationAsync_Should_Dispatch_When_Photos_Exist()
    {
        var tenantId = Guid.NewGuid();
        var customer = Customer.Create(tenantId, "Pedro", "11977776666").Value!;
        var vehicle = Vehicle.Create(tenantId, customer.Id, "ABC1234", VehicleSize.HatchSedan).Value!;
        // Serviço elegível: Polimento
        var item = WorkOrderItem.Create(tenantId, Guid.NewGuid(), "Polimento Comercial", 200m, 90, 1).Value!;

        var workOrder = WorkOrder.Create(tenantId, customer.Id, vehicle.Id, [item]).Value!;
        workOrder.ChangeStatus(WorkOrderStatus.InWashing);

        // Adiciona foto pós-serviço
        workOrder.AddPostServicePhoto(
            item.Id,
            null,
            InspectionPhotoCategory.Front,
            "Capô Polido",
            "tenants/t1/photos/capo.jpg",
            "capo.jpg",
            "image/jpeg",
            2048,
            "Acabamento espelhado.");


        var storage = new FakeTenantObjectStorage();
        var photoBytes = new byte[] { 1, 2, 3, 4, 5 };
        await storage.PutAsync(tenantId, "post-service-photos", "capo.jpg", new MemoryStream(photoBytes), "image/jpeg");

        var whatsAppDispatcher = new FakeWhatsAppDispatcher();
        var service = new WorkOrderNotificationApplicationService(
            new FakeWorkOrderRepository([workOrder]),
            new FakeCustomerRepository([customer]),
            new FakeVehicleRepository([vehicle]),
            new FakeVehicleInspectionRepository([]),
            new FakePdfGenerator(),
            storage,
            whatsAppDispatcher,
            new FakeStoreProfileLookup(null));

        var result = await service.SendComparisonPhotosNotificationAsync(tenantId, workOrder.Id);

        Assert.True(result.IsSuccess);
        Assert.Single(whatsAppDispatcher.MediaDispatches);
        var dispatch = whatsAppDispatcher.MediaDispatches.First();
        Assert.Equal("image", dispatch.MediaType);
        Assert.Equal("image/jpeg", dispatch.MimeType);
        Assert.Contains("Capô Polido", dispatch.Caption);
    }

    // Fakes para teste unitário
    private sealed class FakePdfGenerator : IWorkOrderReceiptPdfGenerator
    {
        public Result<byte[]> GenerateReceiptPdf(WorkOrderReceiptPdfModel model)
            => Result<byte[]>.Success(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\nFAKE\n%%EOF"));
    }

    private sealed class FakeWhatsAppDispatcher : IOutboundWhatsAppDispatcher
    {
        public List<(string Phone, string Message, string? IdempotencyKey)> TextDispatches { get; } = [];
        public List<(string Phone, string Caption, string MediaType, string MimeType, string FileName, string? IdempotencyKey)> MediaDispatches { get; } = [];

        public Task<Result<WhatsAppMessageDto>> DispatchTextMessageAsync(Guid tenantId, string recipientPhone, string messageText, string? idempotencyKey = null, CancellationToken ct = default)
        {
            TextDispatches.Add((recipientPhone, messageText, idempotencyKey));
            return Task.FromResult(Result<WhatsAppMessageDto>.Success(new WhatsAppMessageDto(
                Guid.NewGuid(), recipientPhone, messageText, "queued", null, 0, DateTimeOffset.UtcNow, null, null)));
        }

        public Task<Result<WhatsAppMessageDto>> DispatchMediaMessageAsync(Guid tenantId, string recipientPhone, string caption, string mediaType, string mediaUrlOrBase64, string mediaMimeType, string mediaFileName, string? idempotencyKey = null, CancellationToken ct = default)
        {
            MediaDispatches.Add((recipientPhone, caption, mediaType, mediaMimeType, mediaFileName, idempotencyKey));
            return Task.FromResult(Result<WhatsAppMessageDto>.Success(new WhatsAppMessageDto(
                Guid.NewGuid(), recipientPhone, caption, "queued", null, 0, DateTimeOffset.UtcNow, null, null, mediaType, mediaFileName)));
        }

        public Task<Result<IReadOnlyList<WhatsAppMessageDto>>> GetRecentMessagesAsync(Guid tenantId, int count = 20, CancellationToken ct = default)
            => Task.FromResult(Result<IReadOnlyList<WhatsAppMessageDto>>.Success([]));
    }

    private sealed class FakeStoreProfileLookup(StoreProfileDto? profile) : ITenantStoreProfileLookup
    {
        public Task<Result<StoreProfileDto>> GetProfileAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(profile is not null
                ? Result<StoreProfileDto>.Success(profile)
                : Result<StoreProfileDto>.Failure(new Error("store_profile.not_found", "Store profile not found", ErrorType.NotFound)));
    }

    private sealed class FakeTenantObjectStorage : ITenantObjectStorage
    {
        public Dictionary<string, (byte[] Data, string ContentType)> StoredFiles { get; } = [];

        public Task PutAsync(Guid tenantId, string category, string fileName, Stream content, string contentType, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            StoredFiles[$"{category}/{fileName}"] = (ms.ToArray(), contentType);
            return Task.CompletedTask;
        }

        public Task<StoredObject?> GetAsync(Guid tenantId, string category, string fileName, CancellationToken ct = default)
        {
            var key = $"{category}/{fileName}";
            if (StoredFiles.TryGetValue(key, out var item))
            {
                return Task.FromResult<StoredObject?>(new StoredObject(new MemoryStream(item.Data), item.ContentType));
            }
            return Task.FromResult<StoredObject?>(null);
        }
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
        public Task<IReadOnlyCollection<WorkOrder>> ListSurveysAsync(Guid tenantId, int limit = 50, CancellationToken ct = default) => Task.FromResult<IReadOnlyCollection<WorkOrder>>([]);
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
        public void Update(Vehicle vehicle) { }
    }


    private sealed class FakeVehicleInspectionRepository(IEnumerable<VehicleInspection> inspections) : IVehicleInspectionRepository
    {
        private readonly List<VehicleInspection> _inspections = inspections.ToList();
        public Task AddAsync(VehicleInspection inspection, CancellationToken ct = default) { _inspections.Add(inspection); return Task.CompletedTask; }
        public Task<VehicleInspection?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) => Task.FromResult(_inspections.FirstOrDefault(i => i.TenantId == tenantId && i.Id == id));
        public Task<VehicleInspection?> GetByWorkOrderIdAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default) => Task.FromResult(_inspections.FirstOrDefault(i => i.TenantId == tenantId && i.WorkOrderId == workOrderId));
        public void Update(VehicleInspection inspection) { }
    }
}
