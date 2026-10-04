namespace CarWashSaaS.Shared.Contracts;

public sealed record WhatsAppQuotaDto(
    int MessagesSentToday,
    int DailyLimit,
    int MessagesSentCurrentMinute,
    int MinuteLimit,
    bool IsThrottled);
