using System.Text;
using CarWashSaaS.Api.Services;

namespace CarWashSaaS.Api.Endpoints;

public static class PublicSeoEndpoints
{
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

        app.MapGet("/public/llms.txt", async (
            PublicLandingPageService service,
            CancellationToken ct) =>
        {
            var llms = await service.GetLlmsTxtAsync(ct);
            if (string.IsNullOrWhiteSpace(llms))
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Content(llms, "text/plain; charset=utf-8", Encoding.UTF8);
        }).AllowAnonymous();

        return app;
    }
}
