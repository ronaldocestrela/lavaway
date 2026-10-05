namespace CarWashSaaS.Shared.Contracts;

public static class LoyaltyTransactionTypeConstants
{
    public const string Accrual = "Accrual";
    public const string Redemption = "Redemption";
    public const string Adjustment = "Adjustment";
}

public sealed record LoyaltyProgramDto(
    bool IsEnabled,
    int TargetStamps,
    string RewardTitle,
    int ProximityThreshold,
    bool AllServicesEligible,
    string? EligibleCategoryFilter,
    DateTimeOffset UpdatedAtUtc);

public sealed record UpdateLoyaltyProgramRequest(
    bool IsEnabled,
    int TargetStamps,
    string RewardTitle,
    int ProximityThreshold = 1,
    bool AllServicesEligible = true,
    string? EligibleCategoryFilter = null);

public sealed record CustomerLoyaltySummaryDto(
    Guid AccountId,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    int Balance,
    int TargetStamps,
    int RemainingStamps,
    string RewardTitle,
    bool IsReadyForRedemption,
    bool IsNearRedemption,
    DateTimeOffset? LastAccrualAtUtc);

public sealed record CustomerLoyaltyTransactionDto(
    Guid Id,
    string Type,
    int Amount,
    int BalanceAfter,
    Guid? WorkOrderId,
    string? WorkOrderNumber,
    string Description,
    DateTimeOffset CreatedAtUtc);

public sealed record RedeemLoyaltyRewardRequest(
    Guid CustomerId,
    string? Notes = null);

public sealed record ManualLoyaltyAdjustmentRequest(
    Guid CustomerId,
    int Delta,
    string Reason);
