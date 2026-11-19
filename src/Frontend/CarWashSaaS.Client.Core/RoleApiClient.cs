using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class RoleApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<PermissionDefinitionDto>> GetPermissionsAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("identity/permissions", ct);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<PermissionDefinitionDto>>(JsonOptions, ct) ?? [];
    }

    public async Task<IReadOnlyList<CustomRoleDto>> GetRolesAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("identity/roles", ct);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<CustomRoleDto>>(JsonOptions, ct) ?? [];
    }

    public async Task<CustomRoleDto?> GetRoleByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"identity/roles/{id}", ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<CustomRoleDto>(JsonOptions, ct);
    }

    public async Task<Result<CustomRoleDto>> CreateRoleAsync(CreateCustomRoleRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("identity/roles", request, JsonOptions, ct);
        if (response.IsSuccessStatusCode)
        {
            var created = await response.Content.ReadFromJsonAsync<CustomRoleDto>(JsonOptions, ct);
            return Result<CustomRoleDto>.Success(created!);
        }

        var error = await ReadErrorAsync(response, ct);
        return Result<CustomRoleDto>.Failure(error);
    }

    public async Task<Result<CustomRoleDto>> UpdateRoleAsync(Guid id, UpdateCustomRoleRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"identity/roles/{id}", request, JsonOptions, ct);
        if (response.IsSuccessStatusCode)
        {
            var updated = await response.Content.ReadFromJsonAsync<CustomRoleDto>(JsonOptions, ct);
            return Result<CustomRoleDto>.Success(updated!);
        }

        var error = await ReadErrorAsync(response, ct);
        return Result<CustomRoleDto>.Failure(error);
    }

    public async Task<Result> DeleteRoleAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await httpClient.DeleteAsync($"identity/roles/{id}", ct);
        if (response.IsSuccessStatusCode)
        {
            return Result.Success();
        }

        var error = await ReadErrorAsync(response, ct);
        return Result.Failure(error);
    }

    private static async Task<Error> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions, ct);
            if (body is not null && !string.IsNullOrWhiteSpace(body.Description))
            {
                var type = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.NotFound => ErrorType.NotFound,
                    System.Net.HttpStatusCode.Conflict => ErrorType.Conflict,
                    System.Net.HttpStatusCode.Unauthorized => ErrorType.Unauthorized,
                    System.Net.HttpStatusCode.Forbidden => ErrorType.Unauthorized,
                    _ => ErrorType.Validation
                };
                return new Error(body.Code ?? "api_error", body.Description, type);
            }
        }
        catch
        {
            // fallback
        }

        return new Error("api_error", $"Falha na requisição (Status {(int)response.StatusCode}).", ErrorType.Validation);
    }

    private sealed record ApiErrorResponse(string? Code, string? Description);
}
