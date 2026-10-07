namespace CarWashSaaS.Billing.Domain;

public sealed class ProcessedSaasWebhookEvent
{
    private ProcessedSaasWebhookEvent()
    {
    }

    public ProcessedSaasWebhookEvent(Guid id, string eventId, string eventType, Guid tenantId, DateTimeOffset receivedAtUtc)
    {
        Id = id;
        EventId = eventId;
        EventType = eventType;
        TenantId = tenantId;
        ReceivedAtUtc = receivedAtUtc;
    }

    public Guid Id { get; private set; }
    public string EventId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public DateTimeOffset ReceivedAtUtc { get; private set; }
}
