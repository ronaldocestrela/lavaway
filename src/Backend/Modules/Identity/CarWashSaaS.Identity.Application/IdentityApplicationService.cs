using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Identity.Application;

public sealed class IdentityApplicationService(
    IIdentityUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ITokenService tokenService,
    IPlatformUserRepository? platformUserRepository = null,
    AuditTrailApplicationService? auditTrailService = null,
    ICustomRoleRepository? customRoleRepository = null)
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
            // Verificar se é um operador da plataforma tentando logar
            if (platformUserRepository is not null && (!request.TenantId.HasValue || request.TenantId.Value == Guid.Empty))
            {
                var platformUser = await platformUserRepository.FindByEmailAsync(normalizedEmail, ct);
                if (platformUser is not null && platformUser.IsActive)
                {
                    var isPlatformPasswordValid = platformUserRepository.VerifyPassword(platformUser, request.Password);
                    if (isPlatformPasswordValid)
                    {
                        platformUser.RecordLogin(DateTimeOffset.UtcNow);
                        platformUserRepository.Update(platformUser);

                        var platformPermissions = PlatformRolePermissions.GetPermissions(platformUser.Role);
                        var platformToken = tokenService.GeneratePlatformAccessToken(
                            platformUser.Id,
                            platformUser.Email,
                            platformUser.FullName,
                            platformUser.Role,
                            platformPermissions);

                        var platformRefreshToken = tokenService.GenerateRefreshToken();

                        if (auditTrailService is not null)
                        {
                            await auditTrailService.RecordEventAsync(new RecordAuditEventRequest(
                                ActorId: platformUser.Id,
                                ActorEmail: platformUser.Email,
                                ActorRole: platformUser.Role.ToString(),
                                ActorRealm: "Platform",
                                Action: PlatformActionConstants.AuthLoginSuccess,
                                TargetType: PlatformTargetTypeConstants.PlatformUser,
                                TargetId: platformUser.Id.ToString(),
                                Outcome: "Success"), ct);
                        }

                        return Result<AuthTokenResponse>.Success(new AuthTokenResponse(
                            platformToken.Token,
                            platformRefreshToken,
                            platformToken.ExpiresInSeconds,
                            "Bearer",
                            platformUser.Id,
                            platformUser.Email,
                            null,
                            platformUser.Role.ToString(),
                            platformPermissions.Select(p => p.ToString()).ToArray()));
                    }
                    else if (auditTrailService is not null)
                    {
                        await auditTrailService.RecordEventAsync(new RecordAuditEventRequest(
                            ActorId: platformUser.Id,
                            ActorEmail: platformUser.Email,
                            ActorRole: platformUser.Role.ToString(),
                            ActorRealm: "Platform",
                            Action: PlatformActionConstants.AuthLoginFailed,
                            TargetType: PlatformTargetTypeConstants.PlatformUser,
                            TargetId: platformUser.Id.ToString(),
                            Outcome: "Failure",
                            ErrorMessage: "Invalid password"), ct);
                    }
                }
            }

            return Result<AuthTokenResponse>.Failure(new Error("auth.invalid_credentials", "Invalid email or password.", ErrorType.Unauthorized));
        }

        if (request.TenantId.HasValue && request.TenantId.Value != Guid.Empty && user.TenantId != request.TenantId.Value)
        {
            return Result<AuthTokenResponse>.Failure(new Error("auth.invalid_credentials", "Invalid email or password.", ErrorType.Unauthorized));
        }

        if (!user.IsActive)
        {
            return Result<AuthTokenResponse>.Failure(new Error("auth.user.inactive", "Este usuário está desativado. Entre em contato com o administrador.", ErrorType.Unauthorized));
        }

        var passwordValid = await userRepository.CheckPasswordAsync(user.Id, request.Password, ct);
        if (!passwordValid)
        {
            return Result<AuthTokenResponse>.Failure(new Error("auth.invalid_credentials", "Invalid email or password.", ErrorType.Unauthorized));
        }

        var (permissions, roleName) = await ResolvePermissionsAndRoleAsync(user, ct);
        var accessToken = tokenService.GenerateAccessToken(user.Id, user.Email, user.TenantId, roleName, permissions);
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
            roleName,
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

        var (permissions, roleName) = await ResolvePermissionsAndRoleAsync(user, ct);
        var accessToken = tokenService.GenerateAccessToken(user.Id, user.Email, user.TenantId, roleName, permissions);

        return Result<AuthTokenResponse>.Success(new AuthTokenResponse(
            accessToken.Token,
            newRefreshTokenString,
            accessToken.ExpiresInSeconds,
            "Bearer",
            user.Id,
            user.Email,
            user.TenantId,
            roleName,
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

        var role = Enum.TryParse<ShopRole>(request.Role, true, out var parsedRole)
            ? parsedRole
            : ShopRole.Operator;

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var createResult = await userRepository.CreateUserAsync(tenantId, normalizedEmail, request.Password, role, request.CustomRoleId, ct);
        if (!createResult.IsSuccess)
        {
            return Result<UserSummaryDto>.Failure(createResult.Error!);
        }

        var createdUser = createResult.Value!;
        var (permissions, roleName) = await ResolvePermissionsAndRoleAsync(createdUser, ct);

        return Result<UserSummaryDto>.Success(new UserSummaryDto(
            createdUser.Id,
            createdUser.TenantId,
            createdUser.Email,
            roleName,
            permissions.Select(p => p.ToString()).ToArray()));
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
        var dtos = new List<UserSummaryDto>(users.Count);
        foreach (var u in users)
        {
            var (permissions, roleName) = await ResolvePermissionsAndRoleAsync(u, ct);
            dtos.Add(new UserSummaryDto(
                u.Id,
                u.TenantId,
                u.Email,
                roleName,
                permissions.Select(p => p.ToString()).ToArray()));
        }

        return Result<IReadOnlyCollection<UserSummaryDto>>.Success(dtos);
    }

    private async Task<(IReadOnlyCollection<ShopPermission> Permissions, string RoleName)> ResolvePermissionsAndRoleAsync(
        IdentityUserSnapshot user,
        CancellationToken ct)
    {
        if (customRoleRepository is not null && user.CustomRoleId.HasValue)
        {
            var customRole = await customRoleRepository.GetByIdAsync(user.TenantId, user.CustomRoleId.Value, ct);
            if (customRole is not null)
            {
                return (customRole.Permissions, customRole.Name);
            }
        }

        return (ShopRolePermissions.GetPermissions(user.Role), user.Role.ToString());
    }
}
