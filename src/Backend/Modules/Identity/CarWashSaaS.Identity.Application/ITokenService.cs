using CarWashSaaS.Identity.Domain;

namespace CarWashSaaS.Identity.Application;

public sealed record GeneratedToken(string Token, int ExpiresInSeconds);

public interface ITokenService
{
    GeneratedToken GenerateAccessToken(
        Guid userId,
        string email,
        Guid tenantId,
        ShopRole role,
        IReadOnlyCollection<ShopPermission> permissions);

    GeneratedToken GenerateAccessToken(
        Guid userId,
        string email,
        Guid tenantId,
        string roleName,
        IReadOnlyCollection<ShopPermission> permissions)
    {
        var role = Enum.TryParse<ShopRole>(roleName, true, out var r) ? r : ShopRole.Operator;
        return GenerateAccessToken(userId, email, tenantId, role, permissions);
    }

    GeneratedToken GeneratePlatformAccessToken(
        Guid userId,
        string email,
        string fullName,
        PlatformRole role,
        IReadOnlyCollection<PlatformPermission> permissions);

    string GenerateRefreshToken();

    string HashToken(string token);
}
