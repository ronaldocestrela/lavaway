using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed class CommissionApplicationService(
    IWorkOrderRepository workOrderRepository,
    ITeamMemberRepository teamMemberRepository,
    ICommissionRuleRepository commissionRuleRepository,
    ICustomerRepository customerRepository,
    IVehicleRepository vehicleRepository) : ICommissionCalculationLookup
{
    public async Task<Result<CommissionReportDto>> GetCommissionReportAsync(
        Guid tenantId,
        DateOnly startDate,
        DateOnly endDate,
        Guid? teamMemberId = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CommissionReportDto>.Failure(new Error("commission.tenant.required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (endDate < startDate)
        {
            return Result<CommissionReportDto>.Failure(new Error("commission.date_range.invalid", "A data final não pode ser anterior à data inicial.", ErrorType.Validation));
        }

        var startUtc = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var teamMembers = await teamMemberRepository.ListByTenantAsync(tenantId, ct);
        var teamMembersMap = teamMembers.ToDictionary(m => m.Id);

        if (teamMemberId.HasValue && !teamMembersMap.ContainsKey(teamMemberId.Value))
        {
            return Result<CommissionReportDto>.Failure(new Error("commission.team_member.not_found", "Colaborador não encontrado.", ErrorType.NotFound));
        }

        var commissionRules = await commissionRuleRepository.ListByTenantAsync(tenantId, ct);
        var workOrders = await workOrderRepository.ListForCommissionReportAsync(tenantId, startUtc, endUtc, teamMemberId, ct);

        var customerCache = new Dictionary<Guid, Customer?>();
        var vehicleCache = new Dictionary<Guid, Vehicle?>();

        var itemsByOperator = new Dictionary<Guid, List<CommissionItemDetailDto>>();

        foreach (var order in workOrders)
        {
            if (!order.AssignedOperatorId.HasValue) continue;
            var operatorId = order.AssignedOperatorId.Value;

            if (!teamMembersMap.TryGetValue(operatorId, out var member)) continue;

            if (!customerCache.TryGetValue(order.CustomerId, out var customer))
            {
                customer = await customerRepository.GetByIdAsync(tenantId, order.CustomerId, ct);
                customerCache[order.CustomerId] = customer;
            }

            if (!vehicleCache.TryGetValue(order.VehicleId, out var vehicle))
            {
                vehicle = await vehicleRepository.GetByIdAsync(tenantId, order.VehicleId, ct);
                vehicleCache[order.VehicleId] = vehicle;
            }

            var customerName = customer?.Name ?? "Cliente";
            var licensePlate = vehicle?.Plate ?? "---";
            var orderCompletedAt = order.PaidAtUtc ?? order.CreatedAtUtc;

            if (!itemsByOperator.TryGetValue(operatorId, out var list))
            {
                list = [];
                itemsByOperator[operatorId] = list;
            }

            foreach (var item in order.Items)
            {
                var (percentage, commissionAmount) = CommissionCalculator.CalculateItemCommission(item, member.Role, commissionRules);

                list.Add(new CommissionItemDetailDto(
                    order.Id,
                    customerName,
                    licensePlate,
                    item.ServiceName,
                    item.TotalAmount,
                    percentage,
                    commissionAmount,
                    orderCompletedAt));
            }
        }

        var collaborators = new List<TeamMemberCommissionSummaryDto>();
        decimal overallRevenue = 0m;
        decimal overallCommission = 0m;
        int overallServicesCount = 0;

        var targetMembers = teamMemberId.HasValue
            ? teamMembers.Where(m => m.Id == teamMemberId.Value)
            : teamMembers;

        foreach (var member in targetMembers.OrderBy(m => m.FullName))
        {
            itemsByOperator.TryGetValue(member.Id, out var operatorItems);
            operatorItems ??= [];

            var totalRevenue = operatorItems.Sum(i => i.ServiceAmount);
            var totalCommission = operatorItems.Sum(i => i.CommissionAmount);
            var servicesCount = operatorItems.Count;

            overallRevenue += totalRevenue;
            overallCommission += totalCommission;
            overallServicesCount += servicesCount;

            collaborators.Add(new TeamMemberCommissionSummaryDto(
                member.Id,
                member.FullName,
                member.Role,
                servicesCount,
                totalRevenue,
                totalCommission,
                operatorItems));
        }

        return Result<CommissionReportDto>.Success(new CommissionReportDto(
            startDate,
            endDate,
            overallRevenue,
            overallCommission,
            overallServicesCount,
            collaborators));
    }
}
