using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Identity.Application;

public sealed class PlatformAuthApplicationService(
    IPlatformUserRepository platformUserRepository,
    ITokenService tokenService,
    AuditTrailApplicationService auditTrailService)
{
    public async Task<Result<AuthTokenResponse>> AuthenticateAsync(
        PlatformLoginRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Result<AuthTokenResponse>.Failure(new Error(
                "auth.email.required",
                "Email is required.",
                ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Result<AuthTokenResponse>.Failure(new Error(
                "auth.password.required",
                "Password is required.",
                ErrorType.Validation));
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await platformUserRepository.FindByEmailAsync(normalizedEmail, ct);

        if (user is null || !user.IsActive)
        {
            var actorId = user is not null && user.Id != Guid.Empty ? user.Id : Guid.Parse("00000000-0000-0000-0000-000000000001");
            await auditTrailService.RecordEventAsync(new RecordAuditEventRequest(
                ActorId: actorId,
                ActorEmail: normalizedEmail,

                ActorRole: "Anonymous",
                ActorRealm: "Platform",
                Action: PlatformActionConstants.AuthLoginFailed,
                TargetType: PlatformTargetTypeConstants.PlatformUser,
                TargetId: user?.Id.ToString() ?? normalizedEmail,
                IpAddress: ipAddress,
                UserAgent: userAgent,
                Outcome: "Failure",
                ErrorMessage: user is null ? "User not found" : "User is inactive"), ct);

            return Result<AuthTokenResponse>.Failure(new Error(
                "auth.invalid_credentials",
                "Invalid platform credentials.",
                ErrorType.Unauthorized));
        }

        var isPasswordValid = platformUserRepository.VerifyPassword(user, request.Password);
        if (!isPasswordValid)
        {
            await auditTrailService.RecordEventAsync(new RecordAuditEventRequest(
                ActorId: user.Id,
                ActorEmail: user.Email,
                ActorRole: user.Role.ToString(),
                ActorRealm: "Platform",
                Action: PlatformActionConstants.AuthLoginFailed,
                TargetType: PlatformTargetTypeConstants.PlatformUser,
                TargetId: user.Id.ToString(),
                IpAddress: ipAddress,
                UserAgent: userAgent,
                Outcome: "Failure",
                ErrorMessage: "Invalid password"), ct);

            return Result<AuthTokenResponse>.Failure(new Error(
                "auth.invalid_credentials",
                "Invalid platform credentials.",
                ErrorType.Unauthorized));
        }

        var now = DateTimeOffset.UtcNow;
        user.RecordLogin(now);
        platformUserRepository.Update(user);

        var permissions = PlatformRolePermissions.GetPermissions(user.Role);
        var token = tokenService.GeneratePlatformAccessToken(
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            permissions);

        var refreshToken = tokenService.GenerateRefreshToken();

        // Registrar login bem-sucedido na trilha de auditoria
        await auditTrailService.RecordEventAsync(new RecordAuditEventRequest(
            ActorId: user.Id,
            ActorEmail: user.Email,
            ActorRole: user.Role.ToString(),
            ActorRealm: "Platform",
            Action: PlatformActionConstants.AuthLoginSuccess,
            TargetType: PlatformTargetTypeConstants.PlatformUser,
            TargetId: user.Id.ToString(),
            IpAddress: ipAddress,
            UserAgent: userAgent,
            DetailsJson: $"{{\"fullName\":\"{user.FullName}\",\"role\":\"{user.Role}\"}}",
            Outcome: "Success"), ct);

        var response = new AuthTokenResponse(
            token.Token,
            refreshToken,
            token.ExpiresInSeconds,
            "Bearer",
            user.Id,
            user.Email,
            null,
            user.Role.ToString(),
            permissions.Select(p => p.ToString()).ToArray());

        return Result<AuthTokenResponse>.Success(response);
    }

    public async Task<Result<PlatformUserSummaryDto>> CreatePlatformUserAsync(
        CreatePlatformUserRequest request,
        Guid currentAdminId,
        string currentAdminEmail,
        string currentAdminRole,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Result<PlatformUserSummaryDto>.Failure(new Error(
                "platform_user.email.required",
                "Email is required.",
                ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Result<PlatformUserSummaryDto>.Failure(new Error(
                "platform_user.password.required",
                "Password is required.",
                ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return Result<PlatformUserSummaryDto>.Failure(new Error(
                "platform_user.full_name.required",
                "Full name is required.",
                ErrorType.Validation));
        }

        if (!Enum.TryParse<PlatformRole>(request.Role, true, out var role))
        {
            return Result<PlatformUserSummaryDto>.Failure(new Error(
                "platform_user.role.invalid",
                $"Invalid platform role '{request.Role}'.",
                ErrorType.Validation));
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existing = await platformUserRepository.FindByEmailAsync(normalizedEmail, ct);
        if (existing is not null)
        {
            return Result<PlatformUserSummaryDto>.Failure(new Error(
                "platform_user.email.duplicate",
                "A platform user with this email already exists.",
                ErrorType.Conflict));
        }

        var passwordHash = platformUserRepository.HashPassword(request.Password);
        var createResult = PlatformUser.Create(
            normalizedEmail,
            request.FullName,
            role,
            passwordHash);

        if (!createResult.IsSuccess)
        {
            return Result<PlatformUserSummaryDto>.Failure(createResult.Error!);
        }

        var newUser = createResult.Value!;
        await platformUserRepository.AddAsync(newUser, ct);

        // Registrar criação do usuário na trilha de auditoria
        await auditTrailService.RecordEventAsync(new RecordAuditEventRequest(
            ActorId: currentAdminId,
            ActorEmail: currentAdminEmail,
            ActorRole: currentAdminRole,
            ActorRealm: "Platform",
            Action: PlatformActionConstants.PlatformUserCreated,
            TargetType: PlatformTargetTypeConstants.PlatformUser,
            TargetId: newUser.Id.ToString(),
            DetailsJson: $"{{\"createdEmail\":\"{newUser.Email}\",\"fullName\":\"{newUser.FullName}\",\"role\":\"{newUser.Role}\"}}",
            Outcome: "Success"), ct);

        var dto = new PlatformUserSummaryDto(
            newUser.Id,
            newUser.Email,
            newUser.FullName,
            newUser.Role.ToString(),
            newUser.IsActive,
            newUser.CreatedAtUtc,
            newUser.LastLoginAtUtc);

        return Result<PlatformUserSummaryDto>.Success(dto);
    }

    public async Task<Result<IReadOnlyCollection<PlatformUserSummaryDto>>> ListPlatformUsersAsync(
        CancellationToken ct = default)
    {
        var users = await platformUserRepository.ListAllAsync(ct);
        var dtos = users.Select(u => new PlatformUserSummaryDto(
            u.Id,
            u.Email,
            u.FullName,
            u.Role.ToString(),
            u.IsActive,
            u.CreatedAtUtc,
            u.LastLoginAtUtc)).ToArray();

        return Result<IReadOnlyCollection<PlatformUserSummaryDto>>.Success(dtos);
    }
}
