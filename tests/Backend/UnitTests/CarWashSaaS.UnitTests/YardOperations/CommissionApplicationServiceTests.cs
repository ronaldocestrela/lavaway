using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class CommissionApplicationServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private sealed class FakeWorkOrderRepository : IWorkOrderRepository
    {
        public readonly List<WorkOrder> Orders = [];

        public Task<IReadOnlyCollection<WorkOrder>> ListForCommissionReportAsync(
            Guid tenantId,
            DateTimeOffset startUtc,
            DateTimeOffset endUtc,
            Guid? operatorId = null,
            CancellationToken ct = default)
        {
            var query = Orders.Where(o => o.TenantId == tenantId);
            if (operatorId.HasValue)
            {
                query = query.Where(o => o.AssignedOperatorId == operatorId.Value);
            }

            return Task.FromResult<IReadOnlyCollection<WorkOrder>>(query.ToList());
        }

        public Task<WorkOrder?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Orders.FirstOrDefault(o => o.TenantId == tenantId && o.Id == id));

        public Task<IReadOnlyCollection<WorkOrder>> ListRecentAsync(Guid tenantId, int limit = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>(Orders.Where(o => o.TenantId == tenantId).Take(limit).ToList());

        public Task<IReadOnlyCollection<WorkOrder>> ListActiveAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>(Orders.Where(o => o.TenantId == tenantId).ToList());

        public Task<IReadOnlyCollection<WorkOrder>> GetWorkOrdersPendingSurveyAsync(Guid tenantId, DateTimeOffset cutoff, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>([]);

        public Task<WorkOrder?> GetLatestCompletedOrderByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default) =>
            Task.FromResult<WorkOrder?>(null);

        public Task<WorkOrder?> GetActiveOrderByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default) =>
            Task.FromResult<WorkOrder?>(null);

        public Task<IReadOnlyCollection<WorkOrder>> ListSurveysAsync(Guid tenantId, int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>([]);

        public Task AddAsync(WorkOrder workOrder, CancellationToken ct = default)
        {
            Orders.Add(workOrder);
            return Task.CompletedTask;
        }

        public void Update(WorkOrder workOrder)
        {
            var idx = Orders.FindIndex(o => o.Id == workOrder.Id);
            if (idx >= 0) Orders[idx] = workOrder;
        }
    }

    private sealed class FakeTeamMemberRepository : ITeamMemberRepository
    {
        public readonly List<TeamMember> Members = [];

        public Task<TeamMember?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Members.FirstOrDefault(m => m.TenantId == tenantId && m.Id == id));

        public Task<TeamMember?> GetByEmailAsync(Guid tenantId, string? email, CancellationToken ct = default) =>
            Task.FromResult(Members.FirstOrDefault(m => m.TenantId == tenantId && m.Email == email));

        public Task<IReadOnlyCollection<TeamMember>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<TeamMember>>(Members.Where(m => m.TenantId == tenantId).ToList());

        public Task AddAsync(TeamMember teamMember, CancellationToken ct = default)
        {
            Members.Add(teamMember);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TeamMember teamMember, CancellationToken ct = default)
        {
            var idx = Members.FindIndex(m => m.Id == teamMember.Id);
            if (idx >= 0) Members[idx] = teamMember;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCommissionRuleRepository : ICommissionRuleRepository
    {
        public readonly List<CommissionRule> Rules = [];

        public Task<CommissionRule?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Rules.FirstOrDefault(r => r.TenantId == tenantId && r.Id == id));

        public Task<CommissionRule?> GetByServiceAndRoleAsync(Guid tenantId, string serviceName, string roleName, CancellationToken ct = default) =>
            Task.FromResult(Rules.FirstOrDefault(r => r.TenantId == tenantId &&
                string.Equals(r.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.RoleName, roleName, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyCollection<CommissionRule>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<CommissionRule>>(Rules.Where(r => r.TenantId == tenantId).ToList());

        public Task AddAsync(CommissionRule commissionRule, CancellationToken ct = default)
        {
            Rules.Add(commissionRule);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(CommissionRule commissionRule, CancellationToken ct = default)
        {
            var idx = Rules.FindIndex(r => r.Id == commissionRule.Id);
            if (idx >= 0) Rules[idx] = commissionRule;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(CommissionRule commissionRule, CancellationToken ct = default)
        {
            Rules.RemoveAll(r => r.Id == commissionRule.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        public readonly List<Customer> Customers = [];

        public Task<Customer?> GetByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default) =>
            Task.FromResult(Customers.FirstOrDefault(c => c.TenantId == tenantId && c.Id == customerId));

        public Task AddAsync(Customer customer, CancellationToken ct = default)
        {
            Customers.Add(customer);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeVehicleRepository : IVehicleRepository
    {
        public readonly List<Vehicle> Vehicles = [];

        public Task<Vehicle?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Vehicles.FirstOrDefault(v => v.TenantId == tenantId && v.Id == id));

        public Task<bool> IsPlateRegisteredAsync(Guid tenantId, string normalizedPlate, CancellationToken ct = default) =>
            Task.FromResult(Vehicles.Any(v => v.TenantId == tenantId && v.Plate == normalizedPlate));

        public Task AddAsync(Vehicle vehicle, CancellationToken ct = default)
        {
            Vehicles.Add(vehicle);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GetCommissionReport_CalculatesCommissionForAssignedOperators()
    {
        var workOrderRepo = new FakeWorkOrderRepository();
        var teamMemberRepo = new FakeTeamMemberRepository();
        var commissionRuleRepo = new FakeCommissionRuleRepository();
        var customerRepo = new FakeCustomerRepository();
        var vehicleRepo = new FakeVehicleRepository();

        var service = new CommissionApplicationService(
            workOrderRepo,
            teamMemberRepo,
            commissionRuleRepo,
            customerRepo,
            vehicleRepo);

        var operatorMember = TeamMember.Create(_tenantId, "Roberto Detailer", "Detailer", "roberto@lavaway.com").Value!;
        teamMemberRepo.Members.Add(operatorMember);

        commissionRuleRepo.Rules.AddRange([
            CommissionRule.Create(_tenantId, "Polimento Cristalizado", "Detailer", 20.0m).Value!,
            CommissionRule.Create(_tenantId, "Lavagem Técnica", "Detailer", 15.0m).Value!
        ]);

        var customer = Customer.Create(_tenantId, "Carlos Alberto", "11988887777").Value!;
        customerRepo.Customers.Add(customer);

        var vehicle = Vehicle.Create(_tenantId, customer.Id, "ABC1D23", VehicleSize.Suv).Value!;
        vehicleRepo.Vehicles.Add(vehicle);

        var item1 = WorkOrderItem.Create(_tenantId, Guid.NewGuid(), "Polimento Cristalizado", 500.00m, 180, 1).Value!;
        var item2 = WorkOrderItem.Create(_tenantId, Guid.NewGuid(), "Lavagem Técnica", 100.00m, 60, 1).Value!;

        var workOrder = WorkOrder.Create(_tenantId, customer.Id, vehicle.Id, [item1, item2]).Value!;
        workOrder.AssignOperator(operatorMember.Id, operatorMember.FullName);
        workOrder.MarkPaymentConfirmed(600.00m, PaymentMethodConstants.Pix, DateTimeOffset.UtcNow);
        workOrderRepo.Orders.Add(workOrder);

        var startDate = new DateOnly(2026, 10, 1);
        var endDate = new DateOnly(2026, 10, 5);

        var result = await service.GetCommissionReportAsync(_tenantId, startDate, endDate);

        Assert.True(result.IsSuccess);
        var report = result.Value!;
        Assert.Equal(600.00m, report.TotalRevenue);
        // Item 1: 500 * 20% = 100. Item 2: 100 * 15% = 15. Total = 115
        Assert.Equal(115.00m, report.TotalCommission);
        Assert.Equal(2, report.TotalServicesCount);

        var collaborator = Assert.Single(report.Collaborators);
        Assert.Equal("Roberto Detailer", collaborator.FullName);
        Assert.Equal(115.00m, collaborator.TotalCommission);
        Assert.Equal(2, collaborator.Items.Count);
    }

    [Fact]
    public async Task GetCommissionReport_WithInvalidDateRange_ReturnsValidationError()
    {
        var service = new CommissionApplicationService(
            new FakeWorkOrderRepository(),
            new FakeTeamMemberRepository(),
            new FakeCommissionRuleRepository(),
            new FakeCustomerRepository(),
            new FakeVehicleRepository());

        var startDate = new DateOnly(2026, 10, 10);
        var endDate = new DateOnly(2026, 10, 5);

        var result = await service.GetCommissionReportAsync(_tenantId, startDate, endDate);

        Assert.False(result.IsSuccess);
        Assert.Equal("commission.date_range.invalid", result.Error!.Code);
    }
}
