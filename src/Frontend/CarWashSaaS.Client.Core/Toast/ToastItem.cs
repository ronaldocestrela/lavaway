namespace CarWashSaaS.Client.Core;

public sealed class ToastItem
{
    public Guid Id { get; }
    public string Message { get; }
    public string? Title { get; }
    public ToastLevel Level { get; }
    public int DurationMs { get; }
    public DateTimeOffset CreatedAt { get; }

    public ToastItem(
        string message,
        ToastLevel level = ToastLevel.Info,
        string? title = null,
        int durationMs = 4500,
        Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        Message = message;
        Level = level;
        Title = title;
        DurationMs = durationMs;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
