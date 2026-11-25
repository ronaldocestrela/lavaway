namespace CarWashSaaS.Shared.Contracts;

public record CampaignLinkDto(
    Guid Id,
    string Name,
    string Slug,
    string DestinationPath,
    string UtmSource,
    string UtmMedium,
    string UtmCampaign,
    string? UtmTerm,
    string? UtmContent,
    string ShortUrl,
    string FullTrackingUrl,
    bool IsActive,
    long ClickCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? ExpiresAtUtc);

public record CreateCampaignLinkRequest(
    string Name,
    string Slug,
    string DestinationPath,
    string UtmSource,
    string UtmMedium,
    string UtmCampaign,
    string? UtmTerm,
    string? UtmContent,
    DateTimeOffset? ExpiresAtUtc);

public record UpdateCampaignLinkRequest(
    string Name,
    string DestinationPath,
    string UtmSource,
    string UtmMedium,
    string UtmCampaign,
    string? UtmTerm,
    string? UtmContent,
    bool IsActive,
    DateTimeOffset? ExpiresAtUtc);

public record UpdateCampaignStatusRequest(bool IsActive);

public record CampaignQrCodePreviewDto(
    Guid CampaignId,
    string Slug,
    string ShortUrl,
    string SvgContent);
