using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CarWashSaaS.WhatsApp.Application;
using Microsoft.Extensions.Configuration;

namespace CarWashSaaS.WhatsApp.Infrastructure;

public sealed class EvolutionApiWhatsAppPairingProvider : IWhatsAppPairingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _instanceNamePrefix;

    public EvolutionApiWhatsAppPairingProvider(HttpClient httpClient, IConfiguration configuration)
        : this(httpClient,
            configuration["WhatsApp:EvolutionApi:ApiKey"] ?? throw new InvalidOperationException("WhatsApp:EvolutionApi:ApiKey must be configured."),
            configuration["WhatsApp:EvolutionApi:InstanceNamePrefix"] ?? "lavaway")
    {
        if (string.IsNullOrWhiteSpace(configuration["WhatsApp:EvolutionApi:BaseUrl"]))
        {
            throw new InvalidOperationException("WhatsApp:EvolutionApi:BaseUrl must be configured.");
        }

        _httpClient.BaseAddress = new Uri(configuration["WhatsApp:EvolutionApi:BaseUrl"]!);
    }

    public EvolutionApiWhatsAppPairingProvider(HttpClient httpClient, string apiKey, string instanceNamePrefix = "lavaway")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? throw new ArgumentException("Api key is required.", nameof(apiKey)) : apiKey;
        _instanceNamePrefix = string.IsNullOrWhiteSpace(instanceNamePrefix) ? "lavaway" : instanceNamePrefix;
    }

    public async Task<(string ProviderSessionId, string QrCodeValue)> GeneratePairingAsync(Guid tenantId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var instanceName = BuildInstanceName(tenantId);
        var request = new HttpRequestMessage(HttpMethod.Post, $"instance/connect/{instanceName}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("apikey", _apiKey);
        request.Content = new StringContent("{\"webhook\":false}", Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var fallbackQr = $"evolution:{instanceName}:{DateTime.UtcNow:O}";
            return (instanceName, fallbackQr);
        }

        var qrCodeValue = ExtractQrCodeValue(responseBody, instanceName);
        return (instanceName, qrCodeValue);
    }

    private string BuildInstanceName(Guid tenantId)
        => $"{_instanceNamePrefix}-{tenantId:N}";

    private static string ExtractQrCodeValue(string responseBody, string instanceName)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return $"evolution:{instanceName}:{DateTime.UtcNow:O}";
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            if (TryGetString(root, ["qrcode"], out var qrcode))
            {
                return qrcode;
            }

            if (TryGetString(root, ["qrCode"], out var qrCode))
            {
                return qrCode;
            }

            if (TryGetString(root, ["data", "qrcode"], out var nestedQrcode))
            {
                return nestedQrcode;
            }

            if (TryGetString(root, ["data", "qrCode"], out var nestedQrCode))
            {
                return nestedQrCode;
            }

            if (TryGetString(root, ["instance", "name"], out var instanceNameValue) && !string.IsNullOrWhiteSpace(instanceNameValue))
            {
                return $"evolution:{instanceNameValue}:{DateTime.UtcNow:O}";
            }
        }
        catch (JsonException)
        {
            // invalid JSON falls through to safe fallback below
        }

        return $"evolution:{instanceName}:{DateTime.UtcNow:O}";
    }

    private static bool TryGetString(JsonElement element, string[] path, out string value)
    {
        value = string.Empty;

        JsonElement current = element;
        for (var i = 0; i < path.Length; i++)
        {
            if (!current.TryGetProperty(path[i], out var next))
            {
                return false;
            }

            current = next;
        }

        if (current.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = current.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }
}
