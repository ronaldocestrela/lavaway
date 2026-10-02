using CarWashSaaS.Client.Core;

namespace CarWashSaaS.Client.Web;

public sealed class InMemoryTokenStorage : ITokenStorage
{
    private string? _accessToken;
    private string? _refreshToken;

    public Task<string?> GetAccessTokenAsync() => Task.FromResult(_accessToken);

    public Task SetAccessTokenAsync(string token)
    {
        _accessToken = token;
        return Task.CompletedTask;
    }

    public Task<string?> GetRefreshTokenAsync() => Task.FromResult(_refreshToken);

    public Task SetRefreshTokenAsync(string token)
    {
        _refreshToken = token;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        _accessToken = null;
        _refreshToken = null;
        return Task.CompletedTask;
    }
}
