using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.WhatsApp.Domain;

public enum WhatsAppIncidentType
{
    Disconnected = 1,
    HealthCheckFailed = 2,
    Reconnected = 3,
    ManualProbe = 4
}

public sealed class WhatsAppConnectionIncident : IMustHaveTenant
{
    private WhatsAppConnectionIncident()
    {
    }

    private WhatsAppConnectionIncident(
        Guid id,
        Guid tenantId,
        string providerSessionId,
        WhatsAppIncidentType type,
        string reason,
        bool alertDispatched,
        string? recipientEmail,
        DateTimeOffset occurredAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        ProviderSessionId = providerSessionId;
        Type = type;
        Reason = reason;
        AlertDispatched = alertDispatched;
        RecipientEmail = recipientEmail;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public string ProviderSessionId { get; private set; } = string.Empty;
    public WhatsAppIncidentType Type { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public bool AlertDispatched { get; private set; }
    public string? RecipientEmail { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static Result<WhatsAppConnectionIncident> Create(
        Guid tenantId,
        string providerSessionId,
        WhatsAppIncidentType type,
        string reason,
        bool alertDispatched = false,
        string? recipientEmail = null,
        DateTimeOffset? occurredAtUtc = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<WhatsAppConnectionIncident>.Failure(new Error("whatsapp.incident.tenant_required", "Tenant is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(providerSessionId))
        {
            return Result<WhatsAppConnectionIncident>.Failure(new Error("whatsapp.incident.session_required", "Provider session is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result<WhatsAppConnectionIncident>.Failure(new Error("whatsapp.incident.reason_required", "Reason is required.", ErrorType.Validation));
        }

        return Result<WhatsAppConnectionIncident>.Success(new WhatsAppConnectionIncident(
            Guid.CreateVersion7(),
            tenantId,
            providerSessionId.Trim(),
            type,
            reason.Trim(),
            alertDispatched,
            recipientEmail?.Trim(),
            occurredAtUtc ?? DateTimeOffset.UtcNow));
    }
}
