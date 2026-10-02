namespace CarWashSaaS.Client.Core;

public interface ITokenStorage
{
    Task<string?> GetAccessTokenAsync();
    Task SetAccessTokenAsync(string token);
    Task<string?> GetRefreshTokenAsync();
    Task SetRefreshTokenAsync(string token);
    Task ClearAsync();
}
