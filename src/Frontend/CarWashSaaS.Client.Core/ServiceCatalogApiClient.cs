using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class ServiceCatalogApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyCollection<ServiceDto>> GetServicesAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("services", ct);
        return await ReadResponseAsync<IReadOnlyCollection<ServiceDto>>(response, ct);
    }

    public async Task<ServiceDto?> GetServiceByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"services/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadResponseAsync<ServiceDto>(response, ct);
    }

    public async Task<ServiceDto> CreateServiceAsync(CreateServiceRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("services", request, ct);
        return await ReadResponseAsync<ServiceDto>(response, ct);
    }

    public async Task<ServiceDto> UpdateServiceAsync(Guid id, UpdateServiceRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"services/{id}", request, ct);
        return await ReadResponseAsync<ServiceDto>(response, ct);
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response, ct);
            throw new ServiceCatalogApiException(response.StatusCode, message);
        }

        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken: ct);
        if (value is null)
        {
            throw new ServiceCatalogApiException(response.StatusCode, "Resposta vazia recebida do catálogo de serviços.");
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

public sealed class ServiceCatalogApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
