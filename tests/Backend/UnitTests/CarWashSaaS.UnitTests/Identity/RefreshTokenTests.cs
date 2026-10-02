using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.UnitTests.Identity;

public sealed class RefreshTokenTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tokenHash = "hash123";
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddDays(7);

        var result = RefreshToken.Create(tenantId, userId, tokenHash, expiresAt, now);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal(tenantId, result.Value.TenantId);
        Assert.Equal(userId, result.Value.UserId);
        Assert.Equal(tokenHash, result.Value.TokenHash);
        Assert.Equal(expiresAt, result.Value.ExpiresAtUtc);
        Assert.Equal(now, result.Value.CreatedAtUtc);
        Assert.Null(result.Value.RevokedAtUtc);
        Assert.True(result.Value.IsActive(now));
        Assert.False(result.Value.IsExpired(now));
        Assert.False(result.Value.IsRevoked);
    }

    [Fact]
    public void Create_WithEmptyTenant_ShouldFail()
    {
        var result = RefreshToken.Create(Guid.Empty, Guid.NewGuid(), "hash", DateTimeOffset.UtcNow.AddDays(1));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("refresh_token.tenant_id.required", result.Error.Code);
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldFail()
    {
        var result = RefreshToken.Create(Guid.NewGuid(), Guid.Empty, "hash", DateTimeOffset.UtcNow.AddDays(1));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("refresh_token.user_id.required", result.Error.Code);
    }

    [Fact]
    public void Create_WithEmptyHash_ShouldFail()
    {
        var result = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "   ", DateTimeOffset.UtcNow.AddDays(1));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("refresh_token.hash.required", result.Error.Code);
    }

    [Fact]
    public void Create_WithPastExpiration_ShouldFail()
    {
        var now = DateTimeOffset.UtcNow;
        var result = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", now.AddMinutes(-5), now);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("refresh_token.expiration.invalid", result.Error.Code);
    }

    [Fact]
    public void Revoke_WhenActive_ShouldSucceed()
    {
        var now = DateTimeOffset.UtcNow;
        var token = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", now.AddDays(7), now).Value!;

        var revokeTime = now.AddDays(1);
        var result = token.Revoke(revokeTime, "replacementHash");

        Assert.True(result.IsSuccess);
        Assert.True(token.IsRevoked);
        Assert.False(token.IsActive(revokeTime));
        Assert.Equal(revokeTime, token.RevokedAtUtc);
        Assert.Equal("replacementHash", token.ReplacedByTokenHash);
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ShouldFailWithConflict()
    {
        var now = DateTimeOffset.UtcNow;
        var token = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", now.AddDays(7), now).Value!;
        token.Revoke(now.AddHours(1));

        var secondRevoke = token.Revoke(now.AddHours(2));

        Assert.False(secondRevoke.IsSuccess);
        Assert.Equal(ErrorType.Conflict, secondRevoke.Error!.Type);
        Assert.Equal("refresh_token.already_revoked", secondRevoke.Error.Code);
    }

    [Fact]
    public void IsExpired_WhenCurrentTimeAfterExpiration_ShouldReturnTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var token = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", now.AddDays(1), now).Value!;

        Assert.True(token.IsExpired(now.AddDays(2)));
        Assert.False(token.IsActive(now.AddDays(2)));
    }
}
