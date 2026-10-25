using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class CustomerDataExportServiceTests
{
    [Fact]
    public async Task ExportDossierAsync_Should_ReturnSuccess_WithFullAggregatedData_WhenCustomerExists()
    {
        var tenantId = Guid.CreateVersion7();
        var customerId = Guid.CreateVersion7();
        var vehicleId = Guid.CreateVersion7();
        var workOrderId = Guid.CreateVersion7();

        var customer = Customer.Create(tenantId, "Carlos Eduardo", "11988887777").Value!;
        typeof(Customer).GetProperty(nameof(Customer.Id))!.SetValue(customer, customerId);

        var vehicle = Vehicle.Create(tenantId, customerId, "ABC1D23", VehicleSize.HatchSedan).Value!;
        typeof(Vehicle).GetProperty(nameof(Vehicle.Id))!.SetValue(vehicle, vehicleId);

        var items = new List<WorkOrderItem>
        {
            WorkOrderItem.Create(tenantId, Guid.CreateVersion7(), "Lavagem Completa", 70.00m, 45, 1, ServiceCategoryConstants.LavagemCompleta).Value!
        };
        var workOrder = WorkOrder.Create(tenantId, customerId, vehicleId, items, "Cliente pediu atenção nas rodas").Value!;
        typeof(WorkOrder).GetProperty(nameof(WorkOrder.Id))!.SetValue(workOrder, workOrderId);
        workOrder.ChangeStatus(WorkOrderStatus.InWashing);
        workOrder.ChangeStatus(WorkOrderStatus.Finishing);
        workOrder.ChangeStatus(WorkOrderStatus.QualityControl);
        workOrder.ChangeStatus(WorkOrderStatus.ReadyForPickup);
        workOrder.RegisterPickup(DateTimeOffset.UtcNow, "Entregue");
        workOrder.RecordSatisfactionSurveySent(DateTimeOffset.UtcNow.AddMinutes(-30));
        workOrder.RecordSatisfactionRating(5, "Serviço impecável!", DateTimeOffset.UtcNow);
        workOrder.MarkPaymentConfirmed(70.00m, "Pix", DateTimeOffset.UtcNow, "PIX-123");

        var repo = new FakeExportRepository();
        repo.Customers.Add(customer);
        repo.Vehicles.Add(vehicle);
        repo.WorkOrders.Add(workOrder);

        var fakeWhatsApp = new FakeWhatsAppLookup(new WhatsAppHistoryDossierDto(
            IsOptedIn: true,
            ConsentUpdatedAtUtc: DateTimeOffset.UtcNow.AddDays(-10),
            ConsentReason: "Opt-in via balcão",
            OutboundMessages: new List<OutboundMessageDossierDto>
            {
                new(DateTimeOffset.UtcNow.AddHours(-2), "WorkOrderNotification", "Delivered")
            }));

        var fakeAudit = new FakeAuditTrailService();

        var service = new CustomerDataExportService(
            repo,
            repo,
            repo,
            fakeWhatsApp,
            fakeAudit);

        var result = await service.ExportDossierAsync(
            tenantId,
            customerId,
            operatorId: Guid.CreateVersion7(),
            operatorEmail: "admin@lavaway.com.br",
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent");

        Assert.True(result.IsSuccess);
        var dossier = result.Value!;
        Assert.Equal(tenantId, dossier.TenantId);
        Assert.Equal("Carlos Eduardo", dossier.Profile.Name);
        Assert.Equal("11988887777", dossier.Profile.Phone);
        Assert.False(dossier.Profile.IsAnonymized);

        Assert.Single(dossier.Vehicles);
        Assert.Equal("ABC1D23", dossier.Vehicles[0].Plate);
        Assert.Equal("HatchSedan", dossier.Vehicles[0].SizeCategory);

        Assert.Single(dossier.WorkOrders);
        Assert.Equal(workOrderId, dossier.WorkOrders[0].WorkOrderId);
        Assert.Equal(70.00m, dossier.WorkOrders[0].TotalAmount);
        Assert.True(dossier.WorkOrders[0].IsPaid);
        Assert.NotNull(dossier.WorkOrders[0].Survey);
        Assert.Equal(5, dossier.WorkOrders[0].Survey!.Rating);
        Assert.Equal("Serviço impecável!", dossier.WorkOrders[0].Survey!.Feedback);

        Assert.True(dossier.CommunicationHistory.IsOptedIn);
        Assert.Single(dossier.CommunicationHistory.OutboundMessages);

        Assert.NotNull(dossier.LegalNotice);
        Assert.Equal("privacidade@lavaway.com.br", dossier.LegalNotice.DpoContactEmail);

        Assert.Single(fakeAudit.RecordedEvents);
        Assert.Equal(PlatformActionConstants.CustomerDataExported, fakeAudit.RecordedEvents[0].Action);
        Assert.Equal(customerId.ToString(), fakeAudit.RecordedEvents[0].TargetId);
    }

    [Fact]
    public async Task ExportDossierAsync_Should_ReturnNotFound_WhenCustomerDoesNotExist()
    {
        var tenantId = Guid.CreateVersion7();
        var repo = new FakeExportRepository();
        var service = new CustomerDataExportService(repo, repo, repo);

        var result = await service.ExportDossierAsync(tenantId, Guid.CreateVersion7());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("customer.not_found", result.Error.Code);
    }

    [Fact]
    public async Task ExportDossierAsync_Should_Succeed_WhenCustomerIsAnonymized()
    {
        var tenantId = Guid.CreateVersion7();
        var customerId = Guid.CreateVersion7();

        var customer = Customer.Create(tenantId, "Roberto Carlos", "11977776666").Value!;
        typeof(Customer).GetProperty(nameof(Customer.Id))!.SetValue(customer, customerId);
        customer.Anonymize("hash1234567890abcdef");

        var repo = new FakeExportRepository();
        repo.Customers.Add(customer);

        var service = new CustomerDataExportService(repo, repo, repo);

        var result = await service.ExportDossierAsync(tenantId, customerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(Customer.AnonymizedDefaultName, result.Value!.Profile.Name);
        Assert.Empty(result.Value.Profile.Phone);
        Assert.True(result.Value.Profile.IsAnonymized);
        Assert.NotNull(result.Value.Profile.AnonymizedAtUtc);
    }

    [Fact]
    public async Task ExportDossierPdfAsync_Should_GeneratePdfBytes_WhenCustomerExists()
    {
        var tenantId = Guid.CreateVersion7();
        var customerId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Fernanda Lima", "11966665555").Value!;
        typeof(Customer).GetProperty(nameof(Customer.Id))!.SetValue(customer, customerId);

        var repo = new FakeExportRepository();
        repo.Customers.Add(customer);

        var fakePdfGenerator = new FakeDossierPdfGenerator();
        var service = new CustomerDataExportService(
            repo,
            repo,
            repo,
            pdfGenerator: fakePdfGenerator);

        var result = await service.ExportDossierPdfAsync(tenantId, customerId);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value!);
        Assert.True(fakePdfGenerator.WasCalled);
    }

    private sealed class FakeExportRepository : ICustomerRepository, IVehicleRepository, IWorkOrderRepository
    {
        public List<Customer> Customers { get; } = [];
        public List<Vehicle> Vehicles { get; } = [];
        public List<WorkOrder> WorkOrders { get; } = [];

        Task<Customer?> ICustomerRepository.GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(Customers.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id));

        public Task<Customer?> GetByNormalizedPhoneAsync(Guid tenantId, string normalizedPhone, CancellationToken ct = default) =>
            Task.FromResult(Customers.FirstOrDefault(c => c.TenantId == tenantId && c.NormalizedPhone == normalizedPhone));

        public Task AddAsync(Customer customer, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(Customer customer) { }

        Task<Vehicle?> IVehicleRepository.GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(Vehicles.FirstOrDefault(v => v.TenantId == tenantId && v.Id == id));

        public Task<IReadOnlyCollection<Vehicle>> GetVehiclesByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<Vehicle>>(Vehicles.Where(v => v.TenantId == tenantId && v.CustomerId == customerId).ToList());

        public Task<bool> IsPlateRegisteredAsync(Guid tenantId, string normalizedPlate, CancellationToken ct = default) =>
            Task.FromResult(Vehicles.Any(v => v.TenantId == tenantId && v.Plate == normalizedPlate));

        public Task AddAsync(Vehicle vehicle, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<WorkOrder>> GetByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkOrder>>(WorkOrders.Where(w => w.TenantId == tenantId && w.CustomerId == customerId).ToList());

        Task<WorkOrder?> IWorkOrderRepository.GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(WorkOrders.FirstOrDefault(w => w.TenantId == tenantId && w.Id == id));

        public Task<IReadOnlyCollection<WorkOrder>> ListRecentAsync(Guid tenantId, int limit = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>([]);

        public Task<IReadOnlyCollection<WorkOrder>> ListActiveAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>([]);

        public Task<IReadOnlyCollection<WorkOrder>> GetWorkOrdersPendingSurveyAsync(Guid tenantId, DateTimeOffset cutoff, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>([]);

        public Task<WorkOrder?> GetLatestCompletedOrderByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default) =>
            Task.FromResult<WorkOrder?>(null);

        public Task<WorkOrder?> GetActiveOrderByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default) =>
            Task.FromResult<WorkOrder?>(null);

        public Task<IReadOnlyCollection<WorkOrder>> ListSurveysAsync(Guid tenantId, int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>([]);

        public Task AddAsync(WorkOrder workOrder, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(WorkOrder workOrder) { }
    }

    private sealed class FakeWhatsAppLookup(WhatsAppHistoryDossierDto history) : ICustomerWhatsAppHistoryLookup
    {
        public Task<Result<WhatsAppHistoryDossierDto>> GetCustomerWhatsAppHistoryAsync(Guid tenantId, string phone, CancellationToken ct = default) =>
            Task.FromResult(Result<WhatsAppHistoryDossierDto>.Success(history));
    }

    private sealed class FakeAuditTrailService : IAuditTrailService
    {
        public List<RecordAuditEventRequest> RecordedEvents { get; } = [];

        public Task<Result<Guid>> RecordEventAsync(RecordAuditEventRequest request, CancellationToken ct = default)
        {
            RecordedEvents.Add(request);
            return Task.FromResult(Result<Guid>.Success(Guid.CreateVersion7()));
        }
    }

    private sealed class FakeDossierPdfGenerator : ICustomerDossierPdfGenerator
    {
        public bool WasCalled { get; private set; }

        public Result<byte[]> GenerateDossierPdf(CustomerDossierDto dossier, StoreProfileDto? storeProfile)
        {
            WasCalled = true;
            return Result<byte[]>.Success([0x25, 0x50, 0x44, 0x46]); // %PDF
        }
    }
}
