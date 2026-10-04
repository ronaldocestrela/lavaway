using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarWashSaaS.WhatsApp.Infrastructure;

public sealed class EvolutionApiWhatsAppMessageSender : IWhatsAppMessageSender
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _instanceNamePrefix;

    [ActivatorUtilitiesConstructor]
    public EvolutionApiWhatsAppMessageSender(HttpClient httpClient, IConfiguration configuration)
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

    public EvolutionApiWhatsAppMessageSender(HttpClient httpClient, string apiKey, string instanceNamePrefix = "lavaway")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? throw new ArgumentException("Api key is required.", nameof(apiKey)) : apiKey;
        _instanceNamePrefix = string.IsNullOrWhiteSpace(instanceNamePrefix) ? "lavaway" : instanceNamePrefix;
    }

    public async Task<Result<string>> SendTextMessageAsync(Guid tenantId, string recipientPhone, string messageText, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var instanceName = $"{_instanceNamePrefix}-{tenantId:N}";
        var request = new HttpRequestMessage(HttpMethod.Post, $"message/sendText/{instanceName}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("apikey", _apiKey);

        var payload = JsonSerializer.Serialize(new
        {
            number = recipientPhone,
            text = messageText
        });
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            return Result<string>.Failure(new Error("whatsapp.provider.network_error", ex.Message, ErrorType.Validation));
        }

        using (response)
        {
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<string>.Failure(new Error("whatsapp.provider.error",
                    $"Evolution API returned status {(int)response.StatusCode}: {responseBody}",
                    ErrorType.Validation));
            }

            var providerMessageId = ExtractMessageId(responseBody);
            return Result<string>.Success(providerMessageId);
        }
    }

    private static string ExtractMessageId(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return $"evolution-{Guid.CreateVersion7():N}";
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            if (root.TryGetProperty("key", out var keyElement) &&
                keyElement.TryGetProperty("id", out var idElement) &&
                idElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(idElement.GetString()))
            {
                return idElement.GetString()!;
            }

            if (root.TryGetProperty("id", out var directId) &&
                directId.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(directId.GetString()))
            {
                return directId.GetString()!;
            }
        }
        catch (JsonException)
        {
            // fallback
        }

        return $"evolution-{Guid.CreateVersion7():N}";
    }
}
