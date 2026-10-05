using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class BillingApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<WorkOrderPixChargeDto> GetOrCreatePixChargeAsync(
        Guid workOrderId,
        int? expirationMinutes = null,
        CancellationToken ct = default)
    {
        var request = new GenerateWorkOrderPixChargeRequest(expirationMinutes);
        using var response = await httpClient.PostAsJsonAsync($"billing/work-orders/{workOrderId}/pix", request, ct);
        return await ReadResponseAsync<WorkOrderPixChargeDto>(response, ct);
    }

    public async Task<WorkOrderPixChargeDto?> GetPixChargeAsync(
        Guid workOrderId,
        CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"billing/work-orders/{workOrderId}/pix", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadResponseAsync<WorkOrderPixChargeDto>(response, ct);
    }

    public async Task<WorkOrderPixChargeDto> SendPixChargeToWhatsAppAsync(
        Guid workOrderId,
        string? customMessage = null,
        CancellationToken ct = default)
    {
        var request = new SendWorkOrderPixWhatsAppRequest(customMessage);
        using var response = await httpClient.PostAsJsonAsync($"billing/work-orders/{workOrderId}/pix/send-whatsapp", request, ct);
        return await ReadResponseAsync<WorkOrderPixChargeDto>(response, ct);
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

            throw new InvalidOperationException("API response content is empty.");
        }

        var errorBody = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(errorBody)
                ? $"Request failed with status code {response.StatusCode}."
                : errorBody,
            null,
            response.StatusCode);
    }
}
