using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class AfterSalesApplicationServiceTests
{
    private readonly InMemoryWorkOrderRepository _workOrderRepo = new();
    private readonly InMemoryCustomerRepository _customerRepo = new();
    private readonly InMemoryVehicleRepository _vehicleRepo = new();
    private readonly InMemoryCampaignRepository _campaignRepo = new();
    private readonly FakePreferenceLookup _preferenceLookup = new();
    private readonly FakeWhatsAppDispatcher _dispatcher = new();
    private readonly FakeStoreProfileLookup _storeProfileLookup = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private readonly AfterSalesApplicationService _afterSalesService;
    private readonly FrequencyCappingService _frequencyCappingService;
    private readonly ReactivationCampaignApplicationService _campaignService;

    private readonly Guid _tenantId = Guid.NewGuid();

    public AfterSalesApplicationServiceTests()
    {
        _afterSalesService = new AfterSalesApplicationService(
            _workOrderRepo,
            _customerRepo,
            _vehicleRepo,
            _unitOfWork,
            _storeProfileLookup,
            _dispatcher,
            _preferenceLookup);

        _frequencyCappingService = new FrequencyCappingService(_campaignRepo, _preferenceLookup);

        _campaignService = new ReactivationCampaignApplicationService(
            _campaignRepo,
            _frequencyCappingService,
            _unitOfWork,
            _storeProfileLookup,
            _dispatcher);
    }

    [Fact]
    public async Task RegisterPickupAsync_Should_Set_PickedUpAtUtc_When_ReadyForPickup()
    {
        var (customer, vehicle, workOrder) = await SeedWorkOrderAsync(WorkOrderStatus.ReadyForPickup);

        var pickupTime = DateTimeOffset.UtcNow;
        var result = await _afterSalesService.RegisterPickupAsync(_tenantId, workOrder.Id, pickupTime, "Entregue ao cliente");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.PickedUpAtUtc);
        Assert.Equal(pickupTime, result.Value.PickedUpAtUtc);
    }

    [Fact]
    public async Task RegisterPickupAsync_Should_Fail_When_WorkOrder_Not_ReadyForPickup()
    {
        var (customer, vehicle, workOrder) = await SeedWorkOrderAsync(WorkOrderStatus.InWashing);

        var result = await _afterSalesService.RegisterPickupAsync(_tenantId, workOrder.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("work_order.not_ready_for_pickup", result.Error?.Code);
    }

    [Fact]
    public async Task ScanAndDispatchPendingSurveysAsync_Should_Dispatch_When_1Hour_Passed_And_OptedIn()
    {
        var (customer, vehicle, workOrder) = await SeedWorkOrderAsync(WorkOrderStatus.ReadyForPickup);
        workOrder.RegisterPickup(DateTimeOffset.UtcNow.AddMinutes(-70));

        var result = await _afterSalesService.ScanAndDispatchPendingSurveysAsync(_tenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        Assert.NotNull(workOrder.SurveySentAtUtc);
        Assert.Single(_dispatcher.SentMessages);
        Assert.Contains("1 a 5 estrelas", _dispatcher.SentMessages[0].Message);
    }

    [Fact]
    public async Task ScanAndDispatchPendingSurveysAsync_Should_Not_Dispatch_When_Under_1Hour()
    {
        var (customer, vehicle, workOrder) = await SeedWorkOrderAsync(WorkOrderStatus.ReadyForPickup);
        workOrder.RegisterPickup(DateTimeOffset.UtcNow.AddMinutes(-30)); // Apenas 30 min atrás

        var result = await _afterSalesService.ScanAndDispatchPendingSurveysAsync(_tenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
        Assert.Null(workOrder.SurveySentAtUtc);
        Assert.Empty(_dispatcher.SentMessages);
    }

    [Fact]
    public async Task ScanAndDispatchPendingSurveysAsync_Should_Skip_When_Customer_Opted_Out()
    {
        var (customer, vehicle, workOrder) = await SeedWorkOrderAsync(WorkOrderStatus.ReadyForPickup);
        workOrder.RegisterPickup(DateTimeOffset.UtcNow.AddMinutes(-90));
        _preferenceLookup.SetPreference(customer.Phone, isOptedIn: false);

        var result = await _afterSalesService.ScanAndDispatchPendingSurveysAsync(_tenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
        Assert.NotNull(workOrder.SurveySentAtUtc); // marcado como skipped
        Assert.Contains("Opt-out", workOrder.SurveyFeedback);
        Assert.Empty(_dispatcher.SentMessages);
    }

    [Fact]
    public async Task SubmitSurveyRatingAsync_Should_Record_Rating_And_Feedback()
    {
        var (customer, vehicle, workOrder) = await SeedWorkOrderAsync(WorkOrderStatus.ReadyForPickup);
        workOrder.RegisterPickup(DateTimeOffset.UtcNow.AddHours(-2));
        workOrder.RecordSatisfactionSurveySent(DateTimeOffset.UtcNow.AddHours(-1));

        var result = await _afterSalesService.SubmitSurveyRatingAsync(_tenantId, customer.Phone, 5, "Serviço perfeito!");

        Assert.True(result.IsSuccess);
        Assert.Equal(5, workOrder.SurveyRating);
        Assert.Equal("Serviço perfeito!", workOrder.SurveyFeedback);
        Assert.NotNull(workOrder.SurveyRespondedAtUtc);
    }

    [Fact]
    public async Task GetMetricsAsync_Should_Calculate_Average_And_Distribution()
    {
        var (c1, v1, o1) = await SeedWorkOrderAsync(WorkOrderStatus.ReadyForPickup);
        o1.RegisterPickup(DateTimeOffset.UtcNow.AddDays(-2));
        o1.RecordSatisfactionSurveySent(DateTimeOffset.UtcNow.AddDays(-2));
        o1.RecordSatisfactionRating(5);

        var (c2, v2, o2) = await SeedWorkOrderAsync(WorkOrderStatus.ReadyForPickup);
        o2.RegisterPickup(DateTimeOffset.UtcNow.AddDays(-1));
        o2.RecordSatisfactionSurveySent(DateTimeOffset.UtcNow.AddDays(-1));
        o2.RecordSatisfactionRating(3);

        var metricsResult = await _afterSalesService.GetMetricsAsync(_tenantId);

        Assert.True(metricsResult.IsSuccess);
        var metrics = metricsResult.Value!;
        Assert.Equal(2, metrics.TotalSurveysSent);
        Assert.Equal(2, metrics.TotalSurveysResponded);
        Assert.Equal(100m, metrics.ResponseRatePercentage);
        Assert.Equal(4.0m, metrics.AverageRating);
        Assert.Equal(1, metrics.FiveStarCount);
        Assert.Equal(1, metrics.ThreeStarCount);
    }

    [Fact]
    public async Task FrequencyCappingService_Should_Block_When_Cooling_Period_Active()
    {
        var customerId = Guid.NewGuid();
        var phone = "11999991111";

        // Registra log recente de 3 dias atrás
        await _campaignRepo.AddLogAsync(ReactivationCampaignLog.Create(
            _tenantId, Guid.NewGuid(), customerId, phone, 15, "key-1", DateTimeOffset.UtcNow.AddDays(-3)).Value!);

        var check = await _frequencyCappingService.CanSendMarketingMessageAsync(_tenantId, customerId, phone, 15);

        Assert.False(check.IsAllowed);
        Assert.Equal("frequency_cooling_period", check.RejectionCode);
    }

    [Fact]
    public async Task FrequencyCappingService_Should_Block_When_Customer_Opted_Out()
    {
        var customerId = Guid.NewGuid();
        var phone = "11999992222";
        _preferenceLookup.SetPreference(phone, isOptedIn: false);

        var check = await _frequencyCappingService.CanSendMarketingMessageAsync(_tenantId, customerId, phone, 15);

        Assert.False(check.IsAllowed);
        Assert.Equal("opted_out", check.RejectionCode);
    }

    [Fact]
    public async Task ReactivationCampaignService_Should_AutoCreate_DefaultRules_And_Dispatch()
    {
        var rulesResult = await _campaignService.GetRulesAsync(_tenantId);
        Assert.True(rulesResult.IsSuccess);
        Assert.Equal(3, rulesResult.Value!.Count); // 15, 30, 45 dias

        // Seed customer inativo há 20 dias (Tier 15)
        var customer = Customer.Create(_tenantId, "Roberto Silva", "11988887777").Value!;
        await _customerRepo.AddAsync(customer);

        _campaignRepo.SeedInactiveCustomer(new CustomerLastVisitInfo(
            customer.Id,
            customer.Name,
            customer.Phone,
            Guid.NewGuid(),
            "ABC1D23",
            "Civic",
            DateTimeOffset.UtcNow.AddDays(-20),
            20));

        var dispatchResult = await _campaignService.ScanAndDispatchCampaignsAsync(_tenantId);

        Assert.True(dispatchResult.IsSuccess);
        Assert.Equal(1, dispatchResult.Value!.TotalDispatched);
        Assert.Single(_dispatcher.SentMessages);
        Assert.Contains("Roberto Silva", _dispatcher.SentMessages[0].Message);
        Assert.Contains("Civic", _dispatcher.SentMessages[0].Message);
    }

    private async Task<(Customer, Vehicle, WorkOrder)> SeedWorkOrderAsync(WorkOrderStatus status)
    {
        var customer = Customer.Create(_tenantId, "Cliente Teste", "11999990000").Value!;
        await _customerRepo.AddAsync(customer);

        var vehicle = Vehicle.Create(_tenantId, customer.Id, "ABC1234", VehicleSize.HatchSedan).Value!;
        await _vehicleRepo.AddAsync(vehicle);

        var item = WorkOrderItem.Create(_tenantId, Guid.NewGuid(), "Lavagem", 50m, 40, 1, ServiceCategoryConstants.LavagemCompleta).Value!;
        var order = WorkOrder.Create(_tenantId, customer.Id, vehicle.Id, [item]).Value!;

        if (status != WorkOrderStatus.Waiting)
        {
            if (status is WorkOrderStatus.InWashing or WorkOrderStatus.Finishing or WorkOrderStatus.QualityControl or WorkOrderStatus.ReadyForPickup)
                order.ChangeStatus(WorkOrderStatus.InWashing);
            if (status is WorkOrderStatus.Finishing or WorkOrderStatus.QualityControl or WorkOrderStatus.ReadyForPickup)
                order.ChangeStatus(WorkOrderStatus.Finishing);
            if (status is WorkOrderStatus.QualityControl or WorkOrderStatus.ReadyForPickup)
                order.ChangeStatus(WorkOrderStatus.QualityControl);
            if (status is WorkOrderStatus.ReadyForPickup)
                order.ChangeStatus(WorkOrderStatus.ReadyForPickup);
        }

        await _workOrderRepo.AddAsync(order);
        return (customer, vehicle, order);
    }

    // Fakes
    private sealed class InMemoryWorkOrderRepository : IWorkOrderRepository
    {
        private readonly List<WorkOrder> _orders = [];

        public Task<WorkOrder?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(_orders.FirstOrDefault(o => o.TenantId == tenantId && o.Id == id));

        public Task<IReadOnlyCollection<WorkOrder>> ListRecentAsync(Guid tenantId, int limit = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>(_orders.Where(o => o.TenantId == tenantId).Take(limit).ToList());

        public Task<IReadOnlyCollection<WorkOrder>> ListActiveAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>(_orders.Where(o => o.TenantId == tenantId && (!o.PickedUpAtUtc.HasValue || o.Status != WorkOrderStatus.ReadyForPickup)).ToList());

        public Task<IReadOnlyCollection<WorkOrder>> GetWorkOrdersPendingSurveyAsync(Guid tenantId, DateTimeOffset cutoff, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>(_orders
                .Where(o => o.TenantId == tenantId && o.PickedUpAtUtc.HasValue && o.PickedUpAtUtc.Value <= cutoff && !o.SurveySentAtUtc.HasValue)
                .ToList());

        public Task<WorkOrder?> GetLatestCompletedOrderByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default) =>
            Task.FromResult(_orders
                .Where(o => o.TenantId == tenantId && o.PickedUpAtUtc.HasValue)
                .OrderByDescending(o => o.PickedUpAtUtc)
                .FirstOrDefault());

        public Task<IReadOnlyCollection<WorkOrder>> ListSurveysAsync(Guid tenantId, int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>(_orders.Where(o => o.TenantId == tenantId && o.SurveySentAtUtc.HasValue).Take(limit).ToList());

        public Task AddAsync(WorkOrder workOrder, CancellationToken ct = default)
        {
            _orders.Add(workOrder);
            return Task.CompletedTask;
        }

        public void Update(WorkOrder workOrder) { }
    }

    private sealed class InMemoryCustomerRepository : ICustomerRepository
    {
        private readonly List<Customer> _customers = [];

        public Task<Customer?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(_customers.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id));

        public Task<Customer?> GetByPhoneAsync(Guid tenantId, string phone, CancellationToken ct = default) =>
            Task.FromResult(_customers.FirstOrDefault(c => c.TenantId == tenantId && (c.Phone == phone || c.NormalizedPhone == Customer.NormalizePhone(phone))));

        public Task AddAsync(Customer customer, CancellationToken ct = default)
        {
            _customers.Add(customer);
            return Task.CompletedTask;
        }

        public void Update(Customer customer) { }
    }

    private sealed class InMemoryVehicleRepository : IVehicleRepository
    {
        private readonly List<Vehicle> _vehicles = [];

        public Task<Vehicle?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(_vehicles.FirstOrDefault(v => v.TenantId == tenantId && v.Id == id));

        public Task<Vehicle?> GetByPlateAsync(Guid tenantId, string plate, CancellationToken ct = default) =>
            Task.FromResult(_vehicles.FirstOrDefault(v => v.TenantId == tenantId && v.Plate == plate));

        public Task<IReadOnlyCollection<Vehicle>> ListByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<Vehicle>>(_vehicles.Where(v => v.TenantId == tenantId && v.CustomerId == customerId).ToList());

        public Task<bool> IsPlateRegisteredAsync(Guid tenantId, string plate, CancellationToken ct = default) =>
            Task.FromResult(_vehicles.Any(v => v.TenantId == tenantId && v.Plate == plate));

        public Task AddAsync(Vehicle vehicle, CancellationToken ct = default)
        {
            _vehicles.Add(vehicle);
            return Task.CompletedTask;
        }

        public void Update(Vehicle vehicle) { }
    }

    private sealed class InMemoryCampaignRepository : IReactivationCampaignRepository
    {
        private readonly List<ReactivationCampaignRule> _rules = [];
        private readonly List<ReactivationCampaignLog> _logs = [];
        private readonly List<CustomerLastVisitInfo> _inactiveCustomers = [];

        public void SeedInactiveCustomer(CustomerLastVisitInfo info) => _inactiveCustomers.Add(info);

        public Task<IReadOnlyList<ReactivationCampaignRule>> GetRulesAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ReactivationCampaignRule>>(_rules.Where(r => r.TenantId == tenantId).ToList());

        public Task<ReactivationCampaignRule?> GetRuleByIdAsync(Guid tenantId, Guid ruleId, CancellationToken ct = default) =>
            Task.FromResult(_rules.FirstOrDefault(r => r.TenantId == tenantId && r.Id == ruleId));

        public Task<ReactivationCampaignRule?> GetRuleByDaysAsync(Guid tenantId, int daysInactive, CancellationToken ct = default) =>
            Task.FromResult(_rules.FirstOrDefault(r => r.TenantId == tenantId && r.DaysInactive == daysInactive));

        public Task AddRuleAsync(ReactivationCampaignRule rule, CancellationToken ct = default)
        {
            _rules.Add(rule);
            return Task.CompletedTask;
        }

        public void UpdateRule(ReactivationCampaignRule rule) { }

        public Task<IReadOnlyList<CustomerLastVisitInfo>> GetInactiveCustomersAsync(Guid tenantId, int minDaysInactive, int? maxDaysInactive, CancellationToken ct = default)
        {
            var result = _inactiveCustomers
                .Where(c => c.DaysInactive >= minDaysInactive && (!maxDaysInactive.HasValue || c.DaysInactive <= maxDaysInactive.Value))
                .ToList();
            return Task.FromResult<IReadOnlyList<CustomerLastVisitInfo>>(result);
        }

        public Task<bool> HasRecentCampaignLogAsync(Guid tenantId, Guid customerId, TimeSpan cooldown, CancellationToken ct = default)
        {
            var cutoff = DateTimeOffset.UtcNow - cooldown;
            return Task.FromResult(_logs.Any(l => l.TenantId == tenantId && l.CustomerId == customerId && l.SentAtUtc >= cutoff));
        }

        public Task<bool> HasCampaignLogForCycleAsync(Guid tenantId, Guid customerId, string idempotencyKey, CancellationToken ct = default) =>
            Task.FromResult(_logs.Any(l => l.TenantId == tenantId && l.CustomerId == customerId && l.IdempotencyKey == idempotencyKey));

        public Task AddLogAsync(ReactivationCampaignLog log, CancellationToken ct = default)
        {
            _logs.Add(log);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePreferenceLookup : ICustomerCommunicationPreferenceLookup
    {
        private readonly Dictionary<string, bool> _preferences = new();

        public void SetPreference(string phone, bool isOptedIn) => _preferences[phone] = isOptedIn;

        public Task<Result<CustomerCommunicationPreferenceDto?>> GetPreferenceAsync(Guid tenantId, string phone, CancellationToken ct = default)
        {
            if (_preferences.TryGetValue(phone, out var isOptedIn))
            {
                return Task.FromResult(Result<CustomerCommunicationPreferenceDto?>.Success(
                    new CustomerCommunicationPreferenceDto(Guid.NewGuid(), tenantId, phone, isOptedIn, isOptedIn ? null : DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow)));
            }

            return Task.FromResult(Result<CustomerCommunicationPreferenceDto?>.Success(
                new CustomerCommunicationPreferenceDto(Guid.NewGuid(), tenantId, phone, true, null, null, DateTimeOffset.UtcNow)));
        }

        public Task<Result<CustomerCommunicationPreferenceDto>> UpdatePreferenceAsync(Guid tenantId, string phone, bool isOptedIn, string? reason = null, CancellationToken ct = default)
        {
            _preferences[phone] = isOptedIn;
            return Task.FromResult(Result<CustomerCommunicationPreferenceDto>.Success(
                new CustomerCommunicationPreferenceDto(Guid.NewGuid(), tenantId, phone, isOptedIn, isOptedIn ? null : DateTimeOffset.UtcNow, reason, DateTimeOffset.UtcNow)));
        }

        public Task<Result<IReadOnlyList<CustomerCommunicationPreferenceDto>>> ListPreferencesAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<IReadOnlyList<CustomerCommunicationPreferenceDto>>.Success([]));
    }

    private sealed class FakeWhatsAppDispatcher : IOutboundWhatsAppDispatcher
    {
        public List<(string Phone, string Message)> SentMessages { get; } = [];

        public Task<Result<WhatsAppMessageDto>> DispatchTextMessageAsync(Guid tenantId, string recipientPhone, string messageText, string? idempotencyKey = null, CancellationToken ct = default)
        {
            SentMessages.Add((recipientPhone, messageText));
            return Task.FromResult(Result<WhatsAppMessageDto>.Success(
                new WhatsAppMessageDto(Guid.NewGuid(), recipientPhone, messageText, "Sent", null, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null)));
        }

        public Task<Result<WhatsAppMessageDto>> DispatchMediaMessageAsync(Guid tenantId, string recipientPhone, string caption, string mediaType, string mediaUrlOrBase64, string? mediaMimeType = null, string? mediaFileName = null, string? idempotencyKey = null, CancellationToken ct = default) =>
            Task.FromResult(Result<WhatsAppMessageDto>.Success(
                new WhatsAppMessageDto(Guid.NewGuid(), recipientPhone, caption, "Sent", null, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null)));

        public Task<Result<IReadOnlyList<WhatsAppMessageDto>>> GetRecentMessagesAsync(Guid tenantId, int limit = 20, CancellationToken ct = default) =>
            Task.FromResult(Result<IReadOnlyList<WhatsAppMessageDto>>.Success([]));
    }

    private sealed class FakeStoreProfileLookup : ITenantStoreProfileLookup
    {
        public Task<Result<StoreProfileDto>> GetProfileAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<StoreProfileDto>.Success(new StoreProfileDto(Guid.NewGuid(), tenantId, "Lavaway Razao Social", "Lavaway Master", "12.345.678/0001-90", "11999990000", "Rua das Flores", "São Paulo", "SP", "01234-000", null, null, null)));
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<Result<int>> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(Result<int>.Success(1));
    }
}
