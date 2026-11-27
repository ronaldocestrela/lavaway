using System.Text;
using CarWashSaaS.Api.Services;

namespace CarWashSaaS.Api.Endpoints;

public static class PublicSeoEndpoints
{
    private const string RobotsContent = """
        # Robots.txt para Lava Aê (Lavaway)
        # https://lavaae.app

        User-agent: *
        Allow: /
        Allow: /privacidade
        Allow: /termos
        Allow: /dpo
        Allow: /llms.txt
        Allow: /llms-full.txt
        Allow: /sitemap.xml
        Disallow: /api/
        Disallow: /vistoria/
        Disallow: /reception
        Disallow: /yard
        Disallow: /cashier
        Disallow: /customers
        Disallow: /scheduling
        Disallow: /inspection
        Disallow: /loyalty
        Disallow: /subscriptions
        Disallow: /after-sales
        Disallow: /payment-settings
        Disallow: /whatsapp-settings
        Disallow: /store-profile
        Disallow: /team-members
        Disallow: /platform
        Disallow: /admin

        # Rastreadores e Agentes de Inteligência Artificial (GEO / AIO)
        User-agent: GPTBot
        Allow: /
        Allow: /llms.txt
        Allow: /llms-full.txt

        User-agent: ChatGPT-User
        Allow: /
        Allow: /llms.txt
        Allow: /llms-full.txt

        User-agent: PerplexityBot
        Allow: /
        Allow: /llms.txt
        Allow: /llms-full.txt

        User-agent: ClaudeBot
        Allow: /
        Allow: /llms.txt
        Allow: /llms-full.txt

        User-agent: anthropic-ai
        Allow: /
        Allow: /llms.txt
        Allow: /llms-full.txt

        User-agent: Google-Extended
        Allow: /

        User-agent: Applebot-Extended
        Allow: /

        User-agent: CCBot
        Allow: /

        Sitemap: https://lavaae.app/sitemap.xml
        """;

    private const string SitemapContent = """
        <?xml version="1.0" encoding="UTF-8"?>
        <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"
                xmlns:xhtml="http://www.w3.org/1999/xhtml">
            <url>
                <loc>https://lavaae.app/</loc>
                <lastmod>2026-11-27</lastmod>
                <changefreq>weekly</changefreq>
                <priority>1.0</priority>
            </url>
            <url>
                <loc>https://lavaae.app/authentication/login</loc>
                <lastmod>2026-11-27</lastmod>
                <changefreq>monthly</changefreq>
                <priority>0.8</priority>
            </url>
            <url>
                <loc>https://lavaae.app/privacidade</loc>
                <lastmod>2026-11-27</lastmod>
                <changefreq>monthly</changefreq>
                <priority>0.6</priority>
            </url>
            <url>
                <loc>https://lavaae.app/termos</loc>
                <lastmod>2026-11-27</lastmod>
                <changefreq>monthly</changefreq>
                <priority>0.6</priority>
            </url>
            <url>
                <loc>https://lavaae.app/dpo</loc>
                <lastmod>2026-11-27</lastmod>
                <changefreq>monthly</changefreq>
                <priority>0.5</priority>
            </url>
            <url>
                <loc>https://lavaae.app/llms.txt</loc>
                <lastmod>2026-11-27</lastmod>
                <changefreq>monthly</changefreq>
                <priority>0.7</priority>
            </url>
            <url>
                <loc>https://lavaae.app/llms-full.txt</loc>
                <lastmod>2026-11-27</lastmod>
                <changefreq>monthly</changefreq>
                <priority>0.7</priority>
            </url>
        </urlset>
        """;

    public static IEndpointRouteBuilder MapPublicSeoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/public/landing", async (
            PublicLandingPageService service,
            CancellationToken ct) =>
        {
            var html = await service.GetLandingHtmlAsync(ct);
            if (string.IsNullOrWhiteSpace(html))
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Content(html, "text/html; charset=utf-8", Encoding.UTF8);
        }).AllowAnonymous();

        // Serve /public/llms.txt and /llms.txt directly without authentication
        var handleLlmsTxt = async (
            PublicLandingPageService service,
            CancellationToken ct) =>
        {
            var llms = await service.GetLlmsTxtAsync(ct);
            if (string.IsNullOrWhiteSpace(llms))
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Content(llms, "text/plain; charset=utf-8", Encoding.UTF8);
        };

        app.MapGet("/public/llms.txt", handleLlmsTxt).AllowAnonymous();
        app.MapGet("/llms.txt", handleLlmsTxt).AllowAnonymous();

        app.MapGet("/robots.txt", () =>
            Results.Content(RobotsContent, "text/plain; charset=utf-8", Encoding.UTF8)
        ).AllowAnonymous();

        app.MapGet("/sitemap.xml", () =>
            Results.Content(SitemapContent, "application/xml; charset=utf-8", Encoding.UTF8)
        ).AllowAnonymous();

        return app;
    }
}
