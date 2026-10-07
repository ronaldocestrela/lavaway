using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class PlatformObservabilityApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<PlatformMetricsOverviewDto> GetOverviewAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        Guid? tenantId = null,
        CancellationToken ct = default)
    {
        var queryParams = new List<string>();

        if (from.HasValue)
        {
            queryParams.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
        }

        if (to.HasValue)
        {
            queryParams.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");
        }

        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            queryParams.Add($"tenantId={tenantId.Value}");
        }

        var url = "platform/metrics/overview";
        if (queryParams.Count > 0)
        {
            url += $"?{string.Join("&", queryParams)}";
        }

        using var response = await httpClient.GetAsync(url, ct);
        return await ReadResponseAsync<PlatformMetricsOverviewDto>(response, ct);
    }

    public async Task<IReadOnlyList<PlatformWebhookLogDto>> GetWebhookLogsAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? provider = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var queryParams = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (from.HasValue)
        {
            queryParams.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
        }

        if (to.HasValue)
        {
            queryParams.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");
        }

        if (!string.IsNullOrWhiteSpace(provider))
        {
            queryParams.Add($"provider={Uri.EscapeDataString(provider.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            queryParams.Add($"status={Uri.EscapeDataString(status.Trim())}");
        }

        var url = $"platform/observability/webhooks?{string.Join("&", queryParams)}";
        using var response = await httpClient.GetAsync(url, ct);
        return await ReadResponseAsync<List<PlatformWebhookLogDto>>(response, ct);
    }

    public async Task<IReadOnlyList<PlatformWhatsAppFailureDto>> GetWhatsAppFailuresAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        Guid? tenantId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var queryParams = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (from.HasValue)
        {
            queryParams.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
        }

        if (to.HasValue)
        {
            queryParams.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");
        }

        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            queryParams.Add($"tenantId={tenantId.Value}");
        }

        var url = $"platform/observability/whatsapp-failures?{string.Join("&", queryParams)}";
        using var response = await httpClient.GetAsync(url, ct);
        return await ReadResponseAsync<List<PlatformWhatsAppFailureDto>>(response, ct);
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, ct);
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Falha ao desserializar resposta da plataforma.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        string? errorMessage = null;

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("description", out var desc))
            {
                errorMessage = desc.GetString();
            }
            else if (doc.RootElement.TryGetProperty("error", out var err))
            {
                errorMessage = err.GetString();
            }
            else if (doc.RootElement.TryGetProperty("message", out var msg))
            {
                errorMessage = msg.GetString();
            }
        }
        catch
        {
            // fallback
        }

        throw new HttpRequestException(
            errorMessage ?? $"Erro na chamada de observabilidade ({response.StatusCode}): {content}",
            null,
            response.StatusCode);
    }
}
