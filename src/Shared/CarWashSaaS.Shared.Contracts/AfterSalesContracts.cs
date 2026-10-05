namespace CarWashSaaS.Shared.Contracts;

public sealed record SatisfactionSurveyDto(
    Guid WorkOrderId,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    string VehiclePlate,
    string VehicleModel,
    DateTimeOffset PickedUpAtUtc,
    DateTimeOffset? SurveySentAtUtc,
    int? Rating,
    string? FeedbackComment,
    DateTimeOffset? RespondedAtUtc,
    string Status);

public sealed record AfterSalesMetricsDto(
    int TotalSurveysSent,
    int TotalSurveysResponded,
    decimal ResponseRatePercentage,
    decimal AverageRating,
    int FiveStarCount,
    int FourStarCount,
    int ThreeStarCount,
    int TwoStarCount,
    int OneStarCount);

public sealed record ReactivationCampaignRuleDto(
    Guid Id,
    Guid TenantId,
    int DaysInactive,
    string Title,
    string MessageTemplate,
    bool IsEnabled,
    string? PromotionalOffer,
    int EligibleAudienceCount,
    int FrequencyCappedCount,
    int OptedOutCount,
    DateTimeOffset UpdatedAtUtc);

public sealed record UpdateReactivationCampaignRuleRequest(
    string Title,
    string MessageTemplate,
    bool IsEnabled,
    string? PromotionalOffer);

public sealed record InactiveCustomerSummaryDto(
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    string VehiclePlate,
    string VehicleModel,
    DateTimeOffset LastVisitAtUtc,
    int DaysSinceLastVisit,
    bool IsEligible,
    string EligibilityStatus);

public sealed record RegisterWorkOrderPickupRequest(
    DateTimeOffset? PickedUpAtUtc = null,
    string? Notes = null);

public sealed record RecordSatisfactionRatingRequest(
    int Rating,
    string? FeedbackComment = null);

public sealed record CustomerCommunicationPreferenceDto(
    Guid Id,
    Guid TenantId,
    string NormalizedPhone,
    bool IsOptedIn,
    DateTimeOffset? OptedOutAtUtc,
    string? Reason,
    DateTimeOffset UpdatedAt);

public sealed record UpdateCustomerPreferenceRequest(
    string Phone,
    bool IsOptedIn,
    string? Reason);

public sealed record DispatchCampaignResultDto(
    int TotalEvaluated,
    int TotalDispatched,
    int TotalSkippedFrequencyCap,
    int TotalSkippedOptOut);
