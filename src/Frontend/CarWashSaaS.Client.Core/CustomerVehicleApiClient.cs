using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class CustomerVehicleApiClient(HttpClient httpClient)
{
    public async Task<IReadOnlyCollection<CustomerVehicleMatchDto>> SearchAsync(
        string? plate,
        string? phone,
        int limit = 20,
        CancellationToken ct = default)
    {
        var query = new List<string> { $"limit={limit}" };
        if (!string.IsNullOrWhiteSpace(plate))
        {
            query.Add($"plate={Uri.EscapeDataString(plate)}");
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            query.Add($"phone={Uri.EscapeDataString(phone)}");
        }

        using var response = await httpClient.GetAsync($"customers/search?{string.Join('&', query)}", ct);
        return await ReadResponseAsync<IReadOnlyCollection<CustomerVehicleMatchDto>>(response, ct);
    }

    public async Task<CustomerVehicleMatchDto> GetAsync(Guid customerId, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"customers/{customerId}", ct);
        return await ReadResponseAsync<CustomerVehicleMatchDto>(response, ct);
    }

    public async Task<CustomerVehicleMatchDto> CreateAsync(CreateCustomerWithVehicleRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("customers", request, ct);
        return await ReadResponseAsync<CustomerVehicleMatchDto>(response, ct);
    }

    public async Task<CustomerVehicleMatchDto> AddVehicleAsync(
        Guid customerId,
        AddVehicleToCustomerRequest request,
        CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"customers/{customerId}/vehicles", request, ct);
        return await ReadResponseAsync<CustomerVehicleMatchDto>(response, ct);
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response, ct);
            throw new CustomerVehicleApiException(response.StatusCode, message);
        }

        var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
        return value ?? throw new CustomerVehicleApiException(HttpStatusCode.InternalServerError, "A API retornou uma resposta vazia.");
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                using var document = JsonDocument.Parse(content);
                foreach (var key in new[] { "Description", "detail", "title" })
                {
                    if (document.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                    {
                        return value.GetString()!;
                    }
                }
            }
            catch (JsonException)
            {
            }
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Forbidden => "Sua sessão não tem acesso a esta operação.",
            HttpStatusCode.NotFound => "O cadastro não foi encontrado.",
            HttpStatusCode.Conflict => "A placa já está cadastrada nesta unidade.",
            _ => "Não foi possível concluir a operação. Tente novamente."
        };
    }
}

public sealed class CustomerVehicleApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
