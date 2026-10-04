using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CarWashSaaS.Api.Middleware;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Application;

namespace CarWashSaaS.Api.Endpoints;

public static class WhatsAppEndpoints
{
    public static IEndpointRouteBuilder MapWhatsAppEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/whatsapp");

        group.MapPost("/webhooks/evolution", async (
            HttpRequest request,
            IConfiguration configuration,
            CurrentTenantAccessor currentTenantAccessor,
            WhatsAppConnectionApplicationService connectionService,
            WhatsAppMessageApplicationService messageService,
            CancellationToken ct) =>
        {
            var expectedSecret = configuration["WhatsApp:EvolutionApi:WebhookSecret"];
            var providedSecret = request.Headers["X-Webhook-Secret"].ToString();
            if (string.IsNullOrWhiteSpace(expectedSecret))
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var expectedBytes = Encoding.UTF8.GetBytes(expectedSecret);
            var providedBytes = Encoding.UTF8.GetBytes(providedSecret);
            if (!CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
            {
                return Results.Unauthorized();
            }

            JsonDocument payload;
            try
            {
                payload = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Invalid webhook JSON." });
            }

            using (payload)
            {
                var root = payload.RootElement;
                if (!TryGetJsonString(root, "event", out var eventName) ||
                    !TryGetJsonString(root, "instance", out var instanceName))
                {
                    return Results.BadRequest(new { error = "Webhook event and instance are required." });
                }

                var instancePrefix = configuration["WhatsApp:EvolutionApi:InstanceNamePrefix"] ?? "lavaway";
                var expectedInstancePrefix = $"{instancePrefix}-";
                if (!instanceName.StartsWith(expectedInstancePrefix, StringComparison.Ordinal) ||
                    !Guid.TryParseExact(instanceName[expectedInstancePrefix.Length..], "N", out var tenantId) ||
                    tenantId == Guid.Empty)
                {
                    return Results.BadRequest(new { error = "Webhook instance is invalid." });
                }

                currentTenantAccessor.SetTenant(tenantId);
                var normalizedEvent = eventName.Replace('.', '_').ToLowerInvariant();

                if (normalizedEvent == "connection_update")
                {
                    if (!root.TryGetProperty("data", out var data) || !TryGetJsonString(data, "state", out var providerState))
                    {
                        return Results.BadRequest(new { error = "Connection state is required." });
                    }

                    var result = await connectionService.ApplyProviderStatusAsync(tenantId, instanceName, providerState, ct);
                    if (result.IsSuccess || result.Error!.Code is "whatsapp.not_found" or "whatsapp.provider_session.mismatch")
                    {
                        return Results.NoContent();
                    }

                    return result.Error.Type == ErrorType.Validation
                        ? Results.BadRequest(new { result.Error.Code, result.Error.Description })
                        : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status500InternalServerError);
                }

                if (normalizedEvent is "messages_update" or "send_message")
                {
                    if (root.TryGetProperty("data", out var data))
                    {
                        var providerMessageId = string.Empty;
                        var status = string.Empty;

                        if (data.TryGetProperty("key", out var key) && TryGetJsonString(key, "id", out var keyId))
                        {
                            providerMessageId = keyId;
                        }
                        else if (TryGetJsonString(data, "id", out var directId))
                        {
                            providerMessageId = directId;
                        }

                        if (TryGetJsonString(data, "status", out var directStatus))
                        {
                            status = directStatus;
                        }

                        if (!string.IsNullOrWhiteSpace(providerMessageId) && !string.IsNullOrWhiteSpace(status))
                        {
                            await messageService.ProcessDeliveryWebhookAsync(tenantId, providerMessageId, status, ct);
                        }
                    }

                    return Results.NoContent();
                }

                return Results.Ok(new { ignored = true });
            }
        }).AllowAnonymous();

        group.MapGet("/status", async (ICurrentTenantAccessor currentTenantAccessor, WhatsAppConnectionApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetConnectionAsync(tenantId);
            if (!result.IsSuccess)
            {
                return result.Error!.Type switch
                {
                    ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                    _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
                };
            }

            var connection = result.Value;
            var status = connection?.Status.ToString().ToLowerInvariant() ?? WhatsAppStatusConstants.Disconnected;
            var dto = new WhatsAppConnectionDto(status, connection?.QrCodeValue, connection?.UpdatedAt);
            return Results.Ok(dto);
        }).RequireAuthorization();

        group.MapPost("/pairing/start", async (ICurrentTenantAccessor currentTenantAccessor, WhatsAppConnectionApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.StartPairingAsync(tenantId);
            if (!result.IsSuccess)
            {
                return result.Error!.Type switch
                {
                    ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                    ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                    _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
                };
            }

            var connection = result.Value!;
            var dto = new WhatsAppConnectionDto(
                connection.Status.ToString().ToLowerInvariant(),
                connection.QrCodeValue,
                connection.UpdatedAt);
            return Results.Ok(dto);
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        group.MapPost("/pairing/refresh", async (ICurrentTenantAccessor currentTenantAccessor, WhatsAppConnectionApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.RefreshPairingAsync(tenantId);
            if (!result.IsSuccess)
            {
                return result.Error!.Type switch
                {
                    ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                    ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                    _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
                };
            }

            var connection = result.Value!;
            var dto = new WhatsAppConnectionDto(
                connection.Status.ToString().ToLowerInvariant(),
                connection.QrCodeValue,
                connection.UpdatedAt);
            return Results.Ok(dto);
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        group.MapPost("/pairing/disconnect", async (ICurrentTenantAccessor currentTenantAccessor, WhatsAppConnectionApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.DisconnectAsync(tenantId);
            if (!result.IsSuccess)
            {
                return result.Error!.Type switch
                {
                    ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                    ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                    _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
                };
            }

            var connection = result.Value!;
            var dto = new WhatsAppConnectionDto(
                connection.Status.ToString().ToLowerInvariant(),
                connection.QrCodeValue,
                connection.UpdatedAt);
            return Results.Ok(dto);
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        group.MapPost("/messages/test", async (
            SendWhatsAppTestMessageRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            WhatsAppMessageApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.SendTestMessageAsync(tenantId, request.RecipientPhone, request.MessageText, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        group.MapGet("/messages", async (
            ICurrentTenantAccessor currentTenantAccessor,
            WhatsAppMessageApplicationService service,
            int? count,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetRecentMessagesAsync(tenantId, count ?? 20, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        group.MapGet("/messages/{id:guid}", async (
            Guid id,
            ICurrentTenantAccessor currentTenantAccessor,
            WhatsAppMessageApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetMessageByIdAsync(tenantId, id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                _ => Results.BadRequest(new { result.Error.Code, result.Error.Description })
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        group.MapGet("/quota", async (
            ICurrentTenantAccessor currentTenantAccessor,
            WhatsAppMessageApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetQuotaAsync(tenantId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        return app;
    }

    private static bool TryGetJsonString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(value = property.GetString() ?? string.Empty);
    }
}
