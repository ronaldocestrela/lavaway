namespace CarWashSaaS.Shared.Contracts;

public sealed record AuditEventDto(
    Guid Id,
    DateTimeOffset TimestampUtc,
    Guid ActorId,
    string ActorEmail,
    string ActorRole,
    string ActorRealm,
    string Action,
    string TargetType,
    string TargetId,
    Guid? TenantId,
    string? IpAddress,
    string? UserAgent,
    string DetailsJson,
    string Outcome,
    string? ErrorMessage);

public sealed record RecordAuditEventRequest(
    Guid ActorId,
    string ActorEmail,
    string ActorRole,
    string ActorRealm,
    string Action,
    string TargetType,
    string TargetId,
    Guid? TenantId = null,
    string? IpAddress = null,
    string? UserAgent = null,
    string? DetailsJson = null,
    string Outcome = "Success",
    string? ErrorMessage = null);

public sealed record AuditQueryFilter(
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    Guid? ActorId = null,
    string? ActorEmail = null,
    string? Action = null,
    string? TargetType = null,
    string? TargetId = null,
    Guid? TenantId = null,
    string? Outcome = null,
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 20);

public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public static class PlatformRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string PlatformAuditor = "PlatformAuditor";
    public const string PlatformSupport = "PlatformSupport";
    public const string PlatformBillingAdmin = "PlatformBillingAdmin";
}

public static class PlatformActionConstants
{
    public const string AuthLoginSuccess = "Auth.PlatformLoginSuccess";
    public const string AuthLoginFailed = "Auth.PlatformLoginFailed";
    public const string PlatformUserCreated = "PlatformUser.Created";
    public const string PlatformUserRoleChanged = "PlatformUser.RoleChanged";
    public const string PlatformUserDeactivated = "PlatformUser.Deactivated";
    public const string TenantCreated = "Tenant.Created";
    public const string TenantStatusChanged = "Tenant.StatusChanged";
    public const string TenantImpersonated = "Tenant.Impersonated";
    public const string BillingPlanModified = "BillingPlan.Modified";
    public const string SystemSettingUpdated = "System.SettingUpdated";
    public const string SensitiveDataExported = "Security.SensitiveDataExported";
}

public static class PlatformTargetTypeConstants
{
    public const string PlatformUser = "PlatformUser";
    public const string Tenant = "Tenant";
    public const string ShopUser = "ShopUser";
    public const string BillingPlan = "BillingPlan";
    public const string System = "System";
}

public sealed record PlatformLoginRequest(string Email, string Password);

public sealed record PlatformUserSummaryDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastLoginAtUtc);

public sealed record CreatePlatformUserRequest(
    string Email,
    string Password,
    string FullName,
    string Role);
