using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Identity.Domain;

public sealed class AdministrativeAuditEvent
{
    private AdministrativeAuditEvent()
    {
    }

    private AdministrativeAuditEvent(
        Guid id,
        DateTimeOffset timestampUtc,
        Guid actorId,
        string actorEmail,
        string actorRole,
        string actorRealm,
        string action,
        string targetType,
        string targetId,
        Guid? tenantId,
        string? ipAddress,
        string? userAgent,
        string detailsJson,
        string outcome,
        string? errorMessage)
    {
        Id = id;
        TimestampUtc = timestampUtc;
        ActorId = actorId;
        ActorEmail = actorEmail;
        ActorRole = actorRole;
        ActorRealm = actorRealm;
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        TenantId = tenantId;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        DetailsJson = detailsJson;
        Outcome = outcome;
        ErrorMessage = errorMessage;
    }

    public Guid Id { get; private set; }
    public DateTimeOffset TimestampUtc { get; private set; }
    public Guid ActorId { get; private set; }
    public string ActorEmail { get; private set; } = string.Empty;
    public string ActorRole { get; private set; } = string.Empty;
    public string ActorRealm { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public string TargetId { get; private set; } = string.Empty;
    public Guid? TenantId { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string DetailsJson { get; private set; } = "{}";
    public string Outcome { get; private set; } = "Success";
    public string? ErrorMessage { get; private set; }

    public static Result<AdministrativeAuditEvent> Create(
        Guid actorId,
        string actorEmail,
        string actorRole,
        string actorRealm,
        string action,
        string targetType,
        string targetId,
        Guid? tenantId = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? detailsJson = null,
        string outcome = "Success",
        string? errorMessage = null,
        Guid? id = null,
        DateTimeOffset? timestampUtc = null)
    {
        if (actorId == Guid.Empty)
        {
            return Result<AdministrativeAuditEvent>.Failure(new Error(
                "audit.actor_id.required",
                "Actor ID cannot be empty.",
                ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(actorEmail))
        {
            return Result<AdministrativeAuditEvent>.Failure(new Error(
                "audit.actor_email.required",
                "Actor email is required.",
                ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(actorRole))
        {
            return Result<AdministrativeAuditEvent>.Failure(new Error(
                "audit.actor_role.required",
                "Actor role is required.",
                ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            return Result<AdministrativeAuditEvent>.Failure(new Error(
                "audit.action.required",
                "Audit action is required.",
                ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(targetType))
        {
            return Result<AdministrativeAuditEvent>.Failure(new Error(
                "audit.target_type.required",
                "Target type is required.",
                ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(targetId))
        {
            return Result<AdministrativeAuditEvent>.Failure(new Error(
                "audit.target_id.required",
                "Target ID is required.",
                ErrorType.Validation));
        }

        var normalizedActorEmail = actorEmail.Trim().ToLowerInvariant();
        var normalizedActorRole = actorRole.Trim();
        var normalizedActorRealm = string.IsNullOrWhiteSpace(actorRealm) ? "Platform" : actorRealm.Trim();
        var normalizedAction = action.Trim();
        var normalizedTargetType = targetType.Trim();
        var normalizedTargetId = targetId.Trim();
        var normalizedOutcome = string.IsNullOrWhiteSpace(outcome) ? "Success" : outcome.Trim();
        var normalizedDetailsJson = string.IsNullOrWhiteSpace(detailsJson) ? "{}" : detailsJson.Trim();

        var eventId = id.HasValue && id.Value != Guid.Empty ? id.Value : Guid.CreateVersion7();
        var timestamp = timestampUtc ?? DateTimeOffset.UtcNow;

        return Result<AdministrativeAuditEvent>.Success(new AdministrativeAuditEvent(
            eventId,
            timestamp,
            actorId,
            normalizedActorEmail,
            normalizedActorRole,
            normalizedActorRealm,
            normalizedAction,
            normalizedTargetType,
            normalizedTargetId,
            tenantId,
            ipAddress?.Trim(),
            userAgent?.Trim(),
            normalizedDetailsJson,
            normalizedOutcome,
            errorMessage?.Trim()));
    }
}
