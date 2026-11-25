using System.Security.Claims;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarWashSaaS.Api.Endpoints;

public static class PlatformCampaignsEndpoints
{
    public static IEndpointRouteBuilder MapPlatformCampaignsEndpoints(this IEndpointRouteBuilder app)
    {
        var platformCampaigns = app.MapGroup("/platform/campaigns");

        // Listar campanhas
        platformCampaigns.MapGet("/", async (
            string? searchTerm,
            bool? isActive,
            CampaignLinkApplicationService service,
            CancellationToken ct) =>
        {
            var result = await service.ListCampaignsAsync(searchTerm, isActive, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        }).RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformAuditor);

        // Obter detalhes por ID
        platformCampaigns.MapGet("/{id:guid}", async (
            Guid id,
            CampaignLinkApplicationService service,
            CancellationToken ct) =>
        {
            var result = await service.GetCampaignByIdAsync(id, ct);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Value);
            }

            return result.Error!.Type == ErrorType.NotFound
                ? Results.NotFound(new { result.Error.Code, result.Error.Description })
                : Results.BadRequest(new { result.Error.Code, result.Error.Description });
        }).RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformAuditor);

        // Criar campanha
        platformCampaigns.MapPost("/", async (
            CreateCampaignLinkRequest request,
            CampaignLinkApplicationService service,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken ct) =>
        {
            var actorIdClaim = user.FindFirst("sub")?.Value;
            var actorEmailClaim = user.FindFirst("email")?.Value ?? "admin@lavaway.com";
            var actorId = Guid.TryParse(actorIdClaim, out var parsedId) ? parsedId : Guid.NewGuid();

            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var userAgent = context.Request.Headers.UserAgent.ToString();

            var result = await service.CreateCampaignAsync(
                request,
                actorId,
                actorEmailClaim,
                ipAddress,
                userAgent,
                ct);

            if (result.IsSuccess)
            {
                return Results.Created($"/platform/campaigns/{result.Value!.Id}", result.Value);
            }

            return result.Error!.Type switch
            {
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformSupport);

        // Atualizar campanha
        platformCampaigns.MapPut("/{id:guid}", async (
            Guid id,
            UpdateCampaignLinkRequest request,
            CampaignLinkApplicationService service,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken ct) =>
        {
            var actorIdClaim = user.FindFirst("sub")?.Value;
            var actorEmailClaim = user.FindFirst("email")?.Value ?? "admin@lavaway.com";
            var actorId = Guid.TryParse(actorIdClaim, out var parsedId) ? parsedId : Guid.NewGuid();

            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var userAgent = context.Request.Headers.UserAgent.ToString();

            var result = await service.UpdateCampaignAsync(
                id,
                request,
                actorId,
                actorEmailClaim,
                ipAddress,
                userAgent,
                ct);

            if (result.IsSuccess)
            {
                return Results.Ok(result.Value);
            }

            return result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformSupport);

        // Alternar status ativo/inativo
        platformCampaigns.MapPatch("/{id:guid}/status", async (
            Guid id,
            UpdateCampaignStatusRequest request,
            CampaignLinkApplicationService service,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken ct) =>
        {
            var actorIdClaim = user.FindFirst("sub")?.Value;
            var actorEmailClaim = user.FindFirst("email")?.Value ?? "admin@lavaway.com";
            var actorId = Guid.TryParse(actorIdClaim, out var parsedId) ? parsedId : Guid.NewGuid();

            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var userAgent = context.Request.Headers.UserAgent.ToString();

            var result = await service.ToggleStatusAsync(
                id,
                request.IsActive,
                actorId,
                actorEmailClaim,
                ipAddress,
                userAgent,
                ct);

            if (result.IsSuccess)
            {
                return Results.Ok(result.Value);
            }

            return result.Error!.Type == ErrorType.NotFound
                ? Results.NotFound(new { result.Error.Code, result.Error.Description })
                : Results.BadRequest(new { result.Error.Code, result.Error.Description });
        }).RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformSupport);

        // Preview e download de QR Code
        platformCampaigns.MapGet("/{id:guid}/qrcode", async (
            Guid id,
            string? format,
            int? pixels,
            bool? download,
            CampaignLinkApplicationService service,
            CancellationToken ct) =>
        {
            var isPng = string.Equals(format, "png", StringComparison.OrdinalIgnoreCase);
            var px = pixels is > 0 and <= 50 ? pixels.Value : 10;

            if (isPng)
            {
                var pngResult = await service.GetQrCodePngBytesAsync(id, px, ct);
                if (!pngResult.IsSuccess)
                {
                    return Results.NotFound(new { pngResult.Error!.Code, pngResult.Error.Description });
                }

                var filename = $"qrcode-campaign-{id}.png";
                return download == true
                    ? Results.File(pngResult.Value!, "image/png", fileDownloadName: filename)
                    : Results.File(pngResult.Value!, "image/png");
            }

            var svgResult = await service.GetQrCodePreviewAsync(id, ct);
            if (!svgResult.IsSuccess)
            {
                return Results.NotFound(new { svgResult.Error!.Code, svgResult.Error.Description });
            }

            if (download == true)
            {
                var svgBytes = System.Text.Encoding.UTF8.GetBytes(svgResult.Value!.SvgContent);
                return Results.File(svgBytes, "image/svg+xml", fileDownloadName: $"qrcode-campaign-{id}.svg");
            }

            return Results.Ok(svgResult.Value);
        }).RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformAuditor);

        // Redirecionamento público de Link Curto: /c/{slug} e /api/c/{slug}
        app.MapGet("/c/{slug}", async (
            string slug,
            CampaignLinkApplicationService service,
            CancellationToken ct) =>
        {
            var result = await service.ResolveRedirectTargetAsync(slug, ct);
            if (result.IsSuccess)
            {
                return Results.Redirect(result.Value!, permanent: false);
            }

            return Results.Redirect("/?redirect_error=campaign_unavailable", permanent: false);
        }).AllowAnonymous();

        app.MapGet("/api/c/{slug}", async (
            string slug,
            CampaignLinkApplicationService service,
            CancellationToken ct) =>
        {
            var result = await service.ResolveRedirectTargetAsync(slug, ct);
            if (result.IsSuccess)
            {
                return Results.Redirect(result.Value!, permanent: false);
            }

            return Results.Redirect("/?redirect_error=campaign_unavailable", permanent: false);
        }).AllowAnonymous();

        return app;
    }
}
