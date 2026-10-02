using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Identity.Domain;

public sealed class RefreshToken : IMustHaveTenant
{
    private RefreshToken()
    {
    }

    private RefreshToken(
        Guid id,
        Guid tenantId,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsActive(DateTimeOffset nowUtc) => RevokedAtUtc == null && ExpiresAtUtc > nowUtc;
    public bool IsExpired(DateTimeOffset nowUtc) => ExpiresAtUtc <= nowUtc;
    public bool IsRevoked => RevokedAtUtc != null;

    public static Result<RefreshToken> Create(
        Guid tenantId,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset? createdAtUtc = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<RefreshToken>.Failure(new Error("refresh_token.tenant_id.required", "TenantId is required.", ErrorType.Validation));
        }

        if (userId == Guid.Empty)
        {
            return Result<RefreshToken>.Failure(new Error("refresh_token.user_id.required", "UserId is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return Result<RefreshToken>.Failure(new Error("refresh_token.hash.required", "Token hash is required.", ErrorType.Validation));
        }

        var created = createdAtUtc ?? DateTimeOffset.UtcNow;
        if (expiresAtUtc <= created)
        {
            return Result<RefreshToken>.Failure(new Error("refresh_token.expiration.invalid", "Expiration date must be in the future.", ErrorType.Validation));
        }

        return Result<RefreshToken>.Success(new RefreshToken(
            Guid.CreateVersion7(),
            tenantId,
            userId,
            tokenHash.Trim(),
            expiresAtUtc,
            created));
    }

    public Result Revoke(DateTimeOffset revokedAtUtc, string? replacedByTokenHash = null)
    {
        if (IsRevoked)
        {
            return Result.Failure(new Error("refresh_token.already_revoked", "Refresh token has already been revoked.", ErrorType.Conflict));
        }

        RevokedAtUtc = revokedAtUtc;
        ReplacedByTokenHash = string.IsNullOrWhiteSpace(replacedByTokenHash) ? null : replacedByTokenHash.Trim();
        return Result.Success();
    }
}
