using System.Security.Claims;
using System.Text.Json;
using CarWashSaaS.Client.Core;
using Microsoft.AspNetCore.Components.Authorization;

namespace CarWashSaaS.Client.Web;

public sealed class JwtAuthenticationStateProvider(ITokenStorage tokenStorage) : AuthenticationStateProvider
{
    private static readonly AuthenticationState AnonymousState = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await tokenStorage.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return AnonymousState;
        }

        var claims = ParseClaimsFromJwt(token).ToList();
        if (claims.Count == 0 || IsTokenExpired(claims))
        {
            await tokenStorage.ClearAsync();
            return AnonymousState;
        }

        var identity = new ClaimsIdentity(claims, "jwt", "email", "role");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void NotifyUserAuthentication(string token)
    {
        var claims = ParseClaimsFromJwt(token).ToList();
        if (claims.Count == 0 || IsTokenExpired(claims))
        {
            NotifyAuthenticationStateChanged(Task.FromResult(AnonymousState));
            return;
        }

        var identity = new ClaimsIdentity(claims, "jwt", "email", "role");
        var authState = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        NotifyAuthenticationStateChanged(authState);
    }

    public void NotifyUserLogout()
    {
        NotifyAuthenticationStateChanged(Task.FromResult(AnonymousState));
    }

    public static bool IsTokenExpired(IEnumerable<Claim> claims)
    {
        var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
        if (expClaim is null)
        {
            return true;
        }

        if (long.TryParse(expClaim.Value, out var expSeconds))
        {
            var expirationTime = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
            return expirationTime <= DateTimeOffset.UtcNow;
        }

        return true;
    }

    public static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var claims = new List<Claim>();
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2)
            {
                return claims;
            }

            var payload = parts[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

            if (keyValuePairs is null)
            {
                return claims;
            }

            foreach (var (key, value) in keyValuePairs)
            {
                if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in element.EnumerateArray())
                    {
                        var str = item.ToString();
                        claims.Add(new Claim(key, str));
                        if (string.Equals(key, "role", StringComparison.OrdinalIgnoreCase))
                        {
                            claims.Add(new Claim(ClaimTypes.Role, str));
                        }
                    }
                }
                else if (value is JsonElement el && el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var num))
                {
                    claims.Add(new Claim(key, num.ToString()));
                }
                else
                {
                    var str = value?.ToString() ?? string.Empty;
                    claims.Add(new Claim(key, str));
                    if (string.Equals(key, "role", StringComparison.OrdinalIgnoreCase))
                    {
                        claims.Add(new Claim(ClaimTypes.Role, str));
                    }
                    if (string.Equals(key, "email", StringComparison.OrdinalIgnoreCase))
                    {
                        claims.Add(new Claim(ClaimTypes.Email, str));
                        claims.Add(new Claim(ClaimTypes.Name, str));
                    }
                }
            }
        }
        catch
        {
            return [];
        }

        return claims;
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        base64 = base64.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}

