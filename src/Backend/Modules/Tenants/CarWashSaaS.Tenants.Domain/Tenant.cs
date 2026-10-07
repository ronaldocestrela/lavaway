using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Tenants.Domain;

public sealed class Tenant
{
    private Tenant()
    {
    }

    private Tenant(Guid id, string name, TenantStatus status = TenantStatus.Trial, DateTimeOffset? trialEndsAtUtc = null)
    {
        Id = id;
        Name = name;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        Status = status;
        StatusChangedAtUtc = CreatedAtUtc;
        TrialEndsAtUtc = trialEndsAtUtc ?? (status == TenantStatus.Trial ? CreatedAtUtc.AddDays(14) : null);
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public TenantStatus Status { get; private set; } = TenantStatus.Trial;
    public DateTimeOffset StatusChangedAtUtc { get; private set; }
    public DateTimeOffset? TrialEndsAtUtc { get; private set; }
    public string? StatusReason { get; private set; }

    public static Result<Tenant> Create(string name, TenantStatus status = TenantStatus.Trial, DateTimeOffset? trialEndsAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<Tenant>.Failure(new Error("tenant.name.required", "Tenant name is required.", ErrorType.Validation));
        }

        var normalizedName = name.Trim();
        if (normalizedName.Length > 200)
        {
            return Result<Tenant>.Failure(new Error("tenant.name.too_long", "Tenant name cannot exceed 200 characters.", ErrorType.Validation));
        }

        return Result<Tenant>.Success(new Tenant(Guid.CreateVersion7(), normalizedName, status, trialEndsAtUtc));
    }

    public static Result<Tenant> Create(Guid id, string name, TenantStatus status = TenantStatus.Trial, DateTimeOffset? trialEndsAtUtc = null)
    {
        if (id == Guid.Empty)
        {
            return Result<Tenant>.Failure(new Error("tenant.id.required", "Tenant ID cannot be empty.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<Tenant>.Failure(new Error("tenant.name.required", "Tenant name is required.", ErrorType.Validation));
        }

        var normalizedName = name.Trim();
        if (normalizedName.Length > 200)
        {
            return Result<Tenant>.Failure(new Error("tenant.name.too_long", "Tenant name cannot exceed 200 characters.", ErrorType.Validation));
        }

        return Result<Tenant>.Success(new Tenant(id, normalizedName, status, trialEndsAtUtc));
    }

    public Result Activate(string? reason = null)
    {
        Status = TenantStatus.Active;
        StatusReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        StatusChangedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result StartTrial(DateTimeOffset trialEndsAt, string? reason = null)
    {
        Status = TenantStatus.Trial;
        TrialEndsAtUtc = trialEndsAt;
        StatusReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        StatusChangedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result MarkDelinquent(string? reason = null)
    {
        Status = TenantStatus.Delinquent;
        StatusReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        StatusChangedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result Cancel(string? reason = null)
    {
        Status = TenantStatus.Canceled;
        StatusReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        StatusChangedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result ChangeStatus(TenantStatus newStatus, string? reason = null, DateTimeOffset? trialEndsAt = null)
    {
        Status = newStatus;
        if (newStatus == TenantStatus.Trial && trialEndsAt.HasValue)
        {
            TrialEndsAtUtc = trialEndsAt.Value;
        }
        StatusReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        StatusChangedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }
}
