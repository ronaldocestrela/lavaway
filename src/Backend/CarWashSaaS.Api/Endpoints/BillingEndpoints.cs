using CarWashSaaS.Api.Middleware;
using CarWashSaaS.Billing.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Api.Endpoints;

public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/billing");

        group.MapPost("/work-orders/{workOrderId:guid}/pix", async (
            Guid workOrderId,
            GenerateWorkOrderPixChargeRequest? request,
            ICurrentTenantAccessor currentTenantAccessor,
            PixBillingApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetOrCreatePixChargeForWorkOrderAsync(
                tenantId,
                workOrderId,
                request?.ExpirationMinutes,
                ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapGet("/work-orders/{workOrderId:guid}/pix", async (
            Guid workOrderId,
            ICurrentTenantAccessor currentTenantAccessor,
            PixBillingApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetPixChargeByWorkOrderIdAsync(tenantId, workOrderId, ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapPost("/work-orders/{workOrderId:guid}/pix/send-whatsapp", async (
            Guid workOrderId,
            SendWorkOrderPixWhatsAppRequest? request,
            ICurrentTenantAccessor currentTenantAccessor,
            PixBillingApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.SendPixChargeToCustomerWhatsAppAsync(
                tenantId,
                workOrderId,
                request?.CustomMessage,
                ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapGet("/work-orders/{workOrderId:guid}/settlement", async (
            Guid workOrderId,
            ICurrentTenantAccessor currentTenantAccessor,
            PixBillingApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetPixChargeByWorkOrderIdAsync(tenantId, workOrderId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapGet("/gateway-config", async (
            ICurrentTenantAccessor currentTenantAccessor,
            TenantPaymentGatewayApplicationService service,
            HttpRequest request,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var baseUrl = $"{request.Scheme}://{request.Host}";
            var result = await service.GetConfigAsync(tenantId, baseUrl, ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        group.MapPut("/gateway-config", async (
            SaveTenantPaymentGatewayConfigRequest saveRequest,
            ICurrentTenantAccessor currentTenantAccessor,
            TenantPaymentGatewayApplicationService service,
            HttpRequest request,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var baseUrl = $"{request.Scheme}://{request.Host}";
            var result = await service.SaveConfigAsync(tenantId, saveRequest, baseUrl, ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        group.MapPost("/gateway-config/test", async (
            TestTenantGatewayConnectionRequest testRequest,
            ICurrentTenantAccessor currentTenantAccessor,
            TenantPaymentGatewayApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.TestConnectionAsync(tenantId, testRequest, ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        group.MapPost("/webhooks/{provider}/{tenantId:guid}", async (
            string provider,
            Guid tenantId,
            HttpRequest request,
            IConfiguration configuration,
            CarWashSaaS.Shared.Configuration.CurrentTenantAccessor currentTenantAccessor,
            IPaymentWebhookValidator webhookValidator,
            PixBillingApplicationService service,
            CancellationToken ct) =>
        {
            currentTenantAccessor.SetTenant(tenantId);

            using var reader = new StreamReader(request.Body, System.Text.Encoding.UTF8);
            var bodyText = await reader.ReadToEndAsync(ct);

            PaymentWebhookPayloadDto payloadDto;

            if (provider.Equals("mercadopago", StringComparison.OrdinalIgnoreCase))
            {
                var configRepo = request.HttpContext.RequestServices.GetService<ITenantPaymentGatewayConfigRepository>();
                var encryptor = request.HttpContext.RequestServices.GetService<IPaymentCredentialsEncryptor>();
                var tenantConfig = configRepo is not null ? await configRepo.GetByTenantIdAsync(tenantId, ct) : null;

                var tenantWebhookSecret = tenantConfig is not null && !string.IsNullOrWhiteSpace(tenantConfig.MercadoPagoWebhookSecretEncrypted) && encryptor is not null
                    ? encryptor.Decrypt(tenantConfig.MercadoPagoWebhookSecretEncrypted)
                    : null;

                var webhookSecret = !string.IsNullOrWhiteSpace(tenantWebhookSecret)
                    ? tenantWebhookSecret
                    : (configuration["Billing:MercadoPago:WebhookSecret"]
                        ?? configuration["Billing:MercadoPago:AccessToken"]
                        ?? "dev-mercadopago-webhook-secret");

                var xSignature = request.Headers["x-signature"].ToString();
                var xRequestId = request.Headers["x-request-id"].ToString();

                string? dataId = null;
                string? action = null;
                string? eventId = null;

                if (!string.IsNullOrWhiteSpace(bodyText))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(bodyText);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("data", out var dataElem) && dataElem.TryGetProperty("id", out var idElem))
                        {
                            dataId = idElem.GetString();
                        }
                        else if (root.TryGetProperty("id", out var rootId))
                        {
                            dataId = rootId.GetString();
                        }

                        if (root.TryGetProperty("action", out var actionElem))
                        {
                            action = actionElem.GetString();
                        }

                        if (root.TryGetProperty("id", out var evIdElem))
                        {
                            eventId = evIdElem.GetString();
                        }
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        return Results.BadRequest(new { error = "Invalid JSON payload." });
                    }
                }

                eventId ??= dataId ?? xRequestId ?? Guid.NewGuid().ToString();

                var validationResult = webhookValidator.ValidateMercadoPagoSignature(xSignature, xRequestId, dataId, webhookSecret);
                if (!validationResult.IsSuccess)
                {
                    return Results.Unauthorized();
                }

                payloadDto = new PaymentWebhookPayloadDto(
                    Provider: "MercadoPago",
                    EventId: eventId,
                    Action: action,
                    PaymentId: dataId,
                    TxId: null,
                    Status: "approved",
                    Amount: null,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    RawPayload: bodyText);
            }
            else if (provider.Equals("pagarme", StringComparison.OrdinalIgnoreCase))
            {
                var configRepo = request.HttpContext.RequestServices.GetService<ITenantPaymentGatewayConfigRepository>();
                var encryptor = request.HttpContext.RequestServices.GetService<IPaymentCredentialsEncryptor>();
                var tenantConfig = configRepo is not null ? await configRepo.GetByTenantIdAsync(tenantId, ct) : null;

                var tenantWebhookSecret = tenantConfig is not null && !string.IsNullOrWhiteSpace(tenantConfig.PagarMeWebhookSecretEncrypted) && encryptor is not null
                    ? encryptor.Decrypt(tenantConfig.PagarMeWebhookSecretEncrypted)
                    : null;

                var webhookSecret = !string.IsNullOrWhiteSpace(tenantWebhookSecret)
                    ? tenantWebhookSecret
                    : (configuration["Billing:PagarMe:WebhookSecret"] ?? "dev-pagarme-webhook-secret");

                var signature = request.Headers["X-Hub-Signature"].ToString();
                if (string.IsNullOrWhiteSpace(signature))
                {
                    signature = request.Headers["X-Webhook-Secret"].ToString();
                }
                if (string.IsNullOrWhiteSpace(signature) && request.Query.TryGetValue("secret", out var secretQuery))
                {
                    signature = secretQuery.ToString();
                }

                var validationResult = webhookValidator.ValidatePagarMeWebhook(signature, bodyText, webhookSecret);
                if (!validationResult.IsSuccess)
                {
                    return Results.Unauthorized();
                }

                string? eventId = null;
                string? eventType = null;
                string? chargeId = null;
                string? transactionId = null;
                string? status = null;
                decimal? amount = null;
                DateTimeOffset? paidAtUtc = null;

                if (!string.IsNullOrWhiteSpace(bodyText))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(bodyText);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("id", out var idElem))
                        {
                            eventId = idElem.GetString();
                        }

                        if (root.TryGetProperty("type", out var typeElem))
                        {
                            eventType = typeElem.GetString();
                        }

                        if (root.TryGetProperty("data", out var dataElem))
                        {
                            if (dataElem.TryGetProperty("id", out var dataIdElem))
                            {
                                chargeId = dataIdElem.GetString();
                            }

                            if (dataElem.TryGetProperty("status", out var statusElem))
                            {
                                status = statusElem.GetString();
                            }

                            if (dataElem.TryGetProperty("paid_amount", out var paidAmountElem) && paidAmountElem.TryGetInt64(out var cents))
                            {
                                amount = cents / 100m;
                            }
                            else if (dataElem.TryGetProperty("amount", out var amountElem) && amountElem.TryGetInt64(out var amtCents))
                            {
                                amount = amtCents / 100m;
                            }

                            if (dataElem.TryGetProperty("paid_at", out var paidAtElem) && DateTimeOffset.TryParse(paidAtElem.GetString(), out var parsedPaidAt))
                            {
                                paidAtUtc = parsedPaidAt;
                            }

                            if (dataElem.TryGetProperty("last_transaction", out var lastTxElem))
                            {
                                if (lastTxElem.TryGetProperty("id", out var txIdElem))
                                {
                                    transactionId = txIdElem.GetString();
                                }
                                if (lastTxElem.TryGetProperty("status", out var txStatusElem))
                                {
                                    status ??= txStatusElem.GetString();
                                }
                            }
                        }
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        return Results.BadRequest(new { error = "Invalid JSON payload." });
                    }
                }

                eventId ??= chargeId ?? Guid.NewGuid().ToString();

                var isPaid = string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(eventType, "charge.paid", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(eventType, "order.paid", StringComparison.OrdinalIgnoreCase);

                payloadDto = new PaymentWebhookPayloadDto(
                    Provider: "PagarMe",
                    EventId: eventId,
                    Action: eventType,
                    PaymentId: chargeId,
                    TxId: transactionId ?? chargeId,
                    Status: isPaid ? "approved" : (status ?? "pending"),
                    Amount: amount,
                    OccurredAtUtc: paidAtUtc ?? DateTimeOffset.UtcNow,
                    RawPayload: bodyText);
            }
            else
            {
                // Provedor simulado / genérico
                var expectedSecret = configuration["Billing:Simulated:WebhookSecret"] ?? "SimulatedDevWebhookSecret2026!";
                var providedSecret = request.Headers["X-Webhook-Secret"].ToString();
                if (string.IsNullOrWhiteSpace(providedSecret) && request.Query.TryGetValue("secret", out var secretQuery))
                {
                    providedSecret = secretQuery.ToString();
                }

                var validationResult = webhookValidator.ValidateSimulatedSecret(providedSecret, expectedSecret);
                if (!validationResult.IsSuccess)
                {
                    return Results.Unauthorized();
                }

                if (!string.IsNullOrWhiteSpace(bodyText))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(bodyText);
                        var root = doc.RootElement;

                        var eventId = root.TryGetProperty("eventId", out var ev) ? ev.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
                        var paymentId = root.TryGetProperty("paymentId", out var p) ? p.GetString() : null;
                        var txId = root.TryGetProperty("txId", out var t) ? t.GetString() : null;
                        var status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "approved" : "approved";
                        decimal? amount = root.TryGetProperty("amount", out var a) && a.TryGetDecimal(out var parsedAmount) ? parsedAmount : null;

                        payloadDto = new PaymentWebhookPayloadDto(
                            Provider: provider,
                            EventId: eventId,
                            Action: "payment.updated",
                            PaymentId: paymentId,
                            TxId: txId,
                            Status: status,
                            Amount: amount,
                            OccurredAtUtc: DateTimeOffset.UtcNow,
                            RawPayload: bodyText);
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        return Results.BadRequest(new { error = "Invalid JSON payload." });
                    }
                }
                else
                {
                    return Results.BadRequest(new { error = "Empty webhook payload." });
                }
            }

            var processResult = await service.ProcessPaymentWebhookAsync(tenantId, payloadDto, ct);
            return processResult.IsSuccess
                ? Results.Ok(processResult.Value)
                : MapErrorToResult(processResult.Error!);
        }).AllowAnonymous();

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
