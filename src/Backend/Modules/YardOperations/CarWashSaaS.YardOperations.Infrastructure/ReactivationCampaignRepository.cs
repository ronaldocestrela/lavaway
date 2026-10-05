using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class ReactivationCampaignRepository(YardOperationsDbContext dbContext) : IReactivationCampaignRepository
{
    public async Task<IReadOnlyList<ReactivationCampaignRule>> GetRulesAsync(Guid tenantId, CancellationToken ct = default) =>
        await dbContext.ReactivationCampaignRules
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.DaysInactive)
            .ToListAsync(ct);

    public async Task<ReactivationCampaignRule?> GetRuleByIdAsync(Guid tenantId, Guid ruleId, CancellationToken ct = default) =>
        await dbContext.ReactivationCampaignRules
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == ruleId, ct);

    public async Task<ReactivationCampaignRule?> GetRuleByDaysAsync(Guid tenantId, int daysInactive, CancellationToken ct = default) =>
        await dbContext.ReactivationCampaignRules
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.DaysInactive == daysInactive, ct);

    public async Task AddRuleAsync(ReactivationCampaignRule rule, CancellationToken ct = default) =>
        await dbContext.ReactivationCampaignRules.AddAsync(rule, ct);

    public void UpdateRule(ReactivationCampaignRule rule) =>
        dbContext.ReactivationCampaignRules.Update(rule);

    public async Task<IReadOnlyList<CustomerLastVisitInfo>> GetInactiveCustomersAsync(
        Guid tenantId,
        int minDaysInactive,
        int? maxDaysInactive,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.DateTime);

        // 1. Clientes com ordens ativas no pátio ou agendamentos futuros não são ausentes
        var customersWithActiveWorkOrders = await dbContext.WorkOrders
            .Where(w => w.TenantId == tenantId && (!w.PickedUpAtUtc.HasValue || w.Status != WorkOrderStatus.ReadyForPickup))
            .Select(w => w.CustomerId)
            .Distinct()
            .ToListAsync(ct);

        var customersWithFutureBookings = await dbContext.Bookings
            .Where(b => b.TenantId == tenantId &&
                        b.ScheduledDate >= today &&
                        b.Status != BookingStatus.Cancelled &&
                        b.CustomerId.HasValue)
            .Select(b => b.CustomerId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var excludedCustomerIds = customersWithActiveWorkOrders
            .Concat(customersWithFutureBookings)
            .ToHashSet();

        // 2. Localiza ordens finalizadas (com PickedUpAtUtc) agrupadas por cliente para obter a última visita
        var completedOrders = await dbContext.WorkOrders
            .Where(w => w.TenantId == tenantId && w.PickedUpAtUtc.HasValue && !excludedCustomerIds.Contains(w.CustomerId))
            .Select(w => new
            {
                w.CustomerId,
                w.VehicleId,
                PickedUpAtUtc = w.PickedUpAtUtc!.Value
            })
            .ToListAsync(ct);

        var latestVisits = completedOrders
            .GroupBy(o => o.CustomerId)
            .Select(g => g.OrderByDescending(x => x.PickedUpAtUtc).First())
            .ToList();

        var result = new List<CustomerLastVisitInfo>();

        foreach (var visit in latestVisits)
        {
            var days = (now - visit.PickedUpAtUtc).Days;
            if (days < minDaysInactive) continue;
            if (maxDaysInactive.HasValue && days > maxDaysInactive.Value) continue;

            var customer = await dbContext.Customers.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == visit.CustomerId, ct);
            if (customer is null || string.IsNullOrWhiteSpace(customer.Phone)) continue;

            var vehicle = await dbContext.Vehicles.FirstOrDefaultAsync(v => v.TenantId == tenantId && v.Id == visit.VehicleId, ct);
            var plate = vehicle?.Plate ?? string.Empty;
            var model = vehicle?.Size.ToString() ?? string.Empty;

            result.Add(new CustomerLastVisitInfo(
                customer.Id,
                customer.Name,
                customer.Phone,
                visit.VehicleId,
                plate,
                model,
                visit.PickedUpAtUtc,
                days));
        }

        return result;
    }

    public async Task<bool> HasRecentCampaignLogAsync(Guid tenantId, Guid customerId, TimeSpan cooldown, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow - cooldown;
        return await dbContext.ReactivationCampaignLogs
            .AnyAsync(l => l.TenantId == tenantId && l.CustomerId == customerId && l.SentAtUtc >= cutoff, ct);
    }

    public async Task<bool> HasCampaignLogForCycleAsync(Guid tenantId, Guid customerId, string idempotencyKey, CancellationToken ct = default) =>
        await dbContext.ReactivationCampaignLogs
            .AnyAsync(l => l.TenantId == tenantId && l.CustomerId == customerId && l.IdempotencyKey == idempotencyKey, ct);

    public async Task AddLogAsync(ReactivationCampaignLog log, CancellationToken ct = default) =>
        await dbContext.ReactivationCampaignLogs.AddAsync(log, ct);
}
