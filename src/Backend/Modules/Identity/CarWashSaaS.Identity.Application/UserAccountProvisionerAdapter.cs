using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Identity.Application;

public sealed class UserAccountProvisionerAdapter(
    IIdentityUserRepository userRepository,
    IRefreshTokenRepository? refreshTokenRepository = null) : IUserAccountProvisioner
{
    public async Task<Result<Guid>> ProvisionUserAsync(
        Guid tenantId,
        string email,
        string password,
        string shopRole,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<Guid>.Failure(new Error("user.tenant_id.required", "TenantId is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<Guid>.Failure(new Error("user.email.required", "Email is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            return Result<Guid>.Failure(new Error("user.password.invalid", "A senha deve conter pelo menos 6 caracteres.", ErrorType.Validation));
        }

        var parsedRole = Enum.TryParse<ShopRole>(shopRole, true, out var role)
            ? role
            : ShopRole.Operator;

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var createResult = await userRepository.CreateUserAsync(tenantId, normalizedEmail, password, parsedRole, ct);
        if (!createResult.IsSuccess)
        {
            return Result<Guid>.Failure(createResult.Error!);
        }

        return Result<Guid>.Success(createResult.Value!.Id);
    }

    public async Task<Result> UpdateUserAccessAsync(
        Guid tenantId,
        Guid userId,
        string email,
        string? newPassword,
        string? shopRole,
        bool isActive,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            return Result.Failure(new Error("user.invalid_parameters", "TenantId and UserId are required.", ErrorType.Validation));
        }

        ShopRole? parsedRole = null;
        if (!string.IsNullOrWhiteSpace(shopRole) && Enum.TryParse<ShopRole>(shopRole, true, out var role))
        {
            parsedRole = role;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var result = await userRepository.UpdateUserAsync(userId, normalizedEmail, newPassword, parsedRole, isActive, ct);
        if (!result.IsSuccess)
        {
            return result;
        }

        if (!isActive && refreshTokenRepository is not null)
        {
            await refreshTokenRepository.RevokeAllForUserAsync(userId, DateTimeOffset.UtcNow, ct);
        }

        return Result.Success();
    }

    public async Task<Result> SetUserActiveStatusAsync(
        Guid tenantId,
        Guid userId,
        bool isActive,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            return Result.Failure(new Error("user.invalid_parameters", "TenantId and UserId are required.", ErrorType.Validation));
        }

        var result = await userRepository.SetActiveStatusAsync(userId, isActive, ct);
        if (!result.IsSuccess)
        {
            return result;
        }

        if (!isActive && refreshTokenRepository is not null)
        {
            await refreshTokenRepository.RevokeAllForUserAsync(userId, DateTimeOffset.UtcNow, ct);
        }

        return Result.Success();
    }
}
