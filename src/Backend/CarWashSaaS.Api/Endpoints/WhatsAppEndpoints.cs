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
            WhatsAppConnectionApplicationService service,
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

                if (!eventName.Replace('.', '_').Equals("connection_update", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Ok(new { ignored = true });
                }

                var instancePrefix = configuration["WhatsApp:EvolutionApi:InstanceNamePrefix"] ?? "lavaway";
                var expectedInstancePrefix = $"{instancePrefix}-";
                if (!instanceName.StartsWith(expectedInstancePrefix, StringComparison.Ordinal) ||
                    !Guid.TryParseExact(instanceName[expectedInstancePrefix.Length..], "N", out var tenantId) ||
                    tenantId == Guid.Empty)
                {
                    return Results.BadRequest(new { error = "Webhook instance is invalid." });
                }

                if (!root.TryGetProperty("data", out var data) || !TryGetJsonString(data, "state", out var providerState))
                {
                    return Results.BadRequest(new { error = "Connection state is required." });
                }

                currentTenantAccessor.SetTenant(tenantId);
                var result = await service.ApplyProviderStatusAsync(tenantId, instanceName, providerState, ct);
                if (result.IsSuccess || result.Error!.Code is "whatsapp.not_found" or "whatsapp.provider_session.mismatch")
                {
                    return Results.NoContent();
                }

                return result.Error.Type == ErrorType.Validation
                    ? Results.BadRequest(new { result.Error.Code, result.Error.Description })
                    : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status500InternalServerError);
            }
        }).AllowAnonymous();

        group.MapGet("/status", async (ICurrentTenantAccessor currentTenantAccessor, WhatsAppConnectionApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetStatusAsync(tenantId);
            return result.IsSuccess ? Results.Ok(new { status = result.Value!.ToString().ToLowerInvariant() }) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization();

        group.MapPost("/pairing/start", async (ICurrentTenantAccessor currentTenantAccessor, WhatsAppConnectionApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.StartPairingAsync(tenantId);
            return result.IsSuccess ? Results.Ok(new { status = result.Value!.Status.ToString().ToLowerInvariant(), qrCode = result.Value.QrCodeValue }) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        group.MapPost("/pairing/refresh", async (ICurrentTenantAccessor currentTenantAccessor, WhatsAppConnectionApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.RefreshPairingAsync(tenantId);
            return result.IsSuccess ? Results.Ok(new { status = result.Value!.Status.ToString().ToLowerInvariant(), qrCode = result.Value.QrCodeValue }) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
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
