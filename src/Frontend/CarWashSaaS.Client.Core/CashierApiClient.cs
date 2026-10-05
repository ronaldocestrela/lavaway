using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class CashierApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<CashTransactionDto> RegisterWorkOrderPaymentAsync(
        RegisterWorkOrderPaymentRequest request,
        CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("billing/cashier/payments/work-order", request, ct);
        return await ReadResponseAsync<CashTransactionDto>(response, ct);
    }

    public async Task<CashTransactionDto> RecordMovementAsync(
        CreateCashMovementRequest request,
        CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("billing/cashier/movements", request, ct);
        return await ReadResponseAsync<CashTransactionDto>(response, ct);
    }

    public async Task<DailyCashSummaryDto> GetDailySummaryAsync(
        DateOnly? date = null,
        CancellationToken ct = default)
    {
        var url = date.HasValue
            ? $"billing/cashier/summary?date={date.Value:yyyy-MM-dd}"
            : "billing/cashier/summary";

        using var response = await httpClient.GetAsync(url, ct);
        return await ReadResponseAsync<DailyCashSummaryDto>(response, ct);
    }

    public async Task<DailyCashClosingDto> CloseDailyCashAsync(
        CloseDailyCashRequest request,
        CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("billing/cashier/close", request, ct);
        return await ReadResponseAsync<DailyCashClosingDto>(response, ct);
    }

    public async Task<IReadOnlyList<DailyCashClosingDto>> ListRecentClosingsAsync(
        int count = 30,
        CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"billing/cashier/closings?count={count}", ct);
        return await ReadResponseAsync<IReadOnlyList<DailyCashClosingDto>>(response, ct);
    }

    public async Task<CommissionReportDto> GetCommissionReportAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        Guid? teamMemberId = null,
        CancellationToken ct = default)
    {
        var url = "yard/commissions/report";
        var queryParams = new List<string>();

        if (startDate.HasValue) queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
        if (endDate.HasValue) queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");
        if (teamMemberId.HasValue) queryParams.Add($"teamMemberId={teamMemberId.Value}");

        if (queryParams.Count > 0)
        {
            url = $"{url}?{string.Join("&", queryParams)}";
        }

        using var response = await httpClient.GetAsync(url, ct);
        return await ReadResponseAsync<CommissionReportDto>(response, ct);
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
            if (content is not null)
            {
                return content;
            }

            throw new InvalidOperationException("Conteúdo da resposta da API está vazio.");
        }

        var errorBody = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(errorBody)
                ? $"Falha na requisição com código {response.StatusCode}."
                : errorBody,
            null,
            response.StatusCode);
    }
}
