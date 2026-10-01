namespace CarWashSaaS.Shared.Contracts;

public sealed record TenantQueueMessage
{
    public TenantQueueMessage(Guid tenantId, string eventType, string payload)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("Event type is required.", nameof(eventType));
        }

        TenantId = tenantId;
        EventType = eventType.Trim();
        Payload = payload ?? string.Empty;
    }

    public Guid TenantId { get; }
    public string EventType { get; }
    public string Payload { get; }
}
