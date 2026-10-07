using CarWashSaaS.Api.Services;
using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Application;
using CarWashSaaS.Tenants.Domain;

namespace CarWashSaaS.IntegrationTests;

public sealed class TenantOnboardingServiceTests
{
    private readonly FakeTenantRepository _tenantRepository = new();
    private readonly FakeIdentityUserRepository _userRepository = new();
    private readonly FakeTenantSubscriptionRepository _subscriptionRepository = new();
    private readonly FakeRefreshTokenRepository _refreshTokenRepository = new();
    private readonly FakeTokenService _tokenService = new();
    private readonly TenantOnboardingService _service;

    public TenantOnboardingServiceTests()
    {
        _service = new TenantOnboardingService(
            _tenantRepository,
            _userRepository,
            _subscriptionRepository,
            _tokenService,
            _refreshTokenRepository);
    }

    [Fact]
    public async Task RegisterAsync_WithValidData_ShouldCreateTenantSubscriptionUserAndReturnToken()
    {
        var request = new RegisterTenantRequest(
            StoreName: "Auto Brilho Estética",
            AdminName: "Carlos Eduardo",
            Email: "carlos@autobrilho.com",
            Password: "Password123!",
            Phone: "(11) 99999-8888",
            City: "Curitiba",
            PlanTier: SaasPlanTier.Pro);

        var result = await _service.RegisterAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("access_token_jwt", result.Value.AccessToken);
        Assert.Equal("carlos@autobrilho.com", result.Value.Email);
        Assert.Equal(ShopRole.Administrator.ToString(), result.Value.Role);
        Assert.NotEqual(Guid.Empty, result.Value.TenantId ?? Guid.Empty);

        Assert.Single(_tenantRepository.Tenants);
        Assert.Equal("Auto Brilho Estética", _tenantRepository.Tenants[0].Name);
        Assert.Equal(TenantStatus.Trial, _tenantRepository.Tenants[0].Status);

        Assert.Single(_userRepository.Users);
        Assert.Equal("carlos@autobrilho.com", _userRepository.Users[0].Email);
        Assert.Equal(ShopRole.Administrator, _userRepository.Users[0].Role);

        Assert.Single(_subscriptionRepository.Subscriptions);
        Assert.Equal(SaasPlanTier.Pro, _subscriptionRepository.Subscriptions[0].PlanTier);
        Assert.Equal(TenantSubscriptionStatus.Trial, _subscriptionRepository.Subscriptions[0].Status);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ShouldReturnConflict()
    {
        var tenantId = Guid.NewGuid();
        _userRepository.Users.Add(new IdentityUserSnapshot(Guid.NewGuid(), tenantId, "existing@test.com", ShopRole.Administrator));

        var request = new RegisterTenantRequest(
            StoreName: "Novo Lava Jato",
            AdminName: "Marcos",
            Email: "existing@test.com",
            Password: "Password123!",
            Phone: "(11) 98888-7777");

        var result = await _service.RegisterAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("onboarding.email.duplicate", result.Error.Code);
        Assert.Empty(_tenantRepository.Tenants);
    }

    [Theory]
    [InlineData("", "carlos@test.com", "Password123!", "(11) 99999-8888")]
    [InlineData("Loja", "", "Password123!", "(11) 99999-8888")]
    [InlineData("Loja", "carlos@test.com", "12345", "(11) 99999-8888")]
    [InlineData("Loja", "carlos@test.com", "Password123!", "")]
    public async Task RegisterAsync_WithInvalidInput_ShouldReturnValidationFailure(
        string storeName,
        string email,
        string password,
        string phone)
    {
        var request = new RegisterTenantRequest(
            StoreName: storeName,
            AdminName: "Carlos",
            Email: email,
            Password: password,
            Phone: phone);

        var result = await _service.RegisterAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    private sealed class FakeTenantRepository : ITenantRepository
    {
        public List<Tenant> Tenants { get; } = [];

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Tenants.FirstOrDefault(t => t.Id == id));

        public Task<PagedResult<GlobalTenantSummaryDto>> SearchGlobalTenantsAsync(GetGlobalTenantsRequest request, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<GlobalTenantSummaryDto>([], 0, 1, 20));

        public Task<GlobalTenantSummaryDto?> GetSummaryByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<GlobalTenantSummaryDto?>(null);

        public Task AddAsync(Tenant tenant, CancellationToken ct = default)
        {
            Tenants.Add(tenant);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Tenant tenant, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeIdentityUserRepository : IIdentityUserRepository
    {
        public List<IdentityUserSnapshot> Users { get; } = [];

        public Task<IdentityUserSnapshot?> FindByEmailAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

        public Task<IdentityUserSnapshot?> FindByIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Id == userId));

        public Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken ct = default) =>
            Task.FromResult(true);

        public Task<Result<IdentityUserSnapshot>> CreateUserAsync(Guid tenantId, string email, string password, ShopRole role, CancellationToken ct = default)
        {
            var user = new IdentityUserSnapshot(Guid.NewGuid(), tenantId, email, role);
            Users.Add(user);
            return Task.FromResult(Result<IdentityUserSnapshot>.Success(user));
        }

        public Task<IReadOnlyCollection<IdentityUserSnapshot>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<IdentityUserSnapshot>>(Users.Where(u => u.TenantId == tenantId).ToList());
    }

    private sealed class FakeTenantSubscriptionRepository : ITenantSaasSubscriptionRepository
    {
        public List<TenantSaasSubscription> Subscriptions { get; } = [];

        public Task<TenantSaasSubscription?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Subscriptions.FirstOrDefault(s => s.TenantId == tenantId));

        public Task<TenantSaasSubscription?> GetByGatewaySubscriptionIdAsync(string gatewaySubscriptionId, CancellationToken ct = default) =>
            Task.FromResult(Subscriptions.FirstOrDefault(s => s.GatewaySubscriptionId == gatewaySubscriptionId));

        public Task<IReadOnlyList<TenantSaasSubscription>> ListAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TenantSaasSubscription>>(Subscriptions);

        public Task AddAsync(TenantSaasSubscription subscription, CancellationToken ct = default)
        {
            Subscriptions.Add(subscription);
            return Task.CompletedTask;
        }

        public void Update(TenantSaasSubscription subscription)
        {
            var index = Subscriptions.FindIndex(s => s.Id == subscription.Id);
            if (index >= 0)
            {
                Subscriptions[index] = subscription;
            }
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public Task AddAsync(RefreshToken token, CancellationToken ct = default) => Task.CompletedTask;
        public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) => Task.FromResult<RefreshToken?>(null);
        public Task UpdateAsync(RefreshToken token, CancellationToken ct = default) => Task.CompletedTask;
        public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAtUtc, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeTokenService : ITokenService
    {
        public GeneratedToken GenerateAccessToken(Guid userId, string email, Guid tenantId, ShopRole role, IReadOnlyCollection<ShopPermission> permissions) =>
            new("access_token_jwt", 3600);

        public GeneratedToken GeneratePlatformAccessToken(Guid userId, string email, string fullName, PlatformRole role, IReadOnlyCollection<PlatformPermission> permissions) =>
            new("platform_access_token_jwt", 3600);

        public string GenerateRefreshToken() => "generated_refresh_token";

        public string HashToken(string token) => $"hash_{token}";
    }
}
