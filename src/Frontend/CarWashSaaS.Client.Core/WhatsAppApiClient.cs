using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class WhatsAppApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<WhatsAppConnectionDto> GetStatusAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("whatsapp/status", ct);
        return await ReadResponseAsync<WhatsAppConnectionDto>(response, ct);
    }

    public async Task<TenantWhatsAppHealthDetailDto> GetHealthAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("whatsapp/health", ct);
        return await ReadResponseAsync<TenantWhatsAppHealthDetailDto>(response, ct);
    }

    public async Task<WhatsAppConnectionDto> StartPairingAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsync("whatsapp/pairing/start", null, ct);
        return await ReadResponseAsync<WhatsAppConnectionDto>(response, ct);
    }

    public async Task<WhatsAppConnectionDto> RefreshPairingAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsync("whatsapp/pairing/refresh", null, ct);
        return await ReadResponseAsync<WhatsAppConnectionDto>(response, ct);
    }

    public async Task<WhatsAppConnectionDto> DisconnectAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsync("whatsapp/pairing/disconnect", null, ct);
        return await ReadResponseAsync<WhatsAppConnectionDto>(response, ct);
    }

    public async Task<WhatsAppMessageDto> SendTestMessageAsync(SendWhatsAppTestMessageRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("whatsapp/messages/test", request, JsonOptions, ct);
        return await ReadResponseAsync<WhatsAppMessageDto>(response, ct);
    }

    public async Task<IReadOnlyList<WhatsAppMessageDto>> GetRecentMessagesAsync(int count = 20, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"whatsapp/messages?count={count}", ct);
        return await ReadResponseAsync<List<WhatsAppMessageDto>>(response, ct);
    }

    public async Task<WhatsAppQuotaDto> GetQuotaAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("whatsapp/quota", ct);
        return await ReadResponseAsync<WhatsAppQuotaDto>(response, ct);
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await ReadErrorMessageAsync(response, ct);
            throw new WhatsAppApiException(response.StatusCode, errorMessage);
        }

        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        if (value is null)
        {
            throw new WhatsAppApiException(response.StatusCode, "A resposta da API de WhatsApp veio vazia.");
        }

        return value;
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("description", out var desc) && !string.IsNullOrWhiteSpace(desc.GetString()))
            {
                return desc.GetString()!;
            }

            if (doc.RootElement.TryGetProperty("detail", out var detail) && !string.IsNullOrWhiteSpace(detail.GetString()))
            {
                return detail.GetString()!;
            }
        }
        catch
        {
            // Fallback para corpo em texto puro
        }

        var raw = await response.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(raw) ? $"Erro HTTP {(int)response.StatusCode} ({response.StatusCode})." : raw;
    }
}

public sealed class WhatsAppApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
