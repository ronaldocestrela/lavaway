using System.Text;
using System.Text.RegularExpressions;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Tenants.Domain;

public sealed class CampaignLink
{
    private static readonly Regex SlugRegex = new(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string DestinationPath { get; private set; } = string.Empty;
    public string UtmSource { get; private set; } = string.Empty;
    public string UtmMedium { get; private set; } = string.Empty;
    public string UtmCampaign { get; private set; } = string.Empty;
    public string? UtmTerm { get; private set; }
    public string? UtmContent { get; private set; }
    public bool IsActive { get; private set; }
    public long ClickCount { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    // EF Core constructor
    private CampaignLink() { }

    private CampaignLink(
        Guid id,
        string name,
        string slug,
        string destinationPath,
        string utmSource,
        string utmMedium,
        string utmCampaign,
        string? utmTerm,
        string? utmContent,
        DateTimeOffset? expiresAtUtc,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Name = name;
        Slug = slug;
        DestinationPath = destinationPath;
        UtmSource = utmSource;
        UtmMedium = utmMedium;
        UtmCampaign = utmCampaign;
        UtmTerm = utmTerm;
        UtmContent = utmContent;
        IsActive = true;
        ClickCount = 0;
        ExpiresAtUtc = expiresAtUtc;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
    }

    public static Result<CampaignLink> Create(
        string name,
        string slug,
        string destinationPath,
        string utmSource,
        string utmMedium,
        string utmCampaign,
        string? utmTerm,
        string? utmContent,
        DateTimeOffset? expiresAtUtc,
        Guid createdByUserId)
    {
        var validationResult = Validate(name, slug, destinationPath, utmSource, utmMedium, utmCampaign);
        if (!validationResult.IsSuccess)
        {
            return Result<CampaignLink>.Failure(validationResult.Error!);
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var normalizedDest = destinationPath.Trim();

        var campaign = new CampaignLink(
            Guid.NewGuid(),
            name.Trim(),
            normalizedSlug,
            normalizedDest,
            utmSource.Trim(),
            utmMedium.Trim(),
            utmCampaign.Trim(),
            string.IsNullOrWhiteSpace(utmTerm) ? null : utmTerm.Trim(),
            string.IsNullOrWhiteSpace(utmContent) ? null : utmContent.Trim(),
            expiresAtUtc,
            createdByUserId,
            DateTimeOffset.UtcNow);

        return Result<CampaignLink>.Success(campaign);
    }

    public Result Update(
        string name,
        string destinationPath,
        string utmSource,
        string utmMedium,
        string utmCampaign,
        string? utmTerm,
        string? utmContent,
        bool isActive,
        DateTimeOffset? expiresAtUtc)
    {
        var validationResult = Validate(name, Slug, destinationPath, utmSource, utmMedium, utmCampaign);
        if (!validationResult.IsSuccess)
        {
            return validationResult;
        }

        Name = name.Trim();
        DestinationPath = destinationPath.Trim();
        UtmSource = utmSource.Trim();
        UtmMedium = utmMedium.Trim();
        UtmCampaign = utmCampaign.Trim();
        UtmTerm = string.IsNullOrWhiteSpace(utmTerm) ? null : utmTerm.Trim();
        UtmContent = string.IsNullOrWhiteSpace(utmContent) ? null : utmContent.Trim();
        IsActive = isActive;
        ExpiresAtUtc = expiresAtUtc;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        return Result.Success();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Result RegisterClick(DateTimeOffset utcNow)
    {
        if (!IsActive)
        {
            return Result.Failure(new Error("campaign.inactive", "A campanha informada está desativada.", ErrorType.Validation));
        }

        if (ExpiresAtUtc.HasValue && utcNow > ExpiresAtUtc.Value)
        {
            return Result.Failure(new Error("campaign.expired", "O período de vigência desta campanha já expirou.", ErrorType.Validation));
        }

        ClickCount++;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    public string BuildFullTrackingUrl(string baseUrl)
    {
        var cleanBase = baseUrl.TrimEnd('/');
        var path = DestinationPath.StartsWith('/') ? DestinationPath : "/" + DestinationPath;

        var queryString = BuildTrackingQueryString();
        var separator = path.Contains('?') ? "&" : "?";

        return $"{cleanBase}{path}{separator}{queryString}";
    }

    public string BuildTrackingQueryString()
    {
        var sb = new StringBuilder();
        sb.Append($"utm_source={Uri.EscapeDataString(UtmSource)}");
        sb.Append($"&utm_medium={Uri.EscapeDataString(UtmMedium)}");
        sb.Append($"&utm_campaign={Uri.EscapeDataString(UtmCampaign)}");

        if (!string.IsNullOrWhiteSpace(UtmTerm))
        {
            sb.Append($"&utm_term={Uri.EscapeDataString(UtmTerm)}");
        }

        if (!string.IsNullOrWhiteSpace(UtmContent))
        {
            sb.Append($"&utm_content={Uri.EscapeDataString(UtmContent)}");
        }

        return sb.ToString();
    }

    private static Result Validate(
        string name,
        string slug,
        string destinationPath,
        string utmSource,
        string utmMedium,
        string utmCampaign)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(new Error("campaign.invalid_name", "O nome da campanha é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(slug) || !SlugRegex.IsMatch(slug.Trim().ToLowerInvariant()))
        {
            return Result.Failure(new Error("campaign.invalid_slug", "O slug deve conter apenas letras minúsculas, números e hífens (ex: black-friday-2026).", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(destinationPath) ||
            !destinationPath.StartsWith('/') ||
            destinationPath.StartsWith("//", StringComparison.Ordinal) ||
            destinationPath.Contains("://", StringComparison.Ordinal))
        {
            return Result.Failure(new Error("campaign.unsafe_destination", "O destino deve ser um caminho interno iniciando com '/' (ex: / ou /authentication/register).", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(utmSource))
        {
            return Result.Failure(new Error("campaign.invalid_utm_source", "O parâmetro utm_source é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(utmMedium))
        {
            return Result.Failure(new Error("campaign.invalid_utm_medium", "O parâmetro utm_medium é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(utmCampaign))
        {
            return Result.Failure(new Error("campaign.invalid_utm_campaign", "O parâmetro utm_campaign é obrigatório.", ErrorType.Validation));
        }

        return Result.Success();
    }
}
