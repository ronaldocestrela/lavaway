using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Caching.Memory;

namespace CarWashSaaS.Api.Services;

/// <summary>
/// Serves the public landing page HTML and llms.txt synchronized with live SaaS plans from the database,
/// caching the result in memory and invalidating immediately when plans are updated in the backoffice.
/// </summary>
public sealed partial class PublicLandingPageService
{
    public const string PricingStartMarker = "<!--pricing-section-start-->";
    public const string PricingEndMarker = "<!--pricing-section-end-->";
    public const string FooterPlansStartMarker = "<!--footer-plans-start-->";
    public const string FooterPlansEndMarker = "<!--footer-plans-end-->";

    private const string LandingCacheKey = "public_seo:landing_html";
    private const string LlmsCacheKey = "public_seo:llms_txt";
    private const string TemplateCacheKey = "public_seo:landing_template";
    private const string LlmsTemplateCacheKey = "public_seo:llms_template";

    private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _landingTemplateUrl;
    private readonly string _llmsTemplateUrl;
    private readonly ILogger<PublicLandingPageService> _logger;

    public PublicLandingPageService(
        IMemoryCache cache,
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PublicLandingPageService> logger)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _landingTemplateUrl = configuration["Seo:LandingTemplateUrl"] ?? "http://web/prerendered/index.html";
        _llmsTemplateUrl = configuration["Seo:LlmsTemplateUrl"] ?? "http://web/llms.txt";
        _logger = logger;
    }

    public void InvalidateCache()
    {
        _cache.Remove(LandingCacheKey);
        _cache.Remove(LlmsCacheKey);
    }

    public async Task<string?> GetLandingHtmlAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(LandingCacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
        {
            return cached;
        }

        var template = await GetTemplateAsync(TemplateCacheKey, _landingTemplateUrl, ct);
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var plans = await LoadActivePlansAsync(ct);
        var rendered = InjectLivePlansIntoHtml(template, plans);

        _cache.Set(LandingCacheKey, rendered, TimeSpan.FromMinutes(10));
        return rendered;
    }

    public async Task<string?> GetLlmsTxtAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(LlmsCacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
        {
            return cached;
        }

        var template = await GetTemplateAsync(LlmsTemplateCacheKey, _llmsTemplateUrl, ct);
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var plans = await LoadActivePlansAsync(ct);
        var rendered = InjectLivePlansIntoLlmsTxt(template, plans);

        _cache.Set(LlmsCacheKey, rendered, TimeSpan.FromMinutes(10));
        return rendered;
    }

    public static string InjectLivePlansIntoHtml(string templateHtml, IReadOnlyList<SaasPlan> plans)
    {
        var html = templateHtml;

        if (plans.Count == 0)
        {
            return html;
        }

        html = ReplaceBetweenMarkers(html, PricingStartMarker, PricingEndMarker, BuildPricingGridHtml(plans));
        html = ReplaceBetweenMarkers(html, FooterPlansStartMarker, FooterPlansEndMarker, BuildFooterPlansHtml(plans));
        html = ReplaceJsonLdOffers(html, plans);

        return html;
    }

    public static string InjectLivePlansIntoLlmsTxt(string templateLlms, IReadOnlyList<SaasPlan> plans)
    {
        if (plans.Count == 0)
        {
            return templateLlms;
        }

        var sb = new StringBuilder();
        sb.AppendLine("## Planos e Preços");
        sb.AppendLine();
        foreach (var plan in plans)
        {
            var price = plan.MonthlyPrice.ToString("N2", PtBrCulture);
            var popularTag = plan.Tier == SaasPlanTier.Pro ? " - Plano Mais Popular" : string.Empty;
            var osLabel = plan.MaxWorkOrdersPerCycle <= 0
                ? "ordens de serviço ilimitadas"
                : $"até {plan.MaxWorkOrdersPerCycle} veículos/OS por mês";
            var usersLabel = plan.MaxTeamMembers <= 0 || plan.MaxTeamMembers >= 999
                ? "usuários ilimitados"
                : $"{plan.MaxTeamMembers} usuários cadastrados";
            var boxesLabel = plan.MaxBoxes <= 0
                ? "boxes/baias ilimitados"
                : $"{plan.MaxBoxes} boxes/baias de atendimento";

            sb.AppendLine($"- **Plano {plan.Name} (R$ {price} / mês{popularTag})**:");
            sb.AppendLine($"  - {plan.Description}");
            sb.AppendLine($"  - Capacidade: {osLabel}, {usersLabel} e {boxesLabel}.");
            sb.AppendLine();
        }

        sb.Append("*Opção de pagamento anual disponível com desconto equivalente a 2 meses grátis.*");

        return LlmsPlansSectionRegex().Replace(templateLlms, sb.ToString(), 1);
    }

    private async Task<IReadOnlyList<SaasPlan>> LoadActivePlansAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var planRepo = scope.ServiceProvider.GetRequiredService<ISaasPlanRepository>();
        var allPlans = await planRepo.ListAllAsync(ct);

        var active = allPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.Tier)
            .ToList();

        return active.Count > 0 ? active : SaasPlan.GetStandardPlans();
    }

    private async Task<string?> GetTemplateAsync(string cacheKey, string url, CancellationToken ct)
    {
        if (_cache.TryGetValue(cacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
        {
            return cached;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("WebFrontend");
            using var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Falha ao carregar template público {Url}: HTTP {StatusCode}", url, (int)response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(content))
            {
                _cache.Set(cacheKey, content, TimeSpan.FromMinutes(5));
            }
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao buscar template público em {Url}", url);
            return null;
        }
    }

    private static string BuildPricingGridHtml(IReadOnlyList<SaasPlan> plans)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"pricing-grid\">");

        foreach (var plan in plans)
        {
            var isPopular = plan.Tier == SaasPlanTier.Pro;
            var tierSlug = plan.Tier.ToString().ToLowerInvariant();
            var cardClass = isPopular ? "pricing-card popular" : "pricing-card";
            var btnClass = isPopular ? "btn-primary full glow" : "btn-outline-card";
            var popularSuffix = isPopular ? " (14 Dias Grátis)" : string.Empty;

            var boxesText = plan.MaxBoxes <= 0
                ? "Boxes de atendimento ilimitados"
                : $"Até {plan.MaxBoxes} boxes de atendimento";
            var teamText = plan.MaxTeamMembers <= 0 || plan.MaxTeamMembers >= 999
                ? "Equipe e usuários ilimitados"
                : $"Até {plan.MaxTeamMembers} membros na equipe";
            var osText = plan.MaxWorkOrdersPerCycle <= 0
                ? "Ordens de serviço ilimitadas"
                : $"Até {plan.MaxWorkOrdersPerCycle} OS/mês";
            var waText = plan.MaxWhatsAppMessagesPerCycle <= 0
                ? "WhatsApp automático ilimitado"
                : $"Até {plan.MaxWhatsAppMessagesPerCycle} msgs WhatsApp/mês";

            sb.Append($"<div class=\"{cardClass}\" id=\"pricing-card-{tierSlug}\">");
            if (isPopular)
            {
                sb.Append("<div class=\"popular-ribbon\">MAIS ESCOLHIDO</div>");
            }

            sb.Append("<div class=\"p-header\">");
            sb.Append($"<span class=\"p-tier-name\">{HtmlEncode(plan.Name.ToUpperInvariant())}</span>");
            sb.Append($"<p class=\"p-desc\">{HtmlEncode(plan.Description)}</p>");
            sb.Append("<div class=\"p-price\">");
            sb.Append("<span class=\"currency\">R$</span>");
            sb.Append($"<span class=\"amount\">{plan.MonthlyPrice.ToString("N0", PtBrCulture)}</span>");
            sb.Append("<span class=\"period\">/mês</span>");
            sb.Append("</div></div>");

            sb.Append($"<button class=\"{btnClass}\" type=\"button\" id=\"btn-plan-{tierSlug}\">");
            sb.Append($"Contratar {HtmlEncode(plan.Name)}{popularSuffix}</button>");

            sb.Append("<ul class=\"p-features\">");
            sb.Append($"<li><span>&#10003;</span><strong>{HtmlEncode(boxesText)}</strong></li>");
            sb.Append($"<li><span>&#10003;</span><strong>{HtmlEncode(teamText)}</strong></li>");
            sb.Append($"<li><span>&#10003;</span>{HtmlEncode(osText)}</li>");
            sb.Append($"<li><span>&#10003;</span>{HtmlEncode(waText)}</li>");
            AppendFeatureItem(sb, plan.HasCustomerSubscriptions, "Clube de Assinaturas para clientes", "Sem Clube de Assinaturas");
            AppendFeatureItem(sb, plan.HasLoyalty, "Programa de Fidelidade e Pontos", "Sem Programa de Fidelidade");
            AppendFeatureItem(sb, plan.HasCommissions, "Cálculo e rateio de comissões", "Sem Gestão de Comissões");
            AppendFeatureItem(sb, plan.HasAiChatbot, "Chatbot Inteligente com IA no WhatsApp", "Sem Chatbot com IA");
            sb.Append("</ul></div>");
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    private static void AppendFeatureItem(StringBuilder sb, bool enabled, string enabledText, string disabledText)
    {
        var cls = enabled ? string.Empty : " class=\"muted\"";
        var icon = enabled ? "✓" : "×";
        var text = enabled ? enabledText : disabledText;
        sb.Append($"<li{cls}><span>{icon}</span>{HtmlEncode(text)}</li>");
    }

    private static string BuildFooterPlansHtml(IReadOnlyList<SaasPlan> plans)
    {
        var sb = new StringBuilder();
        foreach (var plan in plans)
        {
            sb.Append($"<button class=\"footer-link-btn\" type=\"button\">Plano {HtmlEncode(plan.Name)}</button>");
        }
        return sb.ToString();
    }

    private static string ReplaceJsonLdOffers(string html, IReadOnlyList<SaasPlan> plans)
    {
        var offers = plans.Select(p => new
        {
            @type = "Offer",
            name = $"Plano {p.Name}",
            price = p.MonthlyPrice.ToString("0.00", CultureInfo.InvariantCulture),
            priceCurrency = "BRL",
            description = p.Description
        });

        var offersJson = JsonSerializer.Serialize(offers, new JsonSerializerOptions { WriteIndented = false });
        return JsonLdOffersRegex().Replace(html, $"\"offers\": {offersJson}", 1);
    }

    private static string ReplaceBetweenMarkers(string source, string startMarker, string endMarker, string replacement)
    {
        var startIdx = source.IndexOf(startMarker, StringComparison.Ordinal);
        var endIdx = source.IndexOf(endMarker, StringComparison.Ordinal);
        if (startIdx < 0 || endIdx < 0 || endIdx <= startIdx)
        {
            return source;
        }

        return string.Concat(
            source[..(startIdx + startMarker.Length)],
            replacement,
            source[endIdx..]);
    }

    private static string HtmlEncode(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);

    [GeneratedRegex("\"offers\"\\s*:\\s*\\[.*?\\]", RegexOptions.Singleline)]
    private static partial Regex JsonLdOffersRegex();

    [GeneratedRegex("## Planos e Preços.*?(?=\\n## )", RegexOptions.Singleline)]
    private static partial Regex LlmsPlansSectionRegex();
}
