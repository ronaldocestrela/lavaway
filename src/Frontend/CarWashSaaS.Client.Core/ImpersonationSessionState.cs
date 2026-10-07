namespace CarWashSaaS.Client.Core;

public sealed class ImpersonationSessionState
{
    public bool IsActive { get; private set; }
    public Guid? TenantId { get; private set; }
    public string? TenantName { get; private set; }
    public string? OperatorEmail { get; private set; }
    public string? Reason { get; private set; }
    public string? TicketReference { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }

    public event Action? OnChange;

    public void Start(
        Guid tenantId,
        string tenantName,
        string operatorEmail,
        string reason,
        string? ticketReference,
        DateTimeOffset startedAtUtc)
    {
        IsActive = true;
        TenantId = tenantId;
        TenantName = tenantName;
        OperatorEmail = operatorEmail;
        Reason = reason;
        TicketReference = ticketReference;
        StartedAtUtc = startedAtUtc;

        NotifyStateChanged();
    }

    public void End()
    {
        IsActive = false;
        TenantId = null;
        TenantName = null;
        OperatorEmail = null;
        Reason = null;
        TicketReference = null;
        StartedAtUtc = null;

        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
