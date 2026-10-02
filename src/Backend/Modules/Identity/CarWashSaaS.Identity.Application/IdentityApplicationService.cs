using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Identity.Application;

public sealed class IdentityApplicationService(
    IIdentityUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ITokenService tokenService)
{
    public async Task<Result<AuthTokenResponse>> AuthenticateAsync(
        LoginRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Result<AuthTokenResponse>.Failure(new Error("auth.email.required", "Email is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Result<AuthTokenResponse>.Failure(new Error("auth.password.required", "Password is required.", ErrorType.Validation));
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.FindByEmailAsync(normalizedEmail, ct);
        if (user is null)
        {
            return Result<AuthTokenResponse>.Failure(new Error("auth.invalid_credentials", "Invalid email or password.", ErrorType.Unauthorized));
        }

        if (request.TenantId.HasValue && request.TenantId.Value != Guid.Empty && user.TenantId != request.TenantId.Value)
        {
            return Result<AuthTokenResponse>.Failure(new Error("auth.invalid_credentials", "Invalid email or password.", ErrorType.Unauthorized));
        }

        var passwordValid = await userRepository.CheckPasswordAsync(user.Id, request.Password, ct);
        if (!passwordValid)
        {
            return Result<AuthTokenResponse>.Failure(new Error("auth.invalid_credentials", "Invalid email or password.", ErrorType.Unauthorized));
        }

        var permissions = ShopRolePermissions.GetPermissions(user.Role);
        var accessToken = tokenService.GenerateAccessToken(user.Id, user.Email, user.TenantId, user.Role, permissions);
        var refreshTokenString = tokenService.GenerateRefreshToken();
        var refreshTokenHash = tokenService.HashToken(refreshTokenString);

        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var refreshTokenEntityResult = RefreshToken.Create(user.TenantId, user.Id, refreshTokenHash, expiresAt);
        if (!refreshTokenEntityResult.IsSuccess)
        {
            return Result<AuthTokenResponse>.Failure(refreshTokenEntityResult.Error!);
        }

        await refreshTokenRepository.AddAsync(refreshTokenEntityResult.Value!, ct);

        return Result<AuthTokenResponse>.Success(new AuthTokenResponse(
            accessToken.Token,
            refreshTokenString,
            accessToken.ExpiresInSeconds,
            "Bearer",
            user.Id,
            user.Email,
            user.TenantId,
            user.Role.ToString(),
            permissions.Select(p => p.ToString()).ToArray()));
    }

    public async Task<Result<AuthTokenResponse>> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result<AuthTokenResponse>.Failure(new Error("auth.refresh_token.required", "Refresh token is required.", ErrorType.Validation));
        }

        var tokenHash = tokenService.HashToken(request.RefreshToken);
        var existingToken = await refreshTokenRepository.GetByHashAsync(tokenHash, ct);
        if (existingToken is null)
        {
            return Result<AuthTokenResponse>.Failure(new Error("auth.refresh_token.invalid", "Invalid refresh token.", ErrorType.Unauthorized));
        }

        var now = DateTimeOffset.UtcNow;
        if (existingToken.IsRevoked)
        {
            // Detecção de reutilização de token revogado: revoga todas as sessões do usuário por segurança
            await refreshTokenRepository.RevokeAllForUserAsync(existingToken.UserId, now, ct);
            return Result<AuthTokenResponse>.Failure(new Error("auth.refresh_token.revoked", "Refresh token has been revoked.", ErrorType.Unauthorized));
        }

        if (existingToken.IsExpired(now))
        {
            return Result<AuthTokenResponse>.Failure(new Error("auth.refresh_token.expired", "Refresh token has expired.", ErrorType.Unauthorized));
        }

        var user = await userRepository.FindByIdAsync(existingToken.UserId, ct);
        if (user is null)
        {
            return Result<AuthTokenResponse>.Failure(new Error("auth.user.not_found", "User associated with token was not found.", ErrorType.Unauthorized));
        }

        var newRefreshTokenString = tokenService.GenerateRefreshToken();
        var newRefreshTokenHash = tokenService.HashToken(newRefreshTokenString);

        var revokeResult = existingToken.Revoke(now, newRefreshTokenHash);
        if (!revokeResult.IsSuccess)
        {
            return Result<AuthTokenResponse>.Failure(revokeResult.Error!);
        }

        await refreshTokenRepository.UpdateAsync(existingToken, ct);

        var newExpiresAt = now.AddDays(7);
        var newTokenEntityResult = RefreshToken.Create(user.TenantId, user.Id, newRefreshTokenHash, newExpiresAt, now);
        if (!newTokenEntityResult.IsSuccess)
        {
            return Result<AuthTokenResponse>.Failure(newTokenEntityResult.Error!);
        }

        await refreshTokenRepository.AddAsync(newTokenEntityResult.Value!, ct);

        var permissions = ShopRolePermissions.GetPermissions(user.Role);
        var accessToken = tokenService.GenerateAccessToken(user.Id, user.Email, user.TenantId, user.Role, permissions);

        return Result<AuthTokenResponse>.Success(new AuthTokenResponse(
            accessToken.Token,
            newRefreshTokenString,
            accessToken.ExpiresInSeconds,
            "Bearer",
            user.Id,
            user.Email,
            user.TenantId,
            user.Role.ToString(),
            permissions.Select(p => p.ToString()).ToArray()));
    }

    public async Task<Result> RevokeTokenAsync(
        RevokeTokenRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result.Failure(new Error("auth.refresh_token.required", "Refresh token is required.", ErrorType.Validation));
        }

        var tokenHash = tokenService.HashToken(request.RefreshToken);
        var existingToken = await refreshTokenRepository.GetByHashAsync(tokenHash, ct);
        if (existingToken is null || existingToken.IsRevoked)
        {
            return Result.Success();
        }

        existingToken.Revoke(DateTimeOffset.UtcNow);
        await refreshTokenRepository.UpdateAsync(existingToken, ct);

        return Result.Success();
    }

    public async Task<Result<UserSummaryDto>> CreateUserAsync(
        Guid tenantId,
        CreateUserRequest request,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<UserSummaryDto>.Failure(new Error("user.tenant_id.required", "TenantId is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Result<UserSummaryDto>.Failure(new Error("user.email.required", "Email is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return Result<UserSummaryDto>.Failure(new Error("user.password.invalid", "Password must have at least 6 characters.", ErrorType.Validation));
        }

        if (!Enum.TryParse<ShopRole>(request.Role, true, out var role))
        {
            return Result<UserSummaryDto>.Failure(new Error("user.role.invalid", $"Role '{request.Role}' is invalid.", ErrorType.Validation));
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var createResult = await userRepository.CreateUserAsync(tenantId, normalizedEmail, request.Password, role, ct);
        if (!createResult.IsSuccess)
        {
            return Result<UserSummaryDto>.Failure(createResult.Error!);
        }

        var createdUser = createResult.Value!;
        var permissions = ShopRolePermissions.GetPermissions(createdUser.Role)
            .Select(p => p.ToString())
            .ToArray();

        return Result<UserSummaryDto>.Success(new UserSummaryDto(
            createdUser.Id,
            createdUser.TenantId,
            createdUser.Email,
            createdUser.Role.ToString(),
            permissions));
    }

    public async Task<Result<IReadOnlyCollection<UserSummaryDto>>> ListUsersAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyCollection<UserSummaryDto>>.Failure(new Error("user.tenant_id.required", "TenantId is required.", ErrorType.Validation));
        }

        var users = await userRepository.ListByTenantAsync(tenantId, ct);
        var dtos = users.Select(u => new UserSummaryDto(
            u.Id,
            u.TenantId,
            u.Email,
            u.Role.ToString(),
            ShopRolePermissions.GetPermissions(u.Role).Select(p => p.ToString()).ToArray()))
            .ToArray();

        return Result<IReadOnlyCollection<UserSummaryDto>>.Success(dtos);
    }
}
