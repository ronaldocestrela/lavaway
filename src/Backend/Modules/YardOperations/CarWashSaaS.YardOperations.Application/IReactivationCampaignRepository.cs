using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public interface IReactivationCampaignRepository
{
    Task<IReadOnlyList<ReactivationCampaignRule>> GetRulesAsync(Guid tenantId, CancellationToken ct = default);
    Task<ReactivationCampaignRule?> GetRuleByIdAsync(Guid tenantId, Guid ruleId, CancellationToken ct = default);
    Task<ReactivationCampaignRule?> GetRuleByDaysAsync(Guid tenantId, int daysInactive, CancellationToken ct = default);
    Task AddRuleAsync(ReactivationCampaignRule rule, CancellationToken ct = default);
    void UpdateRule(ReactivationCampaignRule rule);

    Task<IReadOnlyList<CustomerLastVisitInfo>> GetInactiveCustomersAsync(Guid tenantId, int minDaysInactive, int? maxDaysInactive, CancellationToken ct = default);
    Task<bool> HasRecentCampaignLogAsync(Guid tenantId, Guid customerId, TimeSpan cooldown, CancellationToken ct = default);
    Task<bool> HasCampaignLogForCycleAsync(Guid tenantId, Guid customerId, string idempotencyKey, CancellationToken ct = default);
    Task AddLogAsync(ReactivationCampaignLog log, CancellationToken ct = default);
}

public sealed record CustomerLastVisitInfo(
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    Guid VehicleId,
    string VehiclePlate,
    string VehicleModel,
    DateTimeOffset LastVisitAtUtc,
    int DaysInactive);
