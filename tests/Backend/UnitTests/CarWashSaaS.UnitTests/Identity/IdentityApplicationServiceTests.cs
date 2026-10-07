using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.UnitTests.Identity;

public sealed class IdentityApplicationServiceTests
{
    private readonly FakeIdentityUserRepository _userRepository = new();
    private readonly FakeRefreshTokenRepository _refreshTokenRepository = new();
    private readonly FakeTokenService _tokenService = new();
    private readonly IdentityApplicationService _service;

    public IdentityApplicationServiceTests()
    {
        _service = new IdentityApplicationService(_userRepository, _refreshTokenRepository, _tokenService);
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmptyEmailOrPassword_ShouldReturnValidationFailure()
    {
        var result1 = await _service.AuthenticateAsync(new LoginRequest("", "secret"));
        var result2 = await _service.AuthenticateAsync(new LoginRequest("user@test.com", ""));

        Assert.False(result1.IsSuccess);
        Assert.Equal(ErrorType.Validation, result1.Error!.Type);

        Assert.False(result2.IsSuccess);
        Assert.Equal(ErrorType.Validation, result2.Error!.Type);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenUserNotFound_ShouldReturnUnauthorized()
    {
        var result = await _service.AuthenticateAsync(new LoginRequest("nonexistent@test.com", "secret"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
        Assert.Equal("auth.invalid_credentials", result.Error.Code);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenTenantMismatch_ShouldReturnUnauthorized()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _userRepository.Users.Add(new IdentityUserSnapshot(userId, tenantA, "user@test.com", ShopRole.Receptionist));
        _userRepository.Passwords[userId] = "Password123!";

        var result = await _service.AuthenticateAsync(new LoginRequest("user@test.com", "Password123!", tenantB));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenPasswordIncorrect_ShouldReturnUnauthorized()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _userRepository.Users.Add(new IdentityUserSnapshot(userId, tenantId, "user@test.com", ShopRole.Receptionist));
        _userRepository.Passwords[userId] = "CorrectPassword123!";

        var result = await _service.AuthenticateAsync(new LoginRequest("user@test.com", "WrongPassword!"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenCredentialsValid_ShouldReturnAuthTokensAndStoreRefreshToken()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _userRepository.Users.Add(new IdentityUserSnapshot(userId, tenantId, "admin@test.com", ShopRole.Administrator));
        _userRepository.Passwords[userId] = "StrongPass123!";

        var result = await _service.AuthenticateAsync(new LoginRequest("admin@test.com", "StrongPass123!"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("access_token_jwt", result.Value.AccessToken);
        Assert.Equal("generated_refresh_token", result.Value.RefreshToken);
        Assert.Equal(userId, result.Value.UserId);
        Assert.Equal(tenantId, result.Value.TenantId);
        Assert.Equal(ShopRole.Administrator.ToString(), result.Value.Role);
        Assert.Contains(ShopPermission.ConfigureStore.ToString(), result.Value.Permissions);

        Assert.Single(_refreshTokenRepository.Tokens);
        var storedToken = _refreshTokenRepository.Tokens.Values.First();
        Assert.Equal(userId, storedToken.UserId);
        Assert.Equal(tenantId, storedToken.TenantId);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenTokenNotFound_ShouldReturnUnauthorized()
    {
        var result = await _service.RefreshTokenAsync(new RefreshTokenRequest("nonexistent_token"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
        Assert.Equal("auth.refresh_token.invalid", result.Error.Code);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenTokenIsRevoked_ShouldRevokeAllAndReturnUnauthorized()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tokenString = "revoked_token";
        var tokenHash = _tokenService.HashToken(tokenString);

        var token = RefreshToken.Create(tenantId, userId, tokenHash, DateTimeOffset.UtcNow.AddDays(7)).Value!;
        token.Revoke(DateTimeOffset.UtcNow);
        _refreshTokenRepository.Tokens[tokenHash] = token;

        var result = await _service.RefreshTokenAsync(new RefreshTokenRequest(tokenString));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
        Assert.Equal("auth.refresh_token.revoked", result.Error.Code);
        Assert.Contains(userId, _refreshTokenRepository.UsersWithRevokedAll);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenTokenIsExpired_ShouldReturnUnauthorized()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tokenString = "expired_token";
        var tokenHash = _tokenService.HashToken(tokenString);

        var past = DateTimeOffset.UtcNow.AddDays(-10);
        var token = RefreshToken.Create(tenantId, userId, tokenHash, past.AddDays(1), past).Value!;
        _refreshTokenRepository.Tokens[tokenHash] = token;

        var result = await _service.RefreshTokenAsync(new RefreshTokenRequest(tokenString));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
        Assert.Equal("auth.refresh_token.expired", result.Error.Code);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenValid_ShouldRotateAndReturnNewTokens()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _userRepository.Users.Add(new IdentityUserSnapshot(userId, tenantId, "user@test.com", ShopRole.Operator));

        var tokenString = "valid_token";
        var tokenHash = _tokenService.HashToken(tokenString);

        var token = RefreshToken.Create(tenantId, userId, tokenHash, DateTimeOffset.UtcNow.AddDays(7)).Value!;
        _refreshTokenRepository.Tokens[tokenHash] = token;

        var result = await _service.RefreshTokenAsync(new RefreshTokenRequest(tokenString));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("access_token_jwt", result.Value.AccessToken);
        Assert.Equal("generated_refresh_token", result.Value.RefreshToken);

        // Old token should be revoked
        Assert.True(token.IsRevoked);
        Assert.Equal(_tokenService.HashToken("generated_refresh_token"), token.ReplacedByTokenHash);

        // New token should be added
        var newHash = _tokenService.HashToken("generated_refresh_token");
        Assert.True(_refreshTokenRepository.Tokens.ContainsKey(newHash));
    }

    [Fact]
    public async Task RevokeTokenAsync_WhenValid_ShouldRevokeToken()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tokenString = "token_to_revoke";
        var tokenHash = _tokenService.HashToken(tokenString);

        var token = RefreshToken.Create(tenantId, userId, tokenHash, DateTimeOffset.UtcNow.AddDays(7)).Value!;
        _refreshTokenRepository.Tokens[tokenHash] = token;

        var result = await _service.RevokeTokenAsync(new RevokeTokenRequest(tokenString));

        Assert.True(result.IsSuccess);
        Assert.True(token.IsRevoked);
    }

    [Fact]
    public async Task CreateUserAsync_WithValidData_ShouldCreateUser()
    {
        var tenantId = Guid.NewGuid();
        var request = new CreateUserRequest("colleague@test.com", "ColleaguePass123!", "Operator");

        var result = await _service.CreateUserAsync(tenantId, request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(tenantId, result.Value.TenantId);
        Assert.Equal("colleague@test.com", result.Value.Email);
        Assert.Equal("Operator", result.Value.Role);
        Assert.Contains(ShopPermission.UpdateWorkOrderStatus.ToString(), result.Value.Permissions);
    }

    [Fact]
    public async Task ListUsersAsync_ShouldReturnUsersForTenant()
    {
        var tenantId = Guid.NewGuid();
        _userRepository.Users.Add(new IdentityUserSnapshot(Guid.NewGuid(), tenantId, "user1@test.com", ShopRole.Receptionist));
        _userRepository.Users.Add(new IdentityUserSnapshot(Guid.NewGuid(), tenantId, "user2@test.com", ShopRole.Operator));
        _userRepository.Users.Add(new IdentityUserSnapshot(Guid.NewGuid(), Guid.NewGuid(), "other@test.com", ShopRole.Administrator));

        var result = await _service.ListUsersAsync(tenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.All(result.Value, u => Assert.Equal(tenantId, u.TenantId));
    }

    private sealed class FakeIdentityUserRepository : IIdentityUserRepository
    {
        public List<IdentityUserSnapshot> Users { get; } = [];
        public Dictionary<Guid, string> Passwords { get; } = [];

        public Task<IdentityUserSnapshot?> FindByEmailAsync(string email, CancellationToken ct = default)
        {
            var user = Users.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(user);
        }

        public Task<IdentityUserSnapshot?> FindByIdAsync(Guid userId, CancellationToken ct = default)
        {
            var user = Users.FirstOrDefault(u => u.Id == userId);
            return Task.FromResult(user);
        }

        public Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken ct = default)
        {
            var valid = Passwords.TryGetValue(userId, out var stored) && stored == password;
            return Task.FromResult(valid);
        }

        public Task<Result<IdentityUserSnapshot>> CreateUserAsync(Guid tenantId, string email, string password, ShopRole role, CancellationToken ct = default)
        {
            var user = new IdentityUserSnapshot(Guid.NewGuid(), tenantId, email, role);
            Users.Add(user);
            Passwords[user.Id] = password;
            return Task.FromResult(Result<IdentityUserSnapshot>.Success(user));
        }

        public Task<IReadOnlyCollection<IdentityUserSnapshot>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        {
            var list = Users.Where(u => u.TenantId == tenantId).ToArray();
            return Task.FromResult<IReadOnlyCollection<IdentityUserSnapshot>>(list);
        }
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public Dictionary<string, RefreshToken> Tokens { get; } = [];
        public List<Guid> UsersWithRevokedAll { get; } = [];

        public Task AddAsync(RefreshToken token, CancellationToken ct = default)
        {
            Tokens[token.TokenHash] = token;
            return Task.CompletedTask;
        }

        public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        {
            Tokens.TryGetValue(tokenHash, out var token);
            return Task.FromResult(token);
        }

        public Task UpdateAsync(RefreshToken token, CancellationToken ct = default)
        {
            Tokens[token.TokenHash] = token;
            return Task.CompletedTask;
        }

        public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAtUtc, CancellationToken ct = default)
        {
            UsersWithRevokedAll.Add(userId);
            foreach (var token in Tokens.Values.Where(t => t.UserId == userId))
            {
                if (!token.IsRevoked)
                {
                    token.Revoke(revokedAtUtc);
                }
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTokenService : ITokenService
    {
        public GeneratedToken GenerateAccessToken(Guid userId, string email, Guid tenantId, ShopRole role, IReadOnlyCollection<ShopPermission> permissions)
        {
            return new GeneratedToken("access_token_jwt", 3600);
        }

        public GeneratedToken GeneratePlatformAccessToken(Guid userId, string email, string fullName, PlatformRole role, IReadOnlyCollection<PlatformPermission> permissions)
        {
            return new GeneratedToken("platform_access_token_jwt", 3600);
        }


        public string GenerateRefreshToken()
        {
            return "generated_refresh_token";
        }

        public string HashToken(string token)
        {
            return $"hash_{token}";
        }
    }
}
