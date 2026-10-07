using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Application;
using CarWashSaaS.Tenants.Domain;

namespace CarWashSaaS.Api.Services;

public sealed class TenantOnboardingService(
    ITenantRepository tenantRepository,
    IIdentityUserRepository userRepository,
    ITenantSaasSubscriptionRepository subscriptionRepository,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepository,
    ICurrentTenantAccessor? currentTenantAccessor = null)
{
    public async Task<Result<AuthTokenResponse>> RegisterAsync(
        RegisterTenantRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.StoreName))
        {
            return Result<AuthTokenResponse>.Failure(new Error("onboarding.store_name.required", "O nome do lava-jato ou estética é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Result<AuthTokenResponse>.Failure(new Error("onboarding.email.required", "O e-mail de acesso é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return Result<AuthTokenResponse>.Failure(new Error("onboarding.password.invalid", "A senha deve conter pelo menos 6 caracteres.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            return Result<AuthTokenResponse>.Failure(new Error("onboarding.phone.required", "O número de WhatsApp com DDD é obrigatório.", ErrorType.Validation));
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existingUser = await userRepository.FindByEmailAsync(normalizedEmail, ct);
        if (existingUser is not null)
        {
            return Result<AuthTokenResponse>.Failure(new Error("onboarding.email.duplicate", "Já existe uma conta cadastrada com este e-mail.", ErrorType.Conflict));
        }

        var tenantResult = Tenant.Create(request.StoreName.Trim(), TenantStatus.Trial);
        if (!tenantResult.IsSuccess)
        {
            return Result<AuthTokenResponse>.Failure(tenantResult.Error!);
        }

        var tenant = tenantResult.Value!;
        if (currentTenantAccessor is CurrentTenantAccessor mutableAccessor)
        {
            mutableAccessor.SetTenant(tenant.Id);
        }

        await tenantRepository.AddAsync(tenant, ct);

        var subscriptionResult = TenantSaasSubscription.CreateTrial(
            tenant.Id,
            endsAt: tenant.TrialEndsAtUtc,
            defaultTier: request.PlanTier);

        if (subscriptionResult.IsSuccess && subscriptionResult.Value is not null)
        {
            await subscriptionRepository.AddAsync(subscriptionResult.Value, ct);
            await subscriptionRepository.SaveChangesAsync(ct);
        }

        var createUserResult = await userRepository.CreateUserAsync(
            tenant.Id,
            normalizedEmail,
            request.Password,
            ShopRole.Administrator,
            ct);

        if (!createUserResult.IsSuccess)
        {
            return Result<AuthTokenResponse>.Failure(createUserResult.Error!);
        }

        var adminUser = createUserResult.Value!;

        var permissions = ShopRolePermissions.GetPermissions(ShopRole.Administrator);
        var accessToken = tokenService.GenerateAccessToken(
            adminUser.Id,
            adminUser.Email,
            tenant.Id,
            ShopRole.Administrator,
            permissions);

        var refreshTokenString = tokenService.GenerateRefreshToken();
        var refreshTokenHash = tokenService.HashToken(refreshTokenString);
        var refreshTokenResult = RefreshToken.Create(
            tenant.Id,
            adminUser.Id,
            refreshTokenHash,
            DateTimeOffset.UtcNow.AddDays(7));

        if (refreshTokenResult.IsSuccess && refreshTokenResult.Value is not null)
        {
            await refreshTokenRepository.AddAsync(refreshTokenResult.Value, ct);
        }

        return Result<AuthTokenResponse>.Success(new AuthTokenResponse(
            accessToken.Token,
            refreshTokenString,
            accessToken.ExpiresInSeconds,
            "Bearer",
            adminUser.Id,
            adminUser.Email,
            tenant.Id,
            ShopRole.Administrator.ToString(),
            permissions.Select(p => p.ToString()).ToArray()));
    }
}
