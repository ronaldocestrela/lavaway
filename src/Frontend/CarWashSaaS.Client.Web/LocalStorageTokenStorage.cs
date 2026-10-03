using CarWashSaaS.Client.Core;
using Microsoft.JSInterop;

namespace CarWashSaaS.Client.Web;

public sealed class LocalStorageTokenStorage(IJSRuntime jsRuntime) : ITokenStorage
{
    private const string AccessTokenKey = "lavaway_access_token";
    private const string RefreshTokenKey = "lavaway_refresh_token";

    private string? _cachedAccessToken;
    private string? _cachedRefreshToken;
    private bool _isInitialized;

    public async Task<string?> GetAccessTokenAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        return _cachedAccessToken;
    }

    public async Task SetAccessTokenAsync(string token)
    {
        _cachedAccessToken = token;
        _isInitialized = true;
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", AccessTokenKey, token);
        }
        catch (Exception)
        {
            // Fallback: mantém em memória se JSInterop não estiver disponível
        }
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        return _cachedRefreshToken;
    }

    public async Task SetRefreshTokenAsync(string token)
    {
        _cachedRefreshToken = token;
        _isInitialized = true;
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", RefreshTokenKey, token);
        }
        catch (Exception)
        {
            // Fallback: mantém em memória se JSInterop não estiver disponível
        }
    }

    public async Task ClearAsync()
    {
        _cachedAccessToken = null;
        _cachedRefreshToken = null;
        _isInitialized = true;
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.removeItem", AccessTokenKey);
            await jsRuntime.InvokeVoidAsync("localStorage.removeItem", RefreshTokenKey);
        }
        catch (Exception)
        {
            // Fallback: mantém em memória se JSInterop não estiver disponível
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            _cachedAccessToken = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", AccessTokenKey);
            _cachedRefreshToken = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", RefreshTokenKey);
        }
        catch (Exception)
        {
            // Ambiente de teste ou JSInterop indisponível
        }
        finally
        {
            _isInitialized = true;
        }
    }
}
