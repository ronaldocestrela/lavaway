using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class PlatformWhatsAppApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<PlatformWhatsAppInstancesOverviewDto> GetOverviewAsync(
        string? searchTerm = null,
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

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            queryParams.Add($"searchTerm={Uri.EscapeDataString(searchTerm.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            queryParams.Add($"status={Uri.EscapeDataString(status.Trim())}");
        }

        var url = $"platform/whatsapp/instances?{string.Join("&", queryParams)}";
        using var response = await httpClient.GetAsync(url, ct);
        return await ReadResponseAsync<PlatformWhatsAppInstancesOverviewDto>(response, ct);
    }

    public async Task<TenantWhatsAppHealthDetailDto> GetInstanceHealthAsync(Guid tenantId, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"platform/whatsapp/instances/{tenantId}/health", ct);
        return await ReadResponseAsync<TenantWhatsAppHealthDetailDto>(response, ct);
    }

    public async Task<ProbeWhatsAppInstanceResultDto> RunProbeAsync(Guid tenantId, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsync($"platform/whatsapp/instances/{tenantId}/probe", null, ct);
        return await ReadResponseAsync<ProbeWhatsAppInstanceResultDto>(response, ct);
    }

    public async Task TriggerAlertAsync(Guid tenantId, string? customNote = null, CancellationToken ct = default)
    {
        var request = new TriggerWhatsAppAlertRequest(customNote);
        using var response = await httpClient.PostAsJsonAsync($"platform/whatsapp/instances/{tenantId}/alert", request, JsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<IReadOnlyList<WhatsAppConnectionIncidentDto>> ListIncidentsAsync(int count = 50, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"platform/whatsapp/incidents?count={count}", ct);
        return await ReadResponseAsync<List<WhatsAppConnectionIncidentDto>>(response, ct);
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
            errorMessage ?? $"Erro na chamada da plataforma WhatsApp ({response.StatusCode}): {content}",
            null,
            response.StatusCode);
    }
}
