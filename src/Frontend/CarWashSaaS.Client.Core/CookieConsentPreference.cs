namespace CarWashSaaS.Client.Core;

public sealed class CookieConsentPreference
{
    public bool Necessary { get; set; } = true;
    public bool Analytics { get; set; }
    public DateTimeOffset DecidedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
