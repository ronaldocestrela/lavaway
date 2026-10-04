using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.WhatsApp.Domain;

public sealed class TenantWhatsAppQuota : IMustHaveTenant
{
    private TenantWhatsAppQuota()
    {
    }

    private TenantWhatsAppQuota(Guid id, Guid tenantId, int maxPerMinute, int maxPerDay)
    {
        Id = id;
        TenantId = tenantId;
        MaxMessagesPerMinute = maxPerMinute;
        MaxMessagesPerDay = maxPerDay;
        CurrentMinuteWindowUtc = GetCurrentMinuteUtc();
        CurrentDayWindowUtc = GetCurrentDayUtc();
        SentInCurrentMinute = 0;
        SentToday = 0;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public int MaxMessagesPerMinute { get; private set; }
    public int MaxMessagesPerDay { get; private set; }
    public int SentInCurrentMinute { get; private set; }
    public int SentToday { get; private set; }
    public DateTimeOffset CurrentMinuteWindowUtc { get; private set; }
    public DateTimeOffset CurrentDayWindowUtc { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<TenantWhatsAppQuota> CreateDefault(Guid tenantId, int maxPerMinute = 20, int maxPerDay = 500)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TenantWhatsAppQuota>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        if (maxPerMinute <= 0 || maxPerDay <= 0)
        {
            return Result<TenantWhatsAppQuota>.Failure(new Error("whatsapp.quota.invalid", "Quota limits must be greater than zero.", ErrorType.Validation));
        }

        return Result<TenantWhatsAppQuota>.Success(new TenantWhatsAppQuota(
            Guid.CreateVersion7(),
            tenantId,
            maxPerMinute,
            maxPerDay));
    }

    public bool CanSend()
    {
        SyncWindows();
        return SentInCurrentMinute < MaxMessagesPerMinute && SentToday < MaxMessagesPerDay;
    }

    public Result<TenantWhatsAppQuota> RecordSend()
    {
        SyncWindows();

        if (SentInCurrentMinute >= MaxMessagesPerMinute)
        {
            return Result<TenantWhatsAppQuota>.Failure(new Error("whatsapp.quota_exceeded.minute", "Rate limit per minute exceeded.", ErrorType.Conflict));
        }

        if (SentToday >= MaxMessagesPerDay)
        {
            return Result<TenantWhatsAppQuota>.Failure(new Error("whatsapp.quota_exceeded.daily", "Daily message quota exceeded.", ErrorType.Conflict));
        }

        SentInCurrentMinute++;
        SentToday++;
        UpdatedAt = DateTimeOffset.UtcNow;

        return Result<TenantWhatsAppQuota>.Success(this);
    }

    public void SyncWindows()
    {
        var nowMinute = GetCurrentMinuteUtc();
        if (nowMinute > CurrentMinuteWindowUtc)
        {
            CurrentMinuteWindowUtc = nowMinute;
            SentInCurrentMinute = 0;
        }

        var nowDay = GetCurrentDayUtc();
        if (nowDay > CurrentDayWindowUtc)
        {
            CurrentDayWindowUtc = nowDay;
            SentToday = 0;
        }
    }

    private static DateTimeOffset GetCurrentMinuteUtc()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero);
    }

    private static DateTimeOffset GetCurrentDayUtc()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
    }
}
