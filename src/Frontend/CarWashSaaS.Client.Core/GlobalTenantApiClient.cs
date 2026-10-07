using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class GlobalTenantApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<Result<PagedResult<GlobalTenantSummaryDto>>> GetTenantsAsync(
        GetGlobalTenantsRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var queryParams = new List<string>
            {
                $"page={request.Page}",
                $"pageSize={request.PageSize}"
            };

            if (request.Status.HasValue)
            {
                queryParams.Add($"status={request.Status.Value}");
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                queryParams.Add($"searchTerm={Uri.EscapeDataString(request.SearchTerm.Trim())}");
            }

            var url = $"platform/tenants?{string.Join("&", queryParams)}";
            using var response = await httpClient.GetAsync(url, ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<PagedResult<GlobalTenantSummaryDto>>(JsonOptions, ct);
                return data is not null
                    ? Result<PagedResult<GlobalTenantSummaryDto>>.Success(data)
                    : Result<PagedResult<GlobalTenantSummaryDto>>.Failure(new Error("tenants.empty_response", "Resposta vazia do servidor.", ErrorType.Validation));
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<PagedResult<GlobalTenantSummaryDto>>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<GlobalTenantSummaryDto>>.Failure(new Error("tenants.network_error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<GlobalTenantSummaryDto>> GetTenantByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.GetAsync($"platform/tenants/{id}", ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<GlobalTenantSummaryDto>(JsonOptions, ct);
                return data is not null
                    ? Result<GlobalTenantSummaryDto>.Success(data)
                    : Result<GlobalTenantSummaryDto>.Failure(new Error("tenants.empty_response", "Resposta vazia do servidor.", ErrorType.Validation));
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<GlobalTenantSummaryDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<GlobalTenantSummaryDto>.Failure(new Error("tenants.network_error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<GlobalTenantSummaryDto>> UpdateTenantStatusAsync(
        Guid id,
        UpdateTenantStatusRequest request,
        CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.PutAsJsonAsync($"platform/tenants/{id}/status", request, ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<GlobalTenantSummaryDto>(JsonOptions, ct);
                return data is not null
                    ? Result<GlobalTenantSummaryDto>.Success(data)
                    : Result<GlobalTenantSummaryDto>.Failure(new Error("tenants.empty_response", "Resposta vazia do servidor.", ErrorType.Validation));
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<GlobalTenantSummaryDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<GlobalTenantSummaryDto>.Failure(new Error("tenants.network_error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<ImpersonationSessionDto>> StartImpersonationAsync(
        Guid id,
        StartImpersonationRequest request,
        CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync($"platform/tenants/{id}/impersonate", request, ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<ImpersonationSessionDto>(JsonOptions, ct);
                return data is not null
                    ? Result<ImpersonationSessionDto>.Success(data)
                    : Result<ImpersonationSessionDto>.Failure(new Error("impersonation.empty_response", "Resposta vazia do servidor.", ErrorType.Validation));
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<ImpersonationSessionDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<ImpersonationSessionDto>.Failure(new Error("impersonation.network_error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result> EndImpersonationAsync(
        Guid id,
        EndImpersonationRequest request,
        CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync($"platform/tenants/{id}/end-impersonation", request, ct);

            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            var error = await ReadErrorAsync(response, ct);
            return Result.Failure(error);
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("impersonation.network_error", ex.Message, ErrorType.Validation));
        }
    }

    private static async Task<Error> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var errorType = response.StatusCode switch
        {
            System.Net.HttpStatusCode.NotFound => ErrorType.NotFound,
            System.Net.HttpStatusCode.Unauthorized => ErrorType.Unauthorized,
            System.Net.HttpStatusCode.Forbidden => ErrorType.Unauthorized,
            System.Net.HttpStatusCode.Conflict => ErrorType.Conflict,
            _ => ErrorType.Validation
        };

        try
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var code = root.TryGetProperty("code", out var codeElem)
                ? codeElem.GetString() ?? "api.error"
                : "api.error";

            var description = root.TryGetProperty("description", out var descElem)
                ? descElem.GetString() ?? response.ReasonPhrase ?? "Erro inesperado."
                : (root.TryGetProperty("title", out var titleElem) ? titleElem.GetString() ?? response.ReasonPhrase ?? "Erro inesperado." : "Erro inesperado.");

            return new Error(code, description, errorType);
        }
        catch
        {
            return new Error("api.http_error", $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}", errorType);
        }
    }
}
