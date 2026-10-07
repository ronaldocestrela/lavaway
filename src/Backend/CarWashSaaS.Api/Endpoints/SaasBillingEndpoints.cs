using CarWashSaaS.Billing.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Api.Endpoints;

public static class SaasBillingEndpoints
{
    public static IEndpointRouteBuilder MapSaasBillingEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. Webhook de Billing da Plataforma (Recepção de eventos do Asaas/Stripe/Iugu/Simulado)
        app.MapPost("/platform/billing/webhooks", async (
            HttpContext httpContext,
            SaasBillingWebhookApplicationService webhookService,
            CancellationToken ct) =>
        {
            using var reader = new StreamReader(httpContext.Request.Body);
            var rawBody = await reader.ReadToEndAsync(ct);

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return Results.BadRequest(new { error = "Empty webhook payload." });
            }

            SaasBillingWebhookPayload? payload;
            try
            {
                payload = System.Text.Json.JsonSerializer.Deserialize<SaasBillingWebhookPayload>(rawBody, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (System.Text.Json.JsonException)
            {
                return Results.BadRequest(new { error = "Invalid JSON payload." });
            }

            if (payload is null)
            {
                return Results.BadRequest(new { error = "Deserialized payload is null." });
            }

            var signatureHeader = httpContext.Request.Headers["X-Signature"].FirstOrDefault();
            var webhookSecret = "lavaway_webhook_secret_key_2026";

            var result = await webhookService.ProcessWebhookAsync(payload, rawBody, signatureHeader, webhookSecret, ct);
            return result.IsSuccess ? Results.Ok(new { status = "processed" }) : MapErrorToResult(result.Error!);
        }).AllowAnonymous();

        // 2. Backoffice da Plataforma - Gestão de Assinaturas de Todos os Estabelecimentos
        var platformBilling = app.MapGroup("/platform/billing")
            .RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformBillingAdmin);

        platformBilling.MapGet("/subscriptions", async (
            PlatformSaasBillingApplicationService service,
            CancellationToken ct) =>
        {
            var result = await service.ListAllTenantSubscriptionsAsync(ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        });

        platformBilling.MapPost("/subscriptions/{tenantId:guid}/override", async (
            Guid tenantId,
            AdminOverridePlanRequest request,
            PlatformSaasBillingApplicationService service,
            CancellationToken ct) =>
        {
            var result = await service.AdminOverridePlanAsync(tenantId, request, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        });

        // 3. Portal do Lojista (Tenant) - Visão da Própria Assinatura, Cotas e Faturas
        var tenantSettings = app.MapGroup("/settings/subscription")
            .RequireAuthorization();

        tenantSettings.MapGet("", async (
            ICurrentTenantAccessor tenantAccessor,
            TenantSaasSubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (tenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetOverviewAsync(tenantId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        });

        tenantSettings.MapGet("/plans", async (
            TenantSaasSubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            var result = await service.ListPlansAsync(ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        });

        tenantSettings.MapPost("/change-plan", async (
            ChangePlanRequest request,
            ICurrentTenantAccessor tenantAccessor,
            TenantSaasSubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (tenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.ChangePlanAsync(tenantId, request.TargetTier, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        tenantSettings.MapPost("/invoices/{invoiceId:guid}/settle", async (
            Guid invoiceId,
            ICurrentTenantAccessor tenantAccessor,
            TenantSaasSubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (tenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.SimulateSettleInvoiceAsync(tenantId, invoiceId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        return app;
    }

    private static IResult MapErrorToResult(Error error) =>
        error.Type switch
        {
            ErrorType.NotFound => Results.NotFound(new { error.Code, error.Description }),
            ErrorType.Conflict => Results.Conflict(new { error.Code, error.Description }),
            ErrorType.Unauthorized => Results.Forbid(),
            _ => Results.BadRequest(new { error.Code, error.Description })
        };
}
