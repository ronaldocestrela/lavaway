namespace CarWashSaaS.Shared.Contracts;

public interface IAfterSalesLookup
{
    Task<Result<WorkOrderDto>> RegisterPickupAsync(
        Guid tenantId,
        Guid workOrderId,
        DateTimeOffset? pickedUpAtUtc = null,
        string? notes = null,
        CancellationToken ct = default);

    Task<Result> SubmitSurveyRatingAsync(
        Guid tenantId,
        string customerPhone,
        int rating,
        string? feedbackComment = null,
        CancellationToken ct = default);

    Task<Result<SatisfactionSurveyDto?>> GetPendingSurveyForCustomerAsync(
        Guid tenantId,
        string customerPhone,
        CancellationToken ct = default);
}
