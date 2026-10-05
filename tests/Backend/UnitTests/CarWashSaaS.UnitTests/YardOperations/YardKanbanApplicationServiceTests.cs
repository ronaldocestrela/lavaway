using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class YardKanbanApplicationServiceTests
{
    [Fact]
    public async Task ChangeStatusAsync_ShouldAdvanceStatus_AndNotifyRealtimeChannel()
    {
        var tenantId = Guid.CreateVersion7();
        var env = new FakeYardOperationsEnvironment(tenantId);
        var operatorMember = TeamMember.Create(tenantId, "Roberto Operador", "Lavador", "roberto@lavajato.com").Value!;
        env.TeamMembers.Add(operatorMember);

        var workOrder = env.CreateWorkOrder("ABC1D23");

        var appService = env.CreateApplicationService();

        var request = new ChangeWorkOrderStatusRequest("InWashing", operatorMember.Id);
        var result = await appService.ChangeStatusAsync(tenantId, workOrder.Id, request);

        Assert.True(result.IsSuccess);
        Assert.Equal("InWashing", result.Value!.Status);
        Assert.Equal(operatorMember.Id, result.Value!.AssignedOperatorId);
        Assert.Equal("Roberto Operador", result.Value!.AssignedOperatorName);

        Assert.Single(env.MovedNotifications);
        var notification = env.MovedNotifications[0];
        Assert.Equal(workOrder.Id, notification.WorkOrderId);
        Assert.Equal("Waiting", notification.FromStatus);
        Assert.Equal("InWashing", notification.ToStatus);
        Assert.Equal(operatorMember.Id, notification.OperatorId);
    }

    [Fact]
    public async Task ChangeStatusAsync_ShouldRejectInactiveOperator()
    {
        var tenantId = Guid.CreateVersion7();
        var env = new FakeYardOperationsEnvironment(tenantId);
        var inactiveMember = TeamMember.Create(tenantId, "Inativo Silva", "Lavador", "inativo@lavajato.com").Value!;
        inactiveMember.Deactivate();
        env.TeamMembers.Add(inactiveMember);

        var workOrder = env.CreateWorkOrder("ABC1D23");

        var appService = env.CreateApplicationService();

        var request = new ChangeWorkOrderStatusRequest("InWashing", inactiveMember.Id);
        var result = await appService.ChangeStatusAsync(tenantId, workOrder.Id, request);

        Assert.False(result.IsSuccess);
        Assert.Equal("team_member.inactive", result.Error!.Code);
    }

    [Fact]
    public async Task AssignOperatorAsync_ShouldAssignActiveOperatorSuccessfully()
    {
        var tenantId = Guid.CreateVersion7();
        var env = new FakeYardOperationsEnvironment(tenantId);
        var operatorMember = TeamMember.Create(tenantId, "Lucas Secador", "Secador", "lucas@lavajato.com").Value!;
        env.TeamMembers.Add(operatorMember);

        var workOrder = env.CreateWorkOrder("ABC1D23");
        var appService = env.CreateApplicationService();

        var result = await appService.AssignOperatorAsync(tenantId, workOrder.Id, operatorMember.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(operatorMember.Id, result.Value!.AssignedOperatorId);
        Assert.Equal("Lucas Secador", result.Value!.AssignedOperatorName);
        Assert.Single(env.AssignedOperatorNotifications);
    }

    [Fact]
    public async Task GetKanbanBoardAsync_ShouldGroupCardsIntoAllFiveColumns()
    {
        var tenantId = Guid.CreateVersion7();
        var env = new FakeYardOperationsEnvironment(tenantId);
        env.YardCapacities.Add(YardCapacity.Create(tenantId, 6, "Pátio Central").Value!);

        var wo1 = env.CreateWorkOrder("AAA1111");
        var wo2 = env.CreateWorkOrder("BBB2222");
        wo2.ChangeStatus(WorkOrderStatus.InWashing);

        var appService = env.CreateApplicationService();
        var result = await appService.GetKanbanBoardAsync(tenantId);

        Assert.True(result.IsSuccess);
        var board = result.Value!;
        Assert.Equal(5, board.Columns.Count);
        Assert.Equal(6, board.CapacityTotalBoxes);
        Assert.Equal(1, board.OccupiedBoxes); // 1 in InWashing
        Assert.Equal(2, board.TotalActiveOrders);

        var waitingCol = board.Columns.First(c => c.Status == "Waiting");
        Assert.Equal(1, waitingCol.Count);
        Assert.Equal("AAA1111", waitingCol.Cards[0].Plate);

        var washingCol = board.Columns.First(c => c.Status == "InWashing");
        Assert.Equal(1, washingCol.Count);
        Assert.Equal("BBB2222", washingCol.Cards[0].Plate);
    }

    [Fact]
    public async Task GetStatusHistoryAsync_ShouldReturnChronologicalHistory()
    {
        var tenantId = Guid.CreateVersion7();
        var env = new FakeYardOperationsEnvironment(tenantId);
        var workOrder = env.CreateWorkOrder("ABC1D23");
        workOrder.ChangeStatus(WorkOrderStatus.InWashing);
        workOrder.ChangeStatus(WorkOrderStatus.Finishing);

        var appService = env.CreateApplicationService();
        var result = await appService.GetStatusHistoryAsync(tenantId, workOrder.Id);

        Assert.True(result.IsSuccess);
        var history = result.Value!;
        Assert.Equal(3, history.Count); // Initial Waiting + InWashing + Finishing
        Assert.Null(history.First().FromStatus);
        Assert.Equal("Waiting", history.First().ToStatus);
        Assert.Equal("Finishing", history.Last().ToStatus);
    }

    private sealed class FakeYardOperationsEnvironment :
        IWorkOrderRepository,
        ICustomerRepository,
        IVehicleRepository,
        IServiceRepository,
        ITeamMemberRepository,
        IYardCapacityRepository,
        IYardRealtimeNotifier,
        IUnitOfWork
    {
        private readonly Guid _tenantId;

        public FakeYardOperationsEnvironment(Guid tenantId)
        {
            _tenantId = tenantId;
        }

        public List<WorkOrder> WorkOrders { get; } = [];
        public List<Customer> Customers { get; } = [];
        public List<Vehicle> Vehicles { get; } = [];
        public List<Service> Services { get; } = [];
        public List<TeamMember> TeamMembers { get; } = [];
        public List<YardCapacity> YardCapacities { get; } = [];
        public List<WorkOrderMovedNotification> MovedNotifications { get; } = [];
        public List<(Guid WorkOrderId, Guid? OperatorId, string? OperatorName)> AssignedOperatorNotifications { get; } = [];

        public WorkOrder CreateWorkOrder(string plate)
        {
            var customer = Customer.Create(_tenantId, "Cliente Teste", "11988880000").Value!;
            var vehicle = Vehicle.Create(_tenantId, customer.Id, plate, VehicleSize.HatchSedan).Value!;
            Customers.Add(customer);
            Vehicles.Add(vehicle);

            var price = ServicePrice.Create(_tenantId, VehicleSize.HatchSedan, 60m, 40).Value!;
            var service = Service.Create(_tenantId, "Ducha Rápida", "Lavagem", [price]).Value!;
            Services.Add(service);

            var item = WorkOrderItem.Create(_tenantId, service.Id, service.Name, 60m, 40, 1).Value!;
            var wo = WorkOrder.Create(_tenantId, customer.Id, vehicle.Id, [item]).Value!;
            WorkOrders.Add(wo);
            return wo;
        }

        public WorkOrderApplicationService CreateApplicationService() =>
            new(this, this, this, this, this, this, this, this);

        // IWorkOrderRepository
        Task<WorkOrder?> IWorkOrderRepository.GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(WorkOrders.FirstOrDefault(w => w.TenantId == tenantId && w.Id == id));

        public Task<IReadOnlyCollection<WorkOrder>> ListRecentAsync(Guid tenantId, int limit = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>(WorkOrders.Where(w => w.TenantId == tenantId).Take(limit).ToList());

        public Task<IReadOnlyCollection<WorkOrder>> ListActiveAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>(WorkOrders.Where(w => w.TenantId == tenantId).ToList());

        public Task<IReadOnlyCollection<WorkOrder>> GetWorkOrdersPendingSurveyAsync(Guid tenantId, DateTimeOffset cutoff, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>([]);

        public Task<WorkOrder?> GetLatestCompletedOrderByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default) =>
            Task.FromResult<WorkOrder?>(null);

        public Task<IReadOnlyCollection<WorkOrder>> ListSurveysAsync(Guid tenantId, int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>([]);

        Task IWorkOrderRepository.AddAsync(WorkOrder workOrder, CancellationToken ct)
        {
            WorkOrders.Add(workOrder);
            return Task.CompletedTask;
        }

        void IWorkOrderRepository.Update(WorkOrder workOrder)
        {
        }

        // ICustomerRepository
        Task<Customer?> ICustomerRepository.GetByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct) =>
            Task.FromResult(Customers.FirstOrDefault(c => c.TenantId == tenantId && c.Id == customerId));

        Task ICustomerRepository.AddAsync(Customer customer, CancellationToken ct) => Task.CompletedTask;

        // IVehicleRepository
        Task<Vehicle?> IVehicleRepository.GetByIdAsync(Guid tenantId, Guid vehicleId, CancellationToken ct) =>
            Task.FromResult(Vehicles.FirstOrDefault(v => v.TenantId == tenantId && v.Id == vehicleId));

        public Task<bool> IsPlateRegisteredAsync(Guid tenantId, string normalizedPlate, CancellationToken ct = default) =>
            Task.FromResult(Vehicles.Any(v => v.TenantId == tenantId && v.Plate == normalizedPlate));

        Task IVehicleRepository.AddAsync(Vehicle vehicle, CancellationToken ct) => Task.CompletedTask;

        // IServiceRepository
        Task<IReadOnlyCollection<Service>> IServiceRepository.ListByTenantAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<Service>>(Services);

        Task<Service?> IServiceRepository.GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(Services.FirstOrDefault(s => s.TenantId == tenantId && s.Id == id));

        Task IServiceRepository.AddAsync(Service service, CancellationToken ct) => Task.CompletedTask;
        Task IServiceRepository.UpdateAsync(Service service, CancellationToken ct) => Task.CompletedTask;

        // ITeamMemberRepository
        Task<TeamMember?> ITeamMemberRepository.GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(TeamMembers.FirstOrDefault(m => m.TenantId == tenantId && m.Id == id));

        Task<TeamMember?> ITeamMemberRepository.GetByEmailAsync(Guid tenantId, string email, CancellationToken ct) =>
            Task.FromResult(TeamMembers.FirstOrDefault(m => m.TenantId == tenantId && m.Email == email));

        Task<IReadOnlyCollection<TeamMember>> ITeamMemberRepository.ListByTenantAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<TeamMember>>(TeamMembers.Where(m => m.TenantId == tenantId).ToList());

        public Task<bool> IsEmailInUseAsync(Guid tenantId, string email, Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(false);

        Task ITeamMemberRepository.AddAsync(TeamMember teamMember, CancellationToken ct) => Task.CompletedTask;
        Task ITeamMemberRepository.UpdateAsync(TeamMember teamMember, CancellationToken ct) => Task.CompletedTask;

        // IYardCapacityRepository
        public Task<YardCapacity?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(YardCapacities.FirstOrDefault(c => c.TenantId == tenantId));

        public Task AddAsync(YardCapacity capacity, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(YardCapacity capacity, CancellationToken ct = default) => Task.CompletedTask;

        // IYardRealtimeNotifier
        public Task NotifyWorkOrderMovedAsync(Guid tenantId, WorkOrderMovedNotification notification, CancellationToken ct = default)
        {
            MovedNotifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task NotifyOperatorAssignedAsync(Guid tenantId, Guid workOrderId, Guid? operatorId, string? operatorName, CancellationToken ct = default)
        {
            AssignedOperatorNotifications.Add((workOrderId, operatorId, operatorName));
            return Task.CompletedTask;
        }

        // IUnitOfWork
        public Task<Result<int>> SaveChangesAsync(CancellationToken ct = default) =>
            Task.FromResult(Result<int>.Success(1));
    }
}
