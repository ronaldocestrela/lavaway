using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class PlatformAuditApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<Result<PagedResult<AuditEventDto>>> SearchAuditEventsAsync(
        AuditQueryFilter filter,
        CancellationToken ct = default)
    {
        try
        {
            var queryParams = new List<string>
            {
                $"page={filter.Page}",
                $"pageSize={filter.PageSize}"
            };

            if (filter.FromUtc.HasValue) queryParams.Add($"fromUtc={Uri.EscapeDataString(filter.FromUtc.Value.ToString("O"))}");
            if (filter.ToUtc.HasValue) queryParams.Add($"toUtc={Uri.EscapeDataString(filter.ToUtc.Value.ToString("O"))}");
            if (filter.ActorId.HasValue && filter.ActorId.Value != Guid.Empty) queryParams.Add($"actorId={filter.ActorId.Value}");
            if (!string.IsNullOrWhiteSpace(filter.ActorEmail)) queryParams.Add($"actorEmail={Uri.EscapeDataString(filter.ActorEmail)}");
            if (!string.IsNullOrWhiteSpace(filter.Action)) queryParams.Add($"action={Uri.EscapeDataString(filter.Action)}");
            if (!string.IsNullOrWhiteSpace(filter.TargetType)) queryParams.Add($"targetType={Uri.EscapeDataString(filter.TargetType)}");
            if (!string.IsNullOrWhiteSpace(filter.TargetId)) queryParams.Add($"targetId={Uri.EscapeDataString(filter.TargetId)}");
            if (filter.TenantId.HasValue && filter.TenantId.Value != Guid.Empty) queryParams.Add($"tenantId={filter.TenantId.Value}");
            if (!string.IsNullOrWhiteSpace(filter.Outcome)) queryParams.Add($"outcome={Uri.EscapeDataString(filter.Outcome)}");
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm)) queryParams.Add($"searchTerm={Uri.EscapeDataString(filter.SearchTerm)}");

            var url = $"platform/audit?{string.Join("&", queryParams)}";
            using var response = await httpClient.GetAsync(url, ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<PagedResult<AuditEventDto>>(JsonOptions, ct);
                return data is not null
                    ? Result<PagedResult<AuditEventDto>>.Success(data)
                    : Result<PagedResult<AuditEventDto>>.Failure(new Error("audit.empty_response", "Empty response from server.", ErrorType.Validation));
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<PagedResult<AuditEventDto>>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<AuditEventDto>>.Failure(new Error("audit.network_error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<AuditEventDto>> GetAuditEventByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.GetAsync($"platform/audit/{id}", ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<AuditEventDto>(JsonOptions, ct);
                return data is not null
                    ? Result<AuditEventDto>.Success(data)
                    : Result<AuditEventDto>.Failure(new Error("audit.empty_response", "Empty response from server.", ErrorType.Validation));
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<AuditEventDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<AuditEventDto>.Failure(new Error("audit.network_error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<AuthTokenResponse>> PlatformLoginAsync(
        PlatformLoginRequest request,
        CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("platform/auth/login", request, ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions, ct);
                return data is not null
                    ? Result<AuthTokenResponse>.Success(data)
                    : Result<AuthTokenResponse>.Failure(new Error("auth.empty_response", "Empty response from server.", ErrorType.Validation));
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<AuthTokenResponse>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<AuthTokenResponse>.Failure(new Error("auth.network_error", ex.Message, ErrorType.Validation));
        }
    }

    private static async Task<Error> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body))
            {
                return new Error($"http.{(int)response.StatusCode}", response.ReasonPhrase ?? "Request failed.", ErrorType.Validation);
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var code = root.TryGetProperty("code", out var codeEl) ? codeEl.GetString() : $"http.{(int)response.StatusCode}";
            var desc = root.TryGetProperty("description", out var descEl) ? descEl.GetString() : body;

            return new Error(code ?? "error", desc ?? "Unknown error", ErrorType.Validation);
        }
        catch
        {
            return new Error($"http.{(int)response.StatusCode}", response.ReasonPhrase ?? "Request failed.", ErrorType.Validation);
        }
    }
}
