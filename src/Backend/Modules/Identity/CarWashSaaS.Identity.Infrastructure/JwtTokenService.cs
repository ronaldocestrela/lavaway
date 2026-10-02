using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CarWashSaaS.Identity.Infrastructure;

public sealed class JwtTokenService : ITokenService
{
    public const string DefaultSigningKey = "LavawayDefaultSecretKeyForJwtTokens2026!MustBeAtLeast256Bits";
    private readonly string _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenLifetimeMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        _signingKey = configuration["Authentication:SigningKey"]
            ?? configuration["Authentication:Secret"]
            ?? DefaultSigningKey;
        _issuer = configuration["Authentication:Issuer"]
            ?? configuration["Authentication:Authority"]
            ?? "CarWashSaaS";
        _audience = configuration["Authentication:Audience"]
            ?? "CarWashSaaS.Client";
        _accessTokenLifetimeMinutes = configuration.GetValue("Authentication:AccessTokenLifetimeMinutes", 60);
    }

    public GeneratedToken GenerateAccessToken(
        Guid userId,
        string email,
        Guid tenantId,
        ShopRole role,
        IReadOnlyCollection<ShopPermission> permissions)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_accessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tenant_id", tenantId.ToString()),
            new("role", role.ToString()),
            new(ClaimTypes.Role, role.ToString())
        };

        foreach (var permission in permissions)
        {
            claims.Add(new Claim("permission", permission.ToString()));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        var tokenString = handler.WriteToken(token);

        return new GeneratedToken(tokenString, _accessTokenLifetimeMinutes * 60);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Base64UrlEncoder.Encode(randomBytes);
    }

    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
