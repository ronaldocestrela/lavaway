using System.Text.Json;
using CarWashSaaS.Client.Core;
using Microsoft.JSInterop;

namespace CarWashSaaS.Client.Web;

public sealed class LocalStorageCookieConsentService(IJSRuntime jsRuntime) : ICookieConsentService
{
    public const string StorageKey = "lavaway_cookie_consent_v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private CookieConsentPreference? _cached;
    private bool _initialized;

    public event Action? OnConsentChanged;

    public async Task<CookieConsentPreference?> GetConsentAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }

        return _cached;
    }

    public async Task<bool> HasUserConsentedAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }

        return _cached != null;
    }

    public async Task SaveConsentAsync(bool analyticsGranted)
    {
        _cached = new CookieConsentPreference
        {
            Necessary = true,
            Analytics = analyticsGranted,
            DecidedAtUtc = DateTimeOffset.UtcNow
        };
        _initialized = true;

        try
        {
            var json = JsonSerializer.Serialize(_cached, JsonOptions);
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch
        {
            // Fallback se localStorage bloqueado ou indisponível
        }

        try
        {
            await jsRuntime.InvokeVoidAsync("lavawayAnalytics.updateConsent", analyticsGranted);
        }
        catch
        {
            // Fallback se helper JS não estiver carregado
        }

        OnConsentChanged?.Invoke();
    }

    public async Task RevokeConsentAsync()
    {
        await SaveConsentAsync(false);
    }

    private async Task InitializeAsync()
    {
        try
        {
            var json = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                _cached = JsonSerializer.Deserialize<CookieConsentPreference>(json, JsonOptions);
            }
        }
        catch
        {
            // Ambiente de teste, SSR ou restrição de cookies
        }
        finally
        {
            _initialized = true;
        }
    }
}
