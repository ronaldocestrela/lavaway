using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarWashSaaS.WhatsApp.Infrastructure;

public sealed class EvolutionApiWhatsAppPairingProvider : IWhatsAppPairingProvider, IWhatsAppHealthCheckProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _instanceNamePrefix;
    private readonly string? _webhookUrl;

    [ActivatorUtilitiesConstructor]
    public EvolutionApiWhatsAppPairingProvider(HttpClient httpClient, IConfiguration configuration)
        : this(httpClient,
            configuration["WhatsApp:EvolutionApi:ApiKey"] ?? throw new InvalidOperationException("WhatsApp:EvolutionApi:ApiKey must be configured."),
            configuration["WhatsApp:EvolutionApi:InstanceNamePrefix"] ?? "lavaway",
            configuration["WhatsApp:EvolutionApi:WebhookUrl"])
    {
        if (string.IsNullOrWhiteSpace(configuration["WhatsApp:EvolutionApi:BaseUrl"]))
        {
            throw new InvalidOperationException("WhatsApp:EvolutionApi:BaseUrl must be configured.");
        }

        _httpClient.BaseAddress = new Uri(configuration["WhatsApp:EvolutionApi:BaseUrl"]!);
    }

    public EvolutionApiWhatsAppPairingProvider(
        HttpClient httpClient,
        string apiKey,
        string instanceNamePrefix = "lavaway",
        string? webhookUrl = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? throw new ArgumentException("Api key is required.", nameof(apiKey)) : apiKey;
        _instanceNamePrefix = string.IsNullOrWhiteSpace(instanceNamePrefix) ? "lavaway" : instanceNamePrefix;
        _webhookUrl = webhookUrl;
    }

    public async Task<Result<WhatsAppProviderHealthState>> CheckHealthAsync(string providerSessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerSessionId))
        {
            return Result<WhatsAppProviderHealthState>.Failure(new Error(
                "whatsapp.provider_session.invalid",
                "A valid provider session is required.",
                ErrorType.Validation));
        }

        ct.ThrowIfCancellationRequested();

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"instance/connectionState/{Uri.EscapeDataString(providerSessionId)}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("apikey", _apiKey);

            using var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<WhatsAppProviderHealthState>.Success(new WhatsAppProviderHealthState(
                    IsReachable: false,
                    State: "disconnected",
                    Details: $"HTTP {(int)response.StatusCode}: {responseBody}"));
            }

            var state = "disconnected";
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            if (root.TryGetProperty("instance", out var inst) && inst.TryGetProperty("state", out var st))
            {
                state = st.GetString() ?? "disconnected";
            }
            else if (root.TryGetProperty("state", out var directSt))
            {
                state = directSt.GetString() ?? "disconnected";
            }

            return Result<WhatsAppProviderHealthState>.Success(new WhatsAppProviderHealthState(
                IsReachable: true,
                State: state,
                Details: responseBody));
        }
        catch (HttpRequestException ex)
        {
            return Result<WhatsAppProviderHealthState>.Success(new WhatsAppProviderHealthState(
                IsReachable: false,
                State: "disconnected",
                Details: ex.Message));
        }
    }

    public async Task<Result> DisconnectAsync(string providerSessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerSessionId))
        {
            return Result.Failure(new Error(
                "whatsapp.provider_session.invalid",
                "A valid provider session is required.",
                ErrorType.Validation));
        }

        ct.ThrowIfCancellationRequested();

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"instance/logout/{Uri.EscapeDataString(providerSessionId)}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("apikey", _apiKey);

            using var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure(new Error(
                    "whatsapp.provider.http_error",
                    $"Evolution API returned status {(int)response.StatusCode}: {responseBody}",
                    ErrorType.Unavailable));
            }

            if (TryGetProviderError(responseBody, out var providerError))
            {
                return Result.Failure(new Error(
                    "whatsapp.provider.error",
                    providerError,
                    ErrorType.Unavailable));
            }

            return Result.Success();
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure(new Error(
                "whatsapp.provider.network_error",
                ex.Message,
                ErrorType.Unavailable));
        }
    }


    public async Task<Result<(string ProviderSessionId, string QrCodeValue)>> GeneratePairingAsync(Guid tenantId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var instanceName = BuildInstanceName(tenantId);

        try
        {
            var createRequest = new HttpRequestMessage(HttpMethod.Post, "instance/create");
            createRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            createRequest.Headers.Add("apikey", _apiKey);

            var createPayload = new Dictionary<string, object?>
            {
                ["instanceName"] = instanceName,
                ["qrcode"] = true,
                ["integration"] = "WHATSAPP-BAILEYS"
            };

            if (!string.IsNullOrWhiteSpace(_webhookUrl))
            {
                createPayload["webhook"] = new
                {
                    enabled = true,
                    url = _webhookUrl,
                    byEvents = false,
                    base64 = false,
                    events = new[]
                    {
                        "CONNECTION_UPDATE",
                        "MESSAGES_UPSERT",
                        "MESSAGES_UPDATE",
                        "SEND_MESSAGE"
                    }
                };
            }

            createRequest.Content = new StringContent(
                JsonSerializer.Serialize(createPayload),
                Encoding.UTF8,
                "application/json");

            var createResponse = await _httpClient.SendAsync(createRequest, ct);
            using (createResponse)
            {
                var responseBody = await createResponse.Content.ReadAsStringAsync(ct);

                if (createResponse.IsSuccessStatusCode)
                {
                    var qrCode = ExtractQrCodeValue(responseBody);
                    if (!string.IsNullOrWhiteSpace(qrCode))
                    {
                        return Result<(string ProviderSessionId, string QrCodeValue)>.Success((instanceName, qrCode));
                    }

                    if (TryGetProviderError(responseBody, out var providerError))
                    {
                        return ProviderFailure("whatsapp.provider.error", providerError, ErrorType.Unavailable);
                    }
                }
                else if (createResponse.StatusCode is not (System.Net.HttpStatusCode.Forbidden
                             or System.Net.HttpStatusCode.Conflict))
                {
                    return CreateHttpFailure(createResponse.StatusCode, responseBody);
                }
            }

            var connectRequest = new HttpRequestMessage(HttpMethod.Get, $"instance/connect/{instanceName}");
            connectRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            connectRequest.Headers.Add("apikey", _apiKey);

            var connectResponse = await _httpClient.SendAsync(connectRequest, ct);
            using (connectResponse)
            {
                var responseBody = await connectResponse.Content.ReadAsStringAsync(ct);

                if (connectResponse.IsSuccessStatusCode)
                {
                    var qrCode = ExtractQrCodeValue(responseBody);
                    if (!string.IsNullOrWhiteSpace(qrCode))
                    {
                        return Result<(string ProviderSessionId, string QrCodeValue)>.Success((instanceName, qrCode));
                    }

                    if (TryGetProviderError(responseBody, out var providerError))
                    {
                        return ProviderFailure("whatsapp.provider.error", providerError, ErrorType.Unavailable);
                    }
                }
                else
                {
                    return CreateHttpFailure(connectResponse.StatusCode, responseBody);
                }
            }

            return ProviderFailure(
                "whatsapp.provider.qr_code_unavailable",
                "Evolution API did not return a QR code for the WhatsApp instance.",
                ErrorType.Unavailable);
        }
        catch (HttpRequestException ex)
        {
            return ProviderFailure("whatsapp.provider.network_error", ex.Message, ErrorType.Unavailable);
        }
    }

    private static Result<(string ProviderSessionId, string QrCodeValue)> CreateHttpFailure(
        System.Net.HttpStatusCode statusCode,
        string responseBody)
        => ProviderFailure(
            "whatsapp.provider.http_error",
            $"Evolution API returned status {(int)statusCode}: {responseBody}",
            ErrorType.Unavailable);

    private static Result<(string ProviderSessionId, string QrCodeValue)> ProviderFailure(
        string code,
        string description,
        ErrorType type)
        => Result<(string ProviderSessionId, string QrCodeValue)>.Failure(new Error(code, description, type));

    private static bool TryGetProviderError(string responseBody, out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("error", out var errorElement) ||
                errorElement.ValueKind is not (JsonValueKind.True or JsonValueKind.String))
            {
                return false;
            }

            message = root.TryGetProperty("message", out var messageElement) &&
                      messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString() ?? "Evolution API reported an error."
                : errorElement.ValueKind == JsonValueKind.String
                    ? errorElement.GetString() ?? "Evolution API reported an error."
                    : "Evolution API reported an error.";
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private string BuildInstanceName(Guid tenantId)
        => $"{_instanceNamePrefix}-{tenantId:N}";

    private static string ExtractQrCodeValue(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            return ExtractFromElement(root);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string ExtractFromElement(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        // 1. Prioritize compact WhatsApp pairing code (e.g., 2@..., ~240 chars)
        // This ensures the value fits the 2000-char domain and database column limit.
        if (TryGetNonEmptyString(element, "code", out var code) && code.Length <= 2000)
        {
            return code;
        }

        // 2. Property "qrcode" (can be string or nested object)
        if (element.TryGetProperty("qrcode", out var qrcodeProp))
        {
            if (qrcodeProp.ValueKind == JsonValueKind.String)
            {
                var val = qrcodeProp.GetString();
                if (!string.IsNullOrWhiteSpace(val) && val.Length <= 2000) return val;
            }
            else if (qrcodeProp.ValueKind == JsonValueKind.Object)
            {
                var nested = ExtractFromElement(qrcodeProp);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }

        // 3. Property "qrCode" (camelCase)
        if (element.TryGetProperty("qrCode", out var qrCodeProp))
        {
            if (qrCodeProp.ValueKind == JsonValueKind.String)
            {
                var val = qrCodeProp.GetString();
                if (!string.IsNullOrWhiteSpace(val) && val.Length <= 2000) return val;
            }
            else if (qrCodeProp.ValueKind == JsonValueKind.Object)
            {
                var nested = ExtractFromElement(qrCodeProp);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }

        // 4. Data wrapper
        if (element.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Object)
        {
            var nested = ExtractFromElement(dataProp);
            if (!string.IsNullOrWhiteSpace(nested)) return nested;
        }

        // 5. QR wrapper
        if (element.TryGetProperty("qr", out var qrProp) && qrProp.ValueKind == JsonValueKind.Object)
        {
            var nested = ExtractFromElement(qrProp);
            if (!string.IsNullOrWhiteSpace(nested)) return nested;
        }

        // 6. Base64 image fallback (only if <= 2000 chars, e.g. in mock tests)
        if (TryGetNonEmptyString(element, "base64", out var base64) && base64.Length <= 2000)
        {
            return base64;
        }

        return string.Empty;
    }

    private static bool TryGetNonEmptyString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (element.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            var str = prop.GetString();
            if (!string.IsNullOrWhiteSpace(str))
            {
                value = str;
                return true;
            }
        }

        return false;
    }
}
