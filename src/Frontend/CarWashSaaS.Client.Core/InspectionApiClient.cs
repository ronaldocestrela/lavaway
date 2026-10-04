using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class InspectionApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<VehicleInspectionDto?> GetInspectionAsync(Guid workOrderId, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"work-orders/{workOrderId}/inspection", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadResponseAsync<VehicleInspectionDto>(response, ct);
    }

    public async Task<VehicleInspectionDto> CreateOrGetInspectionAsync(Guid workOrderId, CreateInspectionRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"work-orders/{workOrderId}/inspection", request, ct);
        return await ReadResponseAsync<VehicleInspectionDto>(response, ct);
    }

    public async Task<VehicleInspectionDto> UpdateChecklistAsync(Guid workOrderId, UpdateInspectionChecklistRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"work-orders/{workOrderId}/inspection/checklist", request, ct);
        return await ReadResponseAsync<VehicleInspectionDto>(response, ct);
    }

    public async Task<InspectionDamageDto> AddDamageAsync(Guid workOrderId, AddInspectionDamageRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"work-orders/{workOrderId}/inspection/damages", request, ct);
        return await ReadResponseAsync<InspectionDamageDto>(response, ct);
    }

    public async Task RemoveDamageAsync(Guid workOrderId, Guid damageId, CancellationToken ct = default)
    {
        using var response = await httpClient.DeleteAsync($"work-orders/{workOrderId}/inspection/damages/{damageId}", ct);
        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response, ct);
            throw new InspectionApiException(response.StatusCode, message);
        }
    }

    public async Task<InspectionPhotoDto> UploadPhotoAsync(
        Guid workOrderId,
        InspectionPhotoCategory category,
        Stream content,
        string fileName,
        string contentType,
        Guid? damageId = null,
        CancellationToken ct = default)
    {
        using var multipart = new MultipartFormDataContent();
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(streamContent, "file", fileName);

        var query = $"category={category}";
        if (damageId.HasValue)
        {
            query += $"&damageId={damageId.Value}";
        }

        using var response = await httpClient.PostAsync($"work-orders/{workOrderId}/inspection/photos?{query}", multipart, ct);
        return await ReadResponseAsync<InspectionPhotoDto>(response, ct);
    }

    public string GetPhotoUrl(Guid workOrderId, Guid photoId) =>
        $"{httpClient.BaseAddress}work-orders/{workOrderId}/inspection/photos/{photoId}";

    public async Task<VehicleInspectionDto> CompleteInspectionAsync(Guid workOrderId, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsync($"work-orders/{workOrderId}/inspection/complete", null, ct);
        return await ReadResponseAsync<VehicleInspectionDto>(response, ct);
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response, ct);
            throw new InspectionApiException(response.StatusCode, message);
        }

        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken: ct);
        if (value is null)
        {
            throw new InspectionApiException(response.StatusCode, "Resposta vazia recebida do serviço de vistoria.");
        }

        return value;
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return $"Operação falhou com status {(int)response.StatusCode}.";
            }

            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("description", out var description))
                {
                    return description.GetString() ?? raw;
                }

                if (doc.RootElement.TryGetProperty("detail", out var detail))
                {
                    return detail.GetString() ?? raw;
                }

                if (doc.RootElement.TryGetProperty("title", out var title))
                {
                    return title.GetString() ?? raw;
                }
            }

            return raw;
        }
        catch
        {
            return $"Operação falhou com status {(int)response.StatusCode}.";
        }
    }
}

public sealed class InspectionApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
