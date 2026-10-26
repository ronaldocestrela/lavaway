namespace CarWashSaaS.Billing.Domain;

public sealed class ProcessedSaasWebhookEvent
{
    private ProcessedSaasWebhookEvent()
    {
    }

    public ProcessedSaasWebhookEvent(
        Guid id,
        string eventId,
        string eventType,
        Guid tenantId,
        DateTimeOffset receivedAtUtc,
        string? rawPayload = null,
        bool isPurged = false,
        DateTimeOffset? purgedAtUtc = null)
    {
        Id = id;
        EventId = eventId;
        EventType = eventType;
        TenantId = tenantId;
        ReceivedAtUtc = receivedAtUtc;
        RawPayload = rawPayload;
        IsPurged = isPurged;
        PurgedAtUtc = purgedAtUtc;
    }

    public Guid Id { get; private set; }
    public string EventId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public string? RawPayload { get; private set; }
    public bool IsPurged { get; private set; }
    public DateTimeOffset? PurgedAtUtc { get; private set; }

    public CarWashSaaS.Shared.Contracts.Result PurgePayload(DateTimeOffset purgedAtUtc)
    {
        if (IsPurged) return CarWashSaaS.Shared.Contracts.Result.Success();
        RawPayload = null;
        IsPurged = true;
        PurgedAtUtc = purgedAtUtc;
        return CarWashSaaS.Shared.Contracts.Result.Success();
    }
}
