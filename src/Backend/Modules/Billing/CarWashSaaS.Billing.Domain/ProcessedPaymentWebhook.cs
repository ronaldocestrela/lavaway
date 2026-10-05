using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Domain;

public sealed class ProcessedPaymentWebhook : IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public string Provider { get; private set; } = string.Empty;
    public string EventId { get; private set; } = string.Empty;
    public string TxId { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public DateTimeOffset ProcessedAtUtc { get; private set; }
    public string? PayloadHash { get; private set; }
    public string? Notes { get; private set; }

    private ProcessedPaymentWebhook()
    {
    }

    public static Result<ProcessedPaymentWebhook> Create(
        Guid tenantId,
        string provider,
        string eventId,
        string txId,
        string status,
        string? payloadHash = null,
        string? notes = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<ProcessedPaymentWebhook>.Failure(new Error("webhook.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            return Result<ProcessedPaymentWebhook>.Failure(new Error("webhook.provider_required", "Provedor é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(eventId))
        {
            return Result<ProcessedPaymentWebhook>.Failure(new Error("webhook.event_id_required", "Identificador do evento é obrigatório.", ErrorType.Validation));
        }

        var entry = new ProcessedPaymentWebhook
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Provider = provider.Trim(),
            EventId = eventId.Trim(),
            TxId = txId?.Trim() ?? string.Empty,
            Status = string.IsNullOrWhiteSpace(status) ? "Processed" : status.Trim(),
            ReceivedAtUtc = DateTimeOffset.UtcNow,
            ProcessedAtUtc = DateTimeOffset.UtcNow,
            PayloadHash = string.IsNullOrWhiteSpace(payloadHash) ? null : payloadHash.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };

        return Result<ProcessedPaymentWebhook>.Success(entry);
    }
}
