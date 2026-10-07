using CarWashSaaS.Api.Services;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Api.Endpoints;

public static class PlatformObservabilityEndpoints
{
    public static IEndpointRouteBuilder MapPlatformObservabilityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/platform")
            .RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformUser);

        group.MapGet("/metrics/overview", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            Guid? tenantId,
            PlatformObservabilityApplicationService service,
            CancellationToken ct) =>
        {
            var request = new GetPlatformMetricsOverviewRequest(from, to, tenantId);
            var result = await service.GetOverviewAsync(request, ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        });

        group.MapGet("/observability/webhooks", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? provider,
            string? status,
            int? page,
            int? pageSize,
            PlatformObservabilityApplicationService service,
            CancellationToken ct) =>
        {
            var request = new GetPlatformWebhookLogsRequest(
                FromUtc: from,
                ToUtc: to,
                Provider: provider,
                Status: status,
                Page: page ?? 1,
                PageSize: pageSize ?? 20);

            var result = await service.GetWebhookLogsAsync(request, ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        });

        group.MapGet("/observability/whatsapp-failures", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            Guid? tenantId,
            int? page,
            int? pageSize,
            PlatformObservabilityApplicationService service,
            CancellationToken ct) =>
        {
            var request = new GetPlatformWhatsAppFailuresRequest(
                FromUtc: from,
                ToUtc: to,
                TenantId: tenantId,
                Page: page ?? 1,
                PageSize: pageSize ?? 20);

            var result = await service.GetWhatsAppFailuresAsync(request, ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        });

        return app;
    }
}
