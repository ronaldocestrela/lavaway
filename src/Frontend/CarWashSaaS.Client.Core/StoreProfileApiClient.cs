using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class StoreProfileApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<StoreProfileDto?> GetProfileAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("tenants/profile", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadResponseAsync<StoreProfileDto>(response, ct);
    }

    public async Task<StoreProfileDto> CreateProfileAsync(CreateStoreProfileRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("tenants/profile", request, ct);
        return await ReadResponseAsync<StoreProfileDto>(response, ct);
    }

    public async Task<StoreProfileDto> UpdateProfileAsync(UpdateStoreProfileRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PutAsJsonAsync("tenants/profile", request, ct);
        return await ReadResponseAsync<StoreProfileDto>(response, ct);
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response, ct);
            throw new StoreProfileApiException(response.StatusCode, message);
        }

        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken: ct);
        return value ?? throw new StoreProfileApiException(HttpStatusCode.InternalServerError, "A API retornou uma resposta vazia.");
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                using var document = JsonDocument.Parse(content);
                foreach (var key in new[] { "Description", "description", "detail", "title", "error" })
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
            HttpStatusCode.Forbidden => "Sua sessão não possui permissão de administrador para alterar o perfil.",
            HttpStatusCode.NotFound => "O perfil do estabelecimento não foi encontrado.",
            HttpStatusCode.Conflict => "O perfil para este estabelecimento já foi cadastrado.",
            HttpStatusCode.BadRequest => "Os dados fornecidos para o perfil são inválidos.",
            _ => "Não foi possível concluir a operação de perfil. Tente novamente."
        };
    }
}

public sealed class StoreProfileApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
