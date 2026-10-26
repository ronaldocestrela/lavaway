namespace CarWashSaaS.Client.Core;

public class InMemoryCookieConsentService : ICookieConsentService
{
    private CookieConsentPreference? _preference;

    public event Action? OnConsentChanged;

    public InMemoryCookieConsentService(CookieConsentPreference? initial = null)
    {
        _preference = initial;
    }

    public Task<CookieConsentPreference?> GetConsentAsync() => Task.FromResult(_preference);

    public Task<bool> HasUserConsentedAsync() => Task.FromResult(_preference != null);

    public Task SaveConsentAsync(bool analyticsGranted)
    {
        _preference = new CookieConsentPreference
        {
            Necessary = true,
            Analytics = analyticsGranted,
            DecidedAtUtc = DateTimeOffset.UtcNow
        };
        OnConsentChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task RevokeConsentAsync()
    {
        return SaveConsentAsync(false);
    }
}
