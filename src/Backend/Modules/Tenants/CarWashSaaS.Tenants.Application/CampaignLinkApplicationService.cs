using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Domain;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.Tenants.Application;

public sealed class CampaignLinkApplicationService
{
    private readonly ICampaignLinkRepository _repository;
    private readonly IQrCodeGenerator _qrCodeGenerator;
    private readonly IAuditTrailService? _auditTrailService;
    private readonly IAppUrlResolver _urlResolver;
    private readonly ILogger<CampaignLinkApplicationService>? _logger;

    public CampaignLinkApplicationService(
        ICampaignLinkRepository repository,
        IQrCodeGenerator qrCodeGenerator,
        IAppUrlResolver urlResolver,
        IAuditTrailService? auditTrailService = null,
        ILogger<CampaignLinkApplicationService>? logger = null)
    {
        _repository = repository;
        _qrCodeGenerator = qrCodeGenerator;
        _urlResolver = urlResolver;
        _auditTrailService = auditTrailService;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<CampaignLinkDto>>> ListCampaignsAsync(
        string? searchTerm = null,
        bool? isActive = null,
        CancellationToken ct = default)
    {
        var campaigns = await _repository.ListAsync(searchTerm, isActive, ct);
        var baseUrl = _urlResolver.GetBaseUrl();

        var dtos = campaigns
            .Select(c => MapToDto(c, baseUrl))
            .ToList();

        return Result<IReadOnlyList<CampaignLinkDto>>.Success(dtos);
    }

    public async Task<Result<CampaignLinkDto>> GetCampaignByIdAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await _repository.GetByIdAsync(id, ct);
        if (campaign is null)
        {
            return Result<CampaignLinkDto>.Failure(new Error("campaign.not_found", "Campanha não encontrada.", ErrorType.NotFound));
        }

        var baseUrl = _urlResolver.GetBaseUrl();
        return Result<CampaignLinkDto>.Success(MapToDto(campaign, baseUrl));
    }

    public async Task<Result<CampaignLinkDto>> CreateCampaignAsync(
        CreateCampaignLinkRequest request,
        Guid actorUserId,
        string actorEmail = "admin@lavaway.com",
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        var normalizedSlug = request.Slug?.Trim().ToLowerInvariant() ?? string.Empty;

        var exists = await _repository.ExistsBySlugAsync(normalizedSlug, ct);
        if (exists)
        {
            return Result<CampaignLinkDto>.Failure(new Error(
                "campaign.slug_already_exists",
                $"O slug '{normalizedSlug}' já está em uso por outra campanha.",
                ErrorType.Conflict));
        }

        var domainResult = CampaignLink.Create(
            request.Name,
            request.Slug,
            request.DestinationPath,
            request.UtmSource,
            request.UtmMedium,
            request.UtmCampaign,
            request.UtmTerm,
            request.UtmContent,
            request.ExpiresAtUtc,
            actorUserId);

        if (!domainResult.IsSuccess)
        {
            return Result<CampaignLinkDto>.Failure(domainResult.Error!);
        }

        var campaign = domainResult.Value!;
        await _repository.AddAsync(campaign, ct);

        if (_auditTrailService is not null)
        {
            await _auditTrailService.RecordEventAsync(
                new RecordAuditEventRequest(
                    ActorId: actorUserId,
                    ActorEmail: actorEmail,
                    ActorRole: "PlatformUser",
                    ActorRealm: "Platform",
                    Action: "CreateCampaignLink",
                    TargetType: "CampaignLink",
                    TargetId: campaign.Id.ToString(),
                    TenantId: null,
                    IpAddress: ipAddress,
                    UserAgent: userAgent,
                    DetailsJson: System.Text.Json.JsonSerializer.Serialize(new { campaign.Name, campaign.Slug, campaign.DestinationPath, campaign.UtmCampaign }),
                    Outcome: "Success"),
                ct);
        }

        _logger?.LogInformation("Campaign link '{Slug}' created by {ActorEmail}", campaign.Slug, actorEmail);

        var baseUrl = _urlResolver.GetBaseUrl();
        return Result<CampaignLinkDto>.Success(MapToDto(campaign, baseUrl));
    }

    public async Task<Result<CampaignLinkDto>> UpdateCampaignAsync(
        Guid id,
        UpdateCampaignLinkRequest request,
        Guid actorUserId,
        string actorEmail = "admin@lavaway.com",
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        var campaign = await _repository.GetByIdAsync(id, ct);
        if (campaign is null)
        {
            return Result<CampaignLinkDto>.Failure(new Error("campaign.not_found", "Campanha não encontrada.", ErrorType.NotFound));
        }

        var updateResult = campaign.Update(
            request.Name,
            request.DestinationPath,
            request.UtmSource,
            request.UtmMedium,
            request.UtmCampaign,
            request.UtmTerm,
            request.UtmContent,
            request.IsActive,
            request.ExpiresAtUtc);

        if (!updateResult.IsSuccess)
        {
            return Result<CampaignLinkDto>.Failure(updateResult.Error!);
        }

        _repository.Update(campaign);

        if (_auditTrailService is not null)
        {
            await _auditTrailService.RecordEventAsync(
                new RecordAuditEventRequest(
                    ActorId: actorUserId,
                    ActorEmail: actorEmail,
                    ActorRole: "PlatformUser",
                    ActorRealm: "Platform",
                    Action: "UpdateCampaignLink",
                    TargetType: "CampaignLink",
                    TargetId: campaign.Id.ToString(),
                    TenantId: null,
                    IpAddress: ipAddress,
                    UserAgent: userAgent,
                    DetailsJson: System.Text.Json.JsonSerializer.Serialize(new { campaign.Name, campaign.Slug, campaign.IsActive }),
                    Outcome: "Success"),
                ct);
        }

        var baseUrl = _urlResolver.GetBaseUrl();
        return Result<CampaignLinkDto>.Success(MapToDto(campaign, baseUrl));
    }

    public async Task<Result<CampaignLinkDto>> ToggleStatusAsync(
        Guid id,
        bool isActive,
        Guid actorUserId,
        string actorEmail = "admin@lavaway.com",
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        var campaign = await _repository.GetByIdAsync(id, ct);
        if (campaign is null)
        {
            return Result<CampaignLinkDto>.Failure(new Error("campaign.not_found", "Campanha não encontrada.", ErrorType.NotFound));
        }

        campaign.SetActive(isActive);
        _repository.Update(campaign);

        if (_auditTrailService is not null)
        {
            await _auditTrailService.RecordEventAsync(
                new RecordAuditEventRequest(
                    ActorId: actorUserId,
                    ActorEmail: actorEmail,
                    ActorRole: "PlatformUser",
                    ActorRealm: "Platform",
                    Action: isActive ? "ActivateCampaignLink" : "DeactivateCampaignLink",
                    TargetType: "CampaignLink",
                    TargetId: campaign.Id.ToString(),
                    TenantId: null,
                    IpAddress: ipAddress,
                    UserAgent: userAgent,
                    DetailsJson: System.Text.Json.JsonSerializer.Serialize(new { campaign.Slug, IsActive = isActive }),
                    Outcome: "Success"),
                ct);
        }

        var baseUrl = _urlResolver.GetBaseUrl();
        return Result<CampaignLinkDto>.Success(MapToDto(campaign, baseUrl));
    }

    public async Task<Result<string>> ResolveRedirectTargetAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result<string>.Failure(new Error("campaign.invalid_slug", "Slug inválido.", ErrorType.Validation));
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var campaign = await _repository.GetBySlugAsync(normalizedSlug, ct);
        if (campaign is null)
        {
            return Result<string>.Failure(new Error("campaign.not_found", "Campanha não encontrada.", ErrorType.NotFound));
        }

        var now = DateTimeOffset.UtcNow;
        var clickResult = campaign.RegisterClick(now);
        if (!clickResult.IsSuccess)
        {
            return Result<string>.Failure(clickResult.Error!);
        }

        await _repository.IncrementClickCountAsync(campaign.Id, now, ct);

        var baseUrl = _urlResolver.GetBaseUrl();
        var targetUrl = campaign.BuildFullTrackingUrl(baseUrl);

        return Result<string>.Success(targetUrl);
    }

    public async Task<Result<CampaignQrCodePreviewDto>> GetQrCodePreviewAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await _repository.GetByIdAsync(id, ct);
        if (campaign is null)
        {
            return Result<CampaignQrCodePreviewDto>.Failure(new Error("campaign.not_found", "Campanha não encontrada.", ErrorType.NotFound));
        }

        var baseUrl = _urlResolver.GetBaseUrl();
        var shortUrl = $"{baseUrl.TrimEnd('/')}/c/{campaign.Slug}";
        var svg = _qrCodeGenerator.GenerateSvg(shortUrl, 10);

        return Result<CampaignQrCodePreviewDto>.Success(new CampaignQrCodePreviewDto(
            campaign.Id,
            campaign.Slug,
            shortUrl,
            svg));
    }

    public async Task<Result<byte[]>> GetQrCodePngBytesAsync(Guid id, int pixelsPerModule = 10, CancellationToken ct = default)
    {
        var campaign = await _repository.GetByIdAsync(id, ct);
        if (campaign is null)
        {
            return Result<byte[]>.Failure(new Error("campaign.not_found", "Campanha não encontrada.", ErrorType.NotFound));
        }

        var baseUrl = _urlResolver.GetBaseUrl();
        var shortUrl = $"{baseUrl.TrimEnd('/')}/c/{campaign.Slug}";
        var pngBytes = _qrCodeGenerator.GeneratePng(shortUrl, pixelsPerModule);

        return Result<byte[]>.Success(pngBytes);
    }

    private static CampaignLinkDto MapToDto(CampaignLink campaign, string baseUrl)
    {
        var shortUrl = $"{baseUrl.TrimEnd('/')}/c/{campaign.Slug}";
        var fullTrackingUrl = campaign.BuildFullTrackingUrl(baseUrl);

        return new CampaignLinkDto(
            campaign.Id,
            campaign.Name,
            campaign.Slug,
            campaign.DestinationPath,
            campaign.UtmSource,
            campaign.UtmMedium,
            campaign.UtmCampaign,
            campaign.UtmTerm,
            campaign.UtmContent,
            shortUrl,
            fullTrackingUrl,
            campaign.IsActive,
            campaign.ClickCount,
            campaign.CreatedAtUtc,
            campaign.UpdatedAtUtc,
            campaign.ExpiresAtUtc);
    }
}
