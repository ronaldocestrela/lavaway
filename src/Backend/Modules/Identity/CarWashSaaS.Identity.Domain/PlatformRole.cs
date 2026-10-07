namespace CarWashSaaS.Identity.Domain;

public enum PlatformRole
{
    SuperAdmin,
    PlatformSupport,
    PlatformBillingAdmin,
    PlatformAuditor
}

public enum PlatformPermission
{
    ManageTenants,
    ImpersonateTenant,
    ManagePlatformBilling,
    ViewAuditLogs,
    ManagePlatformUsers,
    ViewSystemMetrics
}

public static class PlatformAuthorizationPolicyNames
{
    public const string PlatformSuperAdmin = "PlatformSuperAdminPolicy";
    public const string PlatformSupport = "PlatformSupportPolicy";
    public const string PlatformBillingAdmin = "PlatformBillingAdminPolicy";
    public const string PlatformAuditor = "PlatformAuditorPolicy";
    public const string PlatformUser = "PlatformUserPolicy";
}

public static class PlatformRolePermissions
{
    public static IReadOnlyCollection<PlatformPermission> GetPermissions(PlatformRole role)
    {
        return role switch
        {
            PlatformRole.SuperAdmin =>
            [
                PlatformPermission.ManageTenants,
                PlatformPermission.ImpersonateTenant,
                PlatformPermission.ManagePlatformBilling,
                PlatformPermission.ViewAuditLogs,
                PlatformPermission.ManagePlatformUsers,
                PlatformPermission.ViewSystemMetrics
            ],
            PlatformRole.PlatformSupport =>
            [
                PlatformPermission.ManageTenants,
                PlatformPermission.ImpersonateTenant,
                PlatformPermission.ViewAuditLogs,
                PlatformPermission.ViewSystemMetrics
            ],
            PlatformRole.PlatformBillingAdmin =>
            [
                PlatformPermission.ManagePlatformBilling,
                PlatformPermission.ViewAuditLogs,
                PlatformPermission.ViewSystemMetrics
            ],
            PlatformRole.PlatformAuditor =>
            [
                PlatformPermission.ViewAuditLogs,
                PlatformPermission.ViewSystemMetrics
            ],
            _ => []
        };
    }

    public static bool HasPermission(PlatformRole role, PlatformPermission permission)
    {
        return GetPermissions(role).Contains(permission);
    }
}
