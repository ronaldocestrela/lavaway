namespace CarWashSaaS.Client.Core;

public interface ICookieConsentService
{
    event Action? OnConsentChanged;

    Task<CookieConsentPreference?> GetConsentAsync();

    Task<bool> HasUserConsentedAsync();

    Task SaveConsentAsync(bool analyticsGranted);

    Task RevokeConsentAsync();
}
