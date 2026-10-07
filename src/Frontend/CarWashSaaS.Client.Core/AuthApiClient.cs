using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class AuthApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<Result<AuthTokenResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("auth/login", request, ct);
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

    public async Task<Result<AuthTokenResponse>> RegisterTenantAsync(RegisterTenantRequest request, CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("auth/register-tenant", request, ct);
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

    public async Task<Result<AuthTokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("auth/refresh", request, ct);
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

    public async Task<Result> RevokeTokenAsync(RevokeTokenRequest request, CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("auth/revoke", request, ct);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            var error = await ReadErrorAsync(response, ct);
            return Result.Failure(error);
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("auth.network_error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<UserSummaryDto>> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("identity/users", request, ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<UserSummaryDto>(JsonOptions, ct);
                return data is not null
                    ? Result<UserSummaryDto>.Success(data)
                    : Result<UserSummaryDto>.Failure(new Error("user.empty_response", "Empty response from server.", ErrorType.Validation));
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<UserSummaryDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<UserSummaryDto>.Failure(new Error("user.network_error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<IReadOnlyCollection<UserSummaryDto>>> ListUsersAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.GetAsync("identity/users", ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<UserSummaryDto>>(JsonOptions, ct);
                return data is not null
                    ? Result<IReadOnlyCollection<UserSummaryDto>>.Success(data)
                    : Result<IReadOnlyCollection<UserSummaryDto>>.Success([]);
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<IReadOnlyCollection<UserSummaryDto>>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyCollection<UserSummaryDto>>.Failure(new Error("user.network_error", ex.Message, ErrorType.Validation));
        }
    }

    private static async Task<Error> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var c) ? c.GetString() ?? "error" : "error";
            var description = root.TryGetProperty("description", out var d) ? d.GetString() ?? response.ReasonPhrase ?? "Unknown error" : response.ReasonPhrase ?? "Unknown error";

            var errorType = response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => ErrorType.Unauthorized,
                System.Net.HttpStatusCode.Conflict => ErrorType.Conflict,
                System.Net.HttpStatusCode.NotFound => ErrorType.NotFound,
                _ => ErrorType.Validation
            };

            return new Error(code, description, errorType);
        }
        catch
        {
            return new Error("http.error", response.ReasonPhrase ?? $"HTTP {(int)response.StatusCode}", ErrorType.Validation);
        }
    }
}
