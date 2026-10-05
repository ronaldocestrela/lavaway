namespace CarWashSaaS.Shared.Contracts;

public static class SubscriptionStatusConstants
{
    public const string Active = "Active";
    public const string PastDue = "PastDue";
    public const string Canceled = "Canceled";
    public const string Suspended = "Suspended";

    public static readonly IReadOnlyList<string> All = [Active, PastDue, Canceled, Suspended];

    public static string ToDisplayName(string status) => status switch
    {
        Active => "Ativo",
        PastDue => "Atrasado",
        Canceled => "Cancelado",
        Suspended => "Suspenso",
        _ => status
    };
}

public sealed record SubscriptionPlanDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Description,
    decimal MonthlyPrice,
    int BillingIntervalDays,
    int CreditsPerCycle,
    int AllowedPlatesLimit,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

public sealed record CustomerSubscriptionDto(
    Guid Id,
    Guid TenantId,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    Guid PlanId,
    string PlanName,
    string Status,
    DateTimeOffset CurrentPeriodStartUtc,
    DateTimeOffset CurrentPeriodEndUtc,
    int TotalCreditsInCycle,
    int UsedCreditsInCycle,
    int AvailableCredits,
    IReadOnlyList<string> AuthorizedPlates,
    string? CardLastFourDigits,
    string? CardBrand,
    string? GatewaySubscriptionId,
    DateTimeOffset CreatedAtUtc);

public sealed record SubscriptionPlateSummaryDto(
    Guid SubscriptionId,
    Guid CustomerId,
    string CustomerName,
    string PlanName,
    string Status,
    string Plate,
    int TotalCredits,
    int UsedCredits,
    int AvailableCredits,
    bool CanConsume,
    DateTimeOffset CurrentPeriodEndUtc);

public sealed record SubscriptionUsageDto(
    Guid Id,
    Guid SubscriptionId,
    Guid? WorkOrderId,
    string Plate,
    string? ServiceName,
    DateTimeOffset ConsumedAtUtc,
    string? Notes);

public sealed record SubscriptionUsageReceiptDto(
    Guid UsageId,
    Guid SubscriptionId,
    Guid? WorkOrderId,
    string Plate,
    int RemainingCredits,
    DateTimeOffset ConsumedAtUtc);

public sealed record CreateSubscriptionPlanRequest(
    string Name,
    string? Description,
    decimal MonthlyPrice,
    int CreditsPerCycle,
    int AllowedPlatesLimit,
    int BillingIntervalDays = 30);

public sealed record UpdateSubscriptionPlanRequest(
    string Name,
    string? Description,
    decimal MonthlyPrice,
    int CreditsPerCycle,
    int AllowedPlatesLimit,
    bool IsActive);

public sealed record SubscribeCustomerRequest(
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    Guid PlanId,
    IReadOnlyList<string> InitialPlates,
    string? CardNumber = null,
    string? CardHolderName = null,
    string? CardExpiration = null,
    string? CardCvv = null);

public sealed record AddSubscriptionPlateRequest(string Plate);

public sealed record ConsumeSubscriptionCreditRequest(
    Guid? WorkOrderId,
    string Plate,
    string? ServiceName,
    string? Notes = null);

public sealed record SubscriptionDashboardSummaryDto(
    int ActiveSubscriptionsCount,
    decimal MonthlyRecurringRevenue,
    int TotalCreditsInActiveCycles,
    int TotalUsagesCurrentMonth);

public interface ISubscriptionLookup
{
    Task<Result<SubscriptionPlateSummaryDto?>> GetActiveSubscriptionByPlateAsync(Guid tenantId, string plate, CancellationToken ct = default);
    Task<Result<IReadOnlyList<CustomerSubscriptionDto>>> GetCustomerSubscriptionsAsync(Guid tenantId, Guid customerId, CancellationToken ct = default);
}

public interface ISubscriptionUsageService
{
    Task<Result<SubscriptionUsageReceiptDto>> ConsumeCreditForWorkOrderAsync(Guid tenantId, ConsumeSubscriptionCreditRequest request, CancellationToken ct = default);
    Task<Result> CancelUsageForWorkOrderAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default);
}
