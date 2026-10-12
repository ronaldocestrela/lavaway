using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Infrastructure;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Options;

namespace CarWashSaaS.Api.Endpoints;

public static class SaasBillingEndpoints
{
    public static IEndpointRouteBuilder MapSaasBillingEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. Webhook de Billing da Plataforma (Recepção de eventos da Stripe e Gateway Simulado)
        app.MapPost("/platform/billing/webhooks", async (
            HttpContext httpContext,
            SaasBillingWebhookApplicationService webhookService,
            IOptions<StripeBillingOptions> stripeOptions,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("CarWashSaaS.Api.Endpoints.SaasBillingEndpoints");
            using var reader = new StreamReader(httpContext.Request.Body);
            var rawBody = await reader.ReadToEndAsync(ct);

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                logger.LogWarning("Recebida requisição de webhook com corpo vazio.");
                return Results.BadRequest(new { error = "Empty webhook payload." });
            }

            var stripeSignature = httpContext.Request.Headers["Stripe-Signature"].FirstOrDefault();
            SaasBillingWebhookPayload? payload = null;

            if (!string.IsNullOrWhiteSpace(stripeSignature))
            {
                string? currentEventId = null;
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
                    var root = doc.RootElement;
                    var eventId = root.GetProperty("id").GetString() ?? Guid.NewGuid().ToString();
                    currentEventId = eventId;
                    var eventType = root.GetProperty("type").GetString() ?? "unknown";

                    Guid tenantId = Guid.Empty;
                    string? subId = null;
                    string? custId = null;
                    string? invId = null;
                    decimal? amount = null;
                    SaasPlanTier? planTier = null;

                    if (root.TryGetProperty("data", out var dataProp) && dataProp.TryGetProperty("object", out var objProp))
                    {
                        // 1. TenantId via client_reference_id ou metadata
                        if (objProp.TryGetProperty("client_reference_id", out var crProp) &&
                            Guid.TryParse(crProp.GetString(), out var crTenantId))
                        {
                            tenantId = crTenantId;
                        }

                        if (objProp.TryGetProperty("metadata", out var metaProp))
                        {
                            if (tenantId == Guid.Empty)
                            {
                                if (metaProp.TryGetProperty("tenant_id", out var tenantProp) &&
                                    Guid.TryParse(tenantProp.GetString(), out var parsedTenantId))
                                {
                                    tenantId = parsedTenantId;
                                }
                                else if (metaProp.TryGetProperty("tenantId", out var camelTenantProp) &&
                                    Guid.TryParse(camelTenantProp.GetString(), out var parsedCamelTenantId))
                                {
                                    tenantId = parsedCamelTenantId;
                                }
                            }

                            if ((metaProp.TryGetProperty("plan_tier", out var ptProp) || metaProp.TryGetProperty("planTier", out ptProp)) &&
                                Enum.TryParse<SaasPlanTier>(ptProp.GetString(), true, out var parsedPlanTier))
                            {
                                planTier = parsedPlanTier;
                            }
                        }

                        // 1.1 PlanTier alternativo em subscription_details.metadata
                        if (!planTier.HasValue && objProp.TryGetProperty("subscription_details", out var subDetailsProp) &&
                            subDetailsProp.TryGetProperty("metadata", out var subMetaProp))
                        {
                            if ((subMetaProp.TryGetProperty("plan_tier", out var smProp) || subMetaProp.TryGetProperty("planTier", out smProp)) &&
                                Enum.TryParse<SaasPlanTier>(smProp.GetString(), true, out var parsedSubTier))
                            {
                                planTier = parsedSubTier;
                            }
                        }

                        // 1.2 PlanTier alternativo em lines.data[0].metadata
                        if (!planTier.HasValue && objProp.TryGetProperty("lines", out var linesProp) &&
                            linesProp.TryGetProperty("data", out var linesArray) && linesArray.GetArrayLength() > 0)
                        {
                            var firstLine = linesArray[0];
                            if (firstLine.TryGetProperty("metadata", out var lineMeta) &&
                                (lineMeta.TryGetProperty("plan_tier", out var lmTier) || lineMeta.TryGetProperty("planTier", out lmTier)) &&
                                Enum.TryParse<SaasPlanTier>(lmTier.GetString(), true, out var parsedLineTier))
                            {
                                planTier = parsedLineTier;
                            }
                        }

                        // 2. Customer ID
                        if (objProp.TryGetProperty("customer", out var cProp))
                        {
                            if (cProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                custId = cProp.GetString();
                            }
                            else if (cProp.ValueKind == System.Text.Json.JsonValueKind.Object && cProp.TryGetProperty("id", out var cidProp))
                            {
                                custId = cidProp.GetString();
                            }
                        }
                        else if (eventType.StartsWith("customer.") && objProp.TryGetProperty("id", out var custObjId))
                        {
                            custId = custObjId.GetString();
                        }

                        // 3. Subscription ID
                        if (objProp.TryGetProperty("subscription", out var sProp))
                        {
                            if (sProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                subId = sProp.GetString();
                            }
                            else if (sProp.ValueKind == System.Text.Json.JsonValueKind.Object && sProp.TryGetProperty("id", out var sidProp))
                            {
                                subId = sidProp.GetString();
                            }
                        }
                        else if (eventType.StartsWith("customer.subscription.") && objProp.TryGetProperty("id", out var subObjId))
                        {
                            subId = subObjId.GetString();
                        }

                        // 4. Invoice ID
                        if (objProp.TryGetProperty("id", out var idProp) && eventType.StartsWith("invoice."))
                        {
                            invId = idProp.GetString();
                        }
                        else if (objProp.TryGetProperty("invoice", out var invProp) && invProp.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            invId = invProp.GetString();
                        }

                        // 5. Amount
                        if (objProp.TryGetProperty("amount_paid", out var amtProp))
                        {
                            amount = amtProp.GetInt64() / 100m;
                        }
                        else if (objProp.TryGetProperty("amount_due", out var dueProp))
                        {
                            amount = dueProp.GetInt64() / 100m;
                        }
                    }

                    payload = new SaasBillingWebhookPayload(
                        EventId: eventId,
                        EventType: eventType,
                        TenantId: tenantId,
                        GatewaySubscriptionId: subId,
                        GatewayInvoiceId: invId,
                        Amount: amount,
                        TimestampUtc: DateTimeOffset.UtcNow,
                        Signature: stripeSignature,
                        GatewayCustomerId: custId,
                        PlanTier: planTier);

                    logger.LogInformation("Webhook do Stripe recebido: EventId={EventId}, Tipo={EventType}, TenantId={TenantId}",
                        eventId, eventType, tenantId);

                    var secret = stripeOptions.Value.WebhookSecret ?? "lavaway_webhook_secret_key_2026";
                    var stripeResult = await webhookService.ProcessWebhookAsync(payload, rawBody, stripeSignature, secret, ct);

                    if (!stripeResult.IsSuccess)
                    {
                        logger.LogWarning("Webhook do Stripe {EventId} rejeitado com erro: {Code} - {Description}",
                            eventId, stripeResult.Error!.Code, stripeResult.Error.Description);
                        return MapErrorToResult(stripeResult.Error!);
                    }

                    return Results.Ok(new { status = "processed" });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Erro ao processar payload do webhook Stripe para o evento {EventId}: {Message}",
                        currentEventId ?? "desconhecido", ex.Message);
                    return Results.BadRequest(new { error = $"Invalid Stripe payload: {ex.Message}" });
                }
            }

            try
            {
                payload = System.Text.Json.JsonSerializer.Deserialize<SaasBillingWebhookPayload>(rawBody, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (System.Text.Json.JsonException ex)
            {
                logger.LogWarning(ex, "Payload JSON de webhook genérico inválido.");
                return Results.BadRequest(new { error = "Invalid JSON payload." });
            }

            if (payload is null)
            {
                logger.LogWarning("Payload JSON desserializado é nulo.");
                return Results.BadRequest(new { error = "Deserialized payload is null." });
            }

            var signatureHeader = httpContext.Request.Headers["X-Signature"].FirstOrDefault();
            var webhookSecret = "lavaway_webhook_secret_key_2026";

            var result = await webhookService.ProcessWebhookAsync(payload, rawBody, signatureHeader, webhookSecret, ct);
            if (!result.IsSuccess)
            {
                logger.LogWarning("Webhook genérico {EventId} rejeitado: {Code} - {Description}",
                    payload.EventId, result.Error!.Code, result.Error.Description);
            }
            return result.IsSuccess ? Results.Ok(new { status = "processed" }) : MapErrorToResult(result.Error!);
        }).AllowAnonymous();

        // 1.1 Catálogo Público de Planos do SaaS (Landing Page / Home / Contratação)
        app.MapGet("/public/saas-plans", async (
            TenantSaasSubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            var result = await service.ListPlansAsync(ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
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

        platformBilling.MapGet("/plans", async (
            PlatformSaasBillingApplicationService service,
            CancellationToken ct) =>
        {
            var result = await service.ListAllPlansAsync(ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        });

        platformBilling.MapPut("/plans/{tier}", async (
            SaasPlanTier tier,
            UpdateSaasPlanRequest request,
            PlatformSaasBillingApplicationService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdatePlanAsync(tier, request, ct);
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

        tenantSettings.MapPost("/checkout-session", async (
            CreateStripeCheckoutSessionRequest request,
            ICurrentTenantAccessor tenantAccessor,
            TenantSaasSubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (tenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.CreateCheckoutSessionAsync(tenantId, request.TargetTier, request.SuccessUrl, request.CancelUrl, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        tenantSettings.MapPost("/checkout-session/sync", async (
            SyncStripeCheckoutSessionRequest request,
            ICurrentTenantAccessor tenantAccessor,
            TenantSaasSubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (tenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.SyncCheckoutSessionAsync(tenantId, request.SessionId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        tenantSettings.MapPost("/portal-session", async (
            ICurrentTenantAccessor tenantAccessor,
            TenantSaasSubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (tenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.CreateCustomerPortalSessionAsync(tenantId, ct: ct);
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
