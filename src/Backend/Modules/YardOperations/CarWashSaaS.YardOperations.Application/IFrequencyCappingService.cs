namespace CarWashSaaS.YardOperations.Application;

public interface IFrequencyCappingService
{
    Task<FrequencyCappingResult> CanSendMarketingMessageAsync(
        Guid tenantId,
        Guid customerId,
        string customerPhone,
        int daysInactive,
        CancellationToken ct = default);
}

public sealed record FrequencyCappingResult(
    bool IsAllowed,
    string Reason,
    string? RejectionCode = null);
