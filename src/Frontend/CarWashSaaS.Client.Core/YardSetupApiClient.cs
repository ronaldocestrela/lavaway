using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class YardSetupApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<YardCapacityDto?> GetCapacityAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("yard/capacity", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadResponseAsync<YardCapacityDto>(response, ct);
    }

    public async Task<YardCapacityDto> CreateCapacityAsync(CreateYardCapacityRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("yard/capacity", request, ct);
        return await ReadResponseAsync<YardCapacityDto>(response, ct);
    }

    public async Task<YardCapacityDto> UpdateCapacityAsync(UpdateYardCapacityRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PutAsJsonAsync("yard/capacity", request, ct);
        return await ReadResponseAsync<YardCapacityDto>(response, ct);
    }

    public async Task<IReadOnlyCollection<TeamMemberDto>> GetTeamMembersAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("team-members", ct);
        return await ReadResponseAsync<IReadOnlyCollection<TeamMemberDto>>(response, ct);
    }

    public async Task<TeamMemberDto?> GetTeamMemberByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"team-members/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadResponseAsync<TeamMemberDto>(response, ct);
    }

    public async Task<TeamMemberDto> CreateTeamMemberAsync(CreateTeamMemberRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("team-members", request, ct);
        return await ReadResponseAsync<TeamMemberDto>(response, ct);
    }

    public async Task<TeamMemberDto> UpdateTeamMemberAsync(Guid id, UpdateTeamMemberRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"team-members/{id}", request, ct);
        return await ReadResponseAsync<TeamMemberDto>(response, ct);
    }

    public async Task<TeamMemberDto> ToggleTeamMemberStatusAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await httpClient.PatchAsync($"team-members/{id}/toggle-status", null, ct);
        return await ReadResponseAsync<TeamMemberDto>(response, ct);
    }

    public async Task<IReadOnlyCollection<CommissionRuleDto>> GetCommissionRulesAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("commission-rules", ct);
        return await ReadResponseAsync<IReadOnlyCollection<CommissionRuleDto>>(response, ct);
    }

    public async Task<CommissionRuleDto> CreateCommissionRuleAsync(CreateCommissionRuleRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("commission-rules", request, ct);
        return await ReadResponseAsync<CommissionRuleDto>(response, ct);
    }

    public async Task<CommissionRuleDto> UpdateCommissionRuleAsync(Guid id, UpdateCommissionRuleRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"commission-rules/{id}", request, ct);
        return await ReadResponseAsync<CommissionRuleDto>(response, ct);
    }

    public async Task DeleteCommissionRuleAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await httpClient.DeleteAsync($"commission-rules/{id}", ct);
        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response, ct);
            throw new YardSetupApiException(response.StatusCode, message);
        }
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response, ct);
            throw new YardSetupApiException(response.StatusCode, message);
        }

        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken: ct);
        if (value is null)
        {
            throw new YardSetupApiException(response.StatusCode, "Resposta vazia recebida da API de pátio e equipe.");
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

public sealed class YardSetupApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
