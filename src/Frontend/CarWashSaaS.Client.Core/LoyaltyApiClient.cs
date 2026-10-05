using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class LoyaltyApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<LoyaltyProgramDto> GetProgramAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("loyalty/program", ct);
        return await ReadResponseAsync<LoyaltyProgramDto>(response, ct);
    }

    public async Task<LoyaltyProgramDto> UpdateProgramAsync(UpdateLoyaltyProgramRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PutAsJsonAsync("loyalty/program", request, ct);
        return await ReadResponseAsync<LoyaltyProgramDto>(response, ct);
    }

    public async Task<IReadOnlyList<CustomerLoyaltySummaryDto>> ListAccountsAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("loyalty/customers", ct);
        return await ReadResponseAsync<IReadOnlyList<CustomerLoyaltySummaryDto>>(response, ct);
    }

    public async Task<CustomerLoyaltySummaryDto?> GetCustomerSummaryAsync(Guid customerId, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"loyalty/customers/{customerId}", ct);
        return await ReadResponseAsync<CustomerLoyaltySummaryDto?>(response, ct);
    }

    public async Task<CustomerLoyaltySummaryDto?> GetSummaryByPhoneAsync(string phone, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"loyalty/customers/by-phone?phone={Uri.EscapeDataString(phone)}", ct);
        return await ReadResponseAsync<CustomerLoyaltySummaryDto?>(response, ct);
    }

    public async Task<IReadOnlyList<CustomerLoyaltyTransactionDto>> GetCustomerTransactionsAsync(Guid customerId, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"loyalty/customers/{customerId}/transactions", ct);
        return await ReadResponseAsync<IReadOnlyList<CustomerLoyaltyTransactionDto>>(response, ct);
    }

    public async Task<CustomerLoyaltyTransactionDto> RedeemRewardAsync(RedeemLoyaltyRewardRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("loyalty/customers/redeem", request, ct);
        return await ReadResponseAsync<CustomerLoyaltyTransactionDto>(response, ct);
    }

    public async Task<CustomerLoyaltyTransactionDto> AdjustBalanceAsync(ManualLoyaltyAdjustmentRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("loyalty/customers/adjust", request, ct);
        return await ReadResponseAsync<CustomerLoyaltyTransactionDto>(response, ct);
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
