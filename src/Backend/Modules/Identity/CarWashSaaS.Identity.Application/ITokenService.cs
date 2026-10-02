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

    string GenerateRefreshToken();

    string HashToken(string token);
}
