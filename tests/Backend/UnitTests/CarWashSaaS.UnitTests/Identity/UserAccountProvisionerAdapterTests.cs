using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.UnitTests.Identity;

public sealed class UserAccountProvisionerAdapterTests
{
    private readonly FakeIdentityUserRepository _userRepository = new();
    private readonly FakeRefreshTokenRepository _refreshTokenRepository = new();
    private readonly UserAccountProvisionerAdapter _adapter;

    public UserAccountProvisionerAdapterTests()
    {
        _adapter = new UserAccountProvisionerAdapter(_userRepository, _refreshTokenRepository);
    }

    [Fact]
    public async Task ProvisionUserAsync_ShouldSucceed_WhenValid()
    {
        var tenantId = Guid.NewGuid();

        var result = await _adapter.ProvisionUserAsync(tenantId, "operador@lava.com", "SenhaForte123!", "Operator");

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
        Assert.Single(_userRepository.Users);
        Assert.Equal("operador@lava.com", _userRepository.Users[0].Email);
        Assert.Equal(ShopRole.Operator, _userRepository.Users[0].Role);
    }

    [Theory]
    [InlineData("Receptionist", ShopRole.Receptionist)]
    [InlineData("Administrator", ShopRole.Administrator)]
    [InlineData("Operator", ShopRole.Operator)]
    [InlineData("InvalidRole", ShopRole.Operator)] // fallback para Operator
    public async Task ProvisionUserAsync_ShouldMapRoleCorrectly(string roleInput, ShopRole expectedRole)
    {
        var tenantId = Guid.NewGuid();

        var result = await _adapter.ProvisionUserAsync(tenantId, $"user_{roleInput}@lava.com", "SenhaForte123!", roleInput);

        Assert.True(result.IsSuccess);
        var user = _userRepository.Users.First(u => string.Equals(u.Email, $"user_{roleInput}@lava.com", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(expectedRole, user.Role);
    }

    [Fact]
    public async Task ProvisionUserAsync_ShouldFail_WhenEmptyTenant()
    {
        var result = await _adapter.ProvisionUserAsync(Guid.Empty, "user@test.com", "Password123!", "Operator");

        Assert.False(result.IsSuccess);
        Assert.Equal("user.tenant_id.required", result.Error!.Code);
    }

    [Fact]
    public async Task ProvisionUserAsync_ShouldFail_WhenEmptyEmail()
    {
        var result = await _adapter.ProvisionUserAsync(Guid.NewGuid(), "   ", "Password123!", "Operator");

        Assert.False(result.IsSuccess);
        Assert.Equal("user.email.required", result.Error!.Code);
    }

    [Fact]
    public async Task ProvisionUserAsync_ShouldFail_WhenPasswordTooShort()
    {
        var result = await _adapter.ProvisionUserAsync(Guid.NewGuid(), "user@test.com", "123", "Operator");

        Assert.False(result.IsSuccess);
        Assert.Equal("user.password.invalid", result.Error!.Code);
    }

    [Fact]
    public async Task SetUserActiveStatusAsync_WhenDeactivating_ShouldRevokeTokens()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _userRepository.Users.Add(new IdentityUserSnapshot(userId, tenantId, "user@test.com", ShopRole.Operator, true));

        var result = await _adapter.SetUserActiveStatusAsync(tenantId, userId, false);

        Assert.True(result.IsSuccess);
        Assert.Contains(userId, _refreshTokenRepository.UsersWithRevokedAll);
    }

    [Fact]
    public async Task UpdateUserAccessAsync_ShouldUpdateUserAndRevokeTokensIfDeactivated()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _userRepository.Users.Add(new IdentityUserSnapshot(userId, tenantId, "old@test.com", ShopRole.Operator, true));

        var result = await _adapter.UpdateUserAccessAsync(tenantId, userId, "new@test.com", "NewPass123!", "Receptionist", false);

        Assert.True(result.IsSuccess);
        var user = _userRepository.Users.First(u => u.Id == userId);
        Assert.Equal("new@test.com", user.Email);
        Assert.Equal(ShopRole.Receptionist, user.Role);
        Assert.False(user.IsActive);
        Assert.Contains(userId, _refreshTokenRepository.UsersWithRevokedAll);
    }

    private sealed class FakeIdentityUserRepository : IIdentityUserRepository
    {
        public List<IdentityUserSnapshot> Users { get; } = [];
        public Dictionary<Guid, string> Passwords { get; } = [];

        public Task<IdentityUserSnapshot?> FindByEmailAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

        public Task<IdentityUserSnapshot?> FindByIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Id == userId));

        public Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken ct = default) =>
            Task.FromResult(Passwords.TryGetValue(userId, out var stored) && stored == password);

        public Task<Result<IdentityUserSnapshot>> CreateUserAsync(Guid tenantId, string email, string password, ShopRole role, CancellationToken ct = default)
        {
            if (Users.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult(Result<IdentityUserSnapshot>.Failure(new Error("user.email.duplicate", "Duplicate", ErrorType.Conflict)));
            }

            var user = new IdentityUserSnapshot(Guid.NewGuid(), tenantId, email, role, true);
            Users.Add(user);
            Passwords[user.Id] = password;
            return Task.FromResult(Result<IdentityUserSnapshot>.Success(user));
        }

        public Task<IReadOnlyCollection<IdentityUserSnapshot>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<IdentityUserSnapshot>>(Users.Where(u => u.TenantId == tenantId).ToList());

        public Task<Result> SetActiveStatusAsync(Guid userId, bool isActive, CancellationToken ct = default)
        {
            var idx = Users.FindIndex(u => u.Id == userId);
            if (idx >= 0)
            {
                var existing = Users[idx];
                Users[idx] = new IdentityUserSnapshot(existing.Id, existing.TenantId, existing.Email, existing.Role, isActive);
            }
            return Task.FromResult(Result.Success());
        }

        public Task<Result> UpdateUserAsync(Guid userId, string email, string? newPassword, ShopRole? role, bool isActive, CancellationToken ct = default)
        {
            var idx = Users.FindIndex(u => u.Id == userId);
            if (idx >= 0)
            {
                var existing = Users[idx];
                Users[idx] = new IdentityUserSnapshot(existing.Id, existing.TenantId, email, role ?? existing.Role, isActive);
                if (!string.IsNullOrWhiteSpace(newPassword))
                {
                    Passwords[userId] = newPassword;
                }
            }
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public List<Guid> UsersWithRevokedAll { get; } = [];

        public Task AddAsync(RefreshToken token, CancellationToken ct = default) => Task.CompletedTask;
        public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) => Task.FromResult<RefreshToken?>(null);
        public Task UpdateAsync(RefreshToken token, CancellationToken ct = default) => Task.CompletedTask;

        public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAtUtc, CancellationToken ct = default)
        {
            UsersWithRevokedAll.Add(userId);
            return Task.CompletedTask;
        }
    }
}
