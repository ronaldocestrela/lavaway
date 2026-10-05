using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class SubscriptionApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<SubscriptionPlanDto>> ListPlansAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"subscriptions/plans?activeOnly={activeOnly}", ct);
        return await ReadResponseAsync<IReadOnlyList<SubscriptionPlanDto>>(response, ct);
    }

    public async Task<SubscriptionPlanDto> GetPlanByIdAsync(Guid planId, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"subscriptions/plans/{planId}", ct);
        return await ReadResponseAsync<SubscriptionPlanDto>(response, ct);
    }

    public async Task<SubscriptionPlanDto> CreatePlanAsync(CreateSubscriptionPlanRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("subscriptions/plans", request, ct);
        return await ReadResponseAsync<SubscriptionPlanDto>(response, ct);
    }

    public async Task<SubscriptionPlanDto> UpdatePlanAsync(Guid planId, UpdateSubscriptionPlanRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"subscriptions/plans/{planId}", request, ct);
        return await ReadResponseAsync<SubscriptionPlanDto>(response, ct);
    }

    public async Task<IReadOnlyList<CustomerSubscriptionDto>> ListSubscriptionsAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("subscriptions", ct);
        return await ReadResponseAsync<IReadOnlyList<CustomerSubscriptionDto>>(response, ct);
    }

    public async Task<CustomerSubscriptionDto> GetSubscriptionByIdAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"subscriptions/{subscriptionId}", ct);
        return await ReadResponseAsync<CustomerSubscriptionDto>(response, ct);
    }

    public async Task<CustomerSubscriptionDto> SubscribeCustomerAsync(SubscribeCustomerRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("subscriptions", request, ct);
        return await ReadResponseAsync<CustomerSubscriptionDto>(response, ct);
    }

    public async Task<CustomerSubscriptionDto> AddPlateAsync(Guid subscriptionId, string plate, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"subscriptions/{subscriptionId}/plates", new AddSubscriptionPlateRequest(plate), ct);
        return await ReadResponseAsync<CustomerSubscriptionDto>(response, ct);
    }

    public async Task<CustomerSubscriptionDto> RemovePlateAsync(Guid subscriptionId, string plate, CancellationToken ct = default)
    {
        using var response = await httpClient.DeleteAsync($"subscriptions/{subscriptionId}/plates/{Uri.EscapeDataString(plate)}", ct);
        return await ReadResponseAsync<CustomerSubscriptionDto>(response, ct);
    }

    public async Task<CustomerSubscriptionDto> CancelSubscriptionAsync(Guid subscriptionId, string? reason, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"subscriptions/{subscriptionId}/cancel", reason, ct);
        return await ReadResponseAsync<CustomerSubscriptionDto>(response, ct);
    }

    public async Task<IReadOnlyList<SubscriptionUsageDto>> ListUsagesAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"subscriptions/{subscriptionId}/usages", ct);
        return await ReadResponseAsync<IReadOnlyList<SubscriptionUsageDto>>(response, ct);
    }

    public async Task<SubscriptionDashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("subscriptions/dashboard", ct);
        return await ReadResponseAsync<SubscriptionDashboardSummaryDto>(response, ct);
    }

    public async Task<SubscriptionPlateSummaryDto?> LookupByPlateAsync(string plate, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"subscriptions/lookup/by-plate?plate={Uri.EscapeDataString(plate)}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadResponseAsync<SubscriptionPlateSummaryDto?>(response, ct);
    }

    public async Task<SubscriptionUsageReceiptDto> ConsumeCreditAsync(ConsumeSubscriptionCreditRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("subscriptions/consume", request, ct);
        return await ReadResponseAsync<SubscriptionUsageReceiptDto>(response, ct);
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            try
            {
                var errorDoc = JsonDocument.Parse(content);
                if (errorDoc.RootElement.TryGetProperty("description", out var desc))
                {
                    throw new InvalidOperationException(desc.GetString());
                }
                if (errorDoc.RootElement.TryGetProperty("error", out var err) &&
                    err.TryGetProperty("description", out var errDesc))
                {
                    throw new InvalidOperationException(errDesc.GetString());
                }
            }
            catch when (content.Length > 0)
            {
                throw new InvalidOperationException(content);
            }

            throw new InvalidOperationException($"Erro na requisição ({response.StatusCode}).");
        }

        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return result!;
    }
}
