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

        group.MapPost("/observability/webhooks/retention/run", async (
            int? retentionDays,
            CarWashSaaS.Billing.Application.BillingWebhookPurgeApplicationService billingPurgeService,
            CarWashSaaS.WhatsApp.Application.WhatsAppTransientDataPurgeApplicationService whatsAppPurgeService,
            CarWashSaaS.Shared.Contracts.IAuditTrailService auditTrailService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var effectiveDays = retentionDays ?? 60;
            var billingResult = await billingPurgeService.PurgeOldWebhookPayloadsAsync(effectiveDays, ct);
            if (!billingResult.IsSuccess)
            {
                return Results.BadRequest(new { billingResult.Error!.Code, billingResult.Error.Description });
            }

            var whatsAppResult = await whatsAppPurgeService.PurgeTransientDataAsync(effectiveDays, ct);
            if (!whatsAppResult.IsSuccess)
            {
                return Results.BadRequest(new { whatsAppResult.Error!.Code, whatsAppResult.Error.Description });
            }

            var (paymentPurged, saasPurged) = billingResult.Value;
            var (mediaPurged, sessionsPurged) = whatsAppResult.Value;

            var dto = new WebhookRetentionPurgeResultDto(
                paymentPurged,
                saasPurged,
                mediaPurged,
                sessionsPurged,
                DateTimeOffset.UtcNow);

            var userIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            Guid.TryParse(userIdClaim, out var actorId);
            var actorEmail = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "admin@platform.lavaway";

            await auditTrailService.RecordEventAsync(new RecordAuditEventRequest(
                ActorId: actorId,
                ActorEmail: actorEmail,
                ActorRole: "PlatformBillingAdmin",
                ActorRealm: "Platform",
                Action: "Webhooks.PayloadPurgedLGPD",
                TargetType: "WebhookAuditing",
                TargetId: Guid.NewGuid().ToString("N"),
                TenantId: null,
                IpAddress: httpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent: httpContext.Request.Headers.UserAgent.ToString(),
                DetailsJson: System.Text.Json.JsonSerializer.Serialize(new
                {
                    paymentPurged,
                    saasPurged,
                    mediaPurged,
                    sessionsPurged,
                    effectiveDays
                })), ct);

            return Results.Ok(dto);
        }).RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformBillingAdmin);

        group.MapGet("/observability/database/encryption-status", async (
            ITdeStatusChecker tdeChecker,
            CancellationToken ct) =>
        {
            var result = await tdeChecker.CheckTdeStatusAsync(ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Problem(
                    title: "Falha na verificação de TDE",
                    detail: result.Error!.Description,
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        return app;
    }
}
