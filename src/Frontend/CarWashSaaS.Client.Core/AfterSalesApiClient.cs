using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class AfterSalesApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<AfterSalesMetricsDto> GetMetricsAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("after-sales/metrics", ct);
        return await ReadResponseAsync<AfterSalesMetricsDto>(response, ct);
    }

    public async Task<IReadOnlyList<SatisfactionSurveyDto>> ListSurveysAsync(int limit = 50, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"after-sales/surveys?limit={limit}", ct);
        return await ReadResponseAsync<IReadOnlyList<SatisfactionSurveyDto>>(response, ct);
    }

    public async Task<IReadOnlyList<ReactivationCampaignRuleDto>> ListCampaignRulesAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("after-sales/campaigns/rules", ct);
        return await ReadResponseAsync<IReadOnlyList<ReactivationCampaignRuleDto>>(response, ct);
    }

    public async Task<ReactivationCampaignRuleDto> UpdateCampaignRuleAsync(Guid id, UpdateReactivationCampaignRuleRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"after-sales/campaigns/rules/{id}", request, ct);
        return await ReadResponseAsync<ReactivationCampaignRuleDto>(response, ct);
    }

    public async Task<IReadOnlyList<InactiveCustomerSummaryDto>> GetCampaignAudienceAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"after-sales/campaigns/rules/{id}/audience", ct);
        return await ReadResponseAsync<IReadOnlyList<InactiveCustomerSummaryDto>>(response, ct);
    }

    public async Task<DispatchCampaignResultDto> DispatchCampaignRuleAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsync($"after-sales/campaigns/rules/{id}/dispatch", null, ct);
        return await ReadResponseAsync<DispatchCampaignResultDto>(response, ct);
    }

    public async Task<IReadOnlyList<CustomerCommunicationPreferenceDto>> ListPreferencesAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("after-sales/preferences", ct);
        return await ReadResponseAsync<IReadOnlyList<CustomerCommunicationPreferenceDto>>(response, ct);
    }

    public async Task<CustomerCommunicationPreferenceDto> TogglePreferenceAsync(UpdateCustomerPreferenceRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("after-sales/preferences/toggle", request, ct);
        return await ReadResponseAsync<CustomerCommunicationPreferenceDto>(response, ct);
    }

    public async Task<WorkOrderDto> RegisterPickupAsync(Guid workOrderId, RegisterWorkOrderPickupRequest? request = null, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"yard/work-orders/{workOrderId}/pickup", request ?? new RegisterWorkOrderPickupRequest(null, null), ct);
        return await ReadResponseAsync<WorkOrderDto>(response, ct);
    }

    public async Task DispatchSurveyManuallyAsync(Guid workOrderId, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsync($"yard/work-orders/{workOrderId}/survey/send", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(body)
                ? $"Falha ao disparar pesquisa: {(int)response.StatusCode}"
                : body);
        }
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
            return content ?? throw new InvalidOperationException("Resposta nula da API.");
        }

        var errorBody = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorBody)
            ? $"A requisição falhou com status {(int)response.StatusCode} ({response.ReasonPhrase})."
            : errorBody);
    }
}
