namespace CarWashSaaS.Shared.Contracts;

public sealed record WhatsAppInstanceHealthSummaryDto(
    int TotalInstances,
    int ConnectedCount,
    int DisconnectedCount,
    int ConnectingCount,
    int AlertsActiveCount);

public sealed record PlatformWhatsAppInstanceItemDto(
    Guid TenantId,
    string TenantName,
    string ProviderSessionId,
    string Status,
    DateTimeOffset? LastConnectedAtUtc,
    DateTimeOffset? LastDisconnectedAtUtc,
    string? DisconnectReason,
    bool HasActiveAlert,
    int AlertCount,
    DateTimeOffset? LastAlertSentAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record PlatformWhatsAppInstancesOverviewDto(
    WhatsAppInstanceHealthSummaryDto Summary,
    PagedResult<PlatformWhatsAppInstanceItemDto> Instances);

public sealed record TenantWhatsAppHealthDetailDto(
    string Status,
    bool IsConnected,
    bool HasActiveAlert,
    string? DisconnectReason,
    DateTimeOffset? LastConnectedAtUtc,
    DateTimeOffset? LastDisconnectedAtUtc,
    DateTimeOffset? LastAlertSentAtUtc,
    string ReconnectInstructionsUrl);

public sealed record WhatsAppConnectionIncidentDto(
    Guid Id,
    Guid TenantId,
    string ProviderSessionId,
    string Type,
    string Reason,
    bool AlertDispatched,
    string? RecipientEmail,
    DateTimeOffset OccurredAtUtc);

public sealed record ProbeWhatsAppInstanceResultDto(
    bool IsReachable,
    string ProviderState,
    string Message,
    DateTimeOffset CheckedAtUtc);

public sealed record TriggerWhatsAppAlertRequest(
    string? CustomNote = null);

public sealed record GetPlatformWhatsAppInstancesRequest(
    string? SearchTerm = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20);

public sealed record TenantNotificationContactDto(
    Guid TenantId,
    string TenantName,
    string ContactEmail,
    string? ContactPhone);

public interface ITenantNotificationContactLookup
{
    Task<Result<TenantNotificationContactDto>> GetContactAsync(Guid tenantId, CancellationToken ct = default);
}
