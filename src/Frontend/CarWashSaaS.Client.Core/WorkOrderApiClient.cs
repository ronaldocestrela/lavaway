using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class WorkOrderApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<WorkOrderDto> CreateWorkOrderAsync(CreateWorkOrderRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("work-orders", request, ct);
        return await ReadResponseAsync<WorkOrderDto>(response, ct);
    }

    public async Task<WorkOrderDto?> GetWorkOrderByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"work-orders/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadResponseAsync<WorkOrderDto>(response, ct);
    }

    public async Task<IReadOnlyCollection<WorkOrderDto>> ListRecentWorkOrdersAsync(int limit = 20, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"work-orders?limit={limit}", ct);
        return await ReadResponseAsync<IReadOnlyCollection<WorkOrderDto>>(response, ct);
    }

    public async Task<YardKanbanBoardDto> GetKanbanBoardAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("yard/kanban", ct);
        return await ReadResponseAsync<YardKanbanBoardDto>(response, ct);
    }

    public async Task<WorkOrderDto> ChangeWorkOrderStatusAsync(Guid workOrderId, ChangeWorkOrderStatusRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PatchAsJsonAsync($"work-orders/{workOrderId}/status", request, ct);
        return await ReadResponseAsync<WorkOrderDto>(response, ct);
    }

    public async Task<WorkOrderDto> AssignOperatorAsync(Guid workOrderId, Guid? operatorId, CancellationToken ct = default)
    {
        using var response = await httpClient.PatchAsJsonAsync($"work-orders/{workOrderId}/operator", new AssignOperatorRequest(operatorId), ct);
        return await ReadResponseAsync<WorkOrderDto>(response, ct);
    }

    public async Task<IReadOnlyCollection<WorkOrderStatusHistoryDto>> GetStatusHistoryAsync(Guid workOrderId, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"work-orders/{workOrderId}/history", ct);
        return await ReadResponseAsync<IReadOnlyCollection<WorkOrderStatusHistoryDto>>(response, ct);
    }

    public async Task<WorkOrderComparisonGalleryDto> GetComparisonGalleryAsync(Guid workOrderId, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"work-orders/{workOrderId}/comparison-gallery", ct);
        return await ReadResponseAsync<WorkOrderComparisonGalleryDto>(response, ct);
    }

    public async Task<PostServicePhotoDto> UploadPostServicePhotoAsync(
        Guid workOrderId,
        Guid? workOrderItemId,
        Guid? beforeInspectionPhotoId,
        InspectionPhotoCategory category,
        string title,
        Stream fileStream,
        string fileName,
        string contentType,
        string? notes = null,
        CancellationToken ct = default)
    {
        using var multipart = new MultipartFormDataContent();
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);

        var query = $"category={category}&title={Uri.EscapeDataString(title)}";
        if (workOrderItemId.HasValue)
        {
            query += $"&workOrderItemId={workOrderItemId.Value}";
        }
        if (beforeInspectionPhotoId.HasValue)
        {
            query += $"&beforeInspectionPhotoId={beforeInspectionPhotoId.Value}";
        }
        if (!string.IsNullOrWhiteSpace(notes))
        {
            query += $"&notes={Uri.EscapeDataString(notes)}";
        }

        using var response = await httpClient.PostAsync($"work-orders/{workOrderId}/post-service-photos?{query}", multipart, ct);
        return await ReadResponseAsync<PostServicePhotoDto>(response, ct);
    }

    public async Task DeletePostServicePhotoAsync(Guid workOrderId, Guid photoId, CancellationToken ct = default)
    {
        using var response = await httpClient.DeleteAsync($"work-orders/{workOrderId}/post-service-photos/{photoId}", ct);
        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response, ct);
            throw new WorkOrderApiException(response.StatusCode, message);
        }
    }

    public string GetPostServicePhotoUrl(Guid workOrderId, Guid photoId) =>
        $"{httpClient.BaseAddress}work-orders/{workOrderId}/post-service-photos/{photoId}";

    public string GetInspectionPhotoUrl(Guid workOrderId, Guid photoId) =>
        $"{httpClient.BaseAddress}work-orders/{workOrderId}/inspection/photos/{photoId}";

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response, ct);
            throw new WorkOrderApiException(response.StatusCode, message);
        }

        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken: ct);
        if (value is null)
        {
            throw new WorkOrderApiException(response.StatusCode, "Resposta vazia recebida do serviço de ordens de serviço.");
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

public sealed class WorkOrderApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
