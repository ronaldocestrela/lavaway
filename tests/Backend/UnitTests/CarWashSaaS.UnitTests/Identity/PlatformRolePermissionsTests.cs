using CarWashSaaS.Identity.Domain;

namespace CarWashSaaS.UnitTests.Identity;

public sealed class PlatformRolePermissionsTests
{
    [Fact]
    public void SuperAdmin_ShouldHaveAllPlatformPermissions()
    {
        var permissions = PlatformRolePermissions.GetPermissions(PlatformRole.SuperAdmin);

        Assert.Contains(PlatformPermission.ManageTenants, permissions);
        Assert.Contains(PlatformPermission.ImpersonateTenant, permissions);
        Assert.Contains(PlatformPermission.ManagePlatformBilling, permissions);
        Assert.Contains(PlatformPermission.ViewAuditLogs, permissions);
        Assert.Contains(PlatformPermission.ManagePlatformUsers, permissions);
        Assert.Contains(PlatformPermission.ViewSystemMetrics, permissions);
    }

    [Fact]
    public void PlatformSupport_ShouldHaveSupportPermissions_AndNotBillingOrUserManagement()
    {
        var permissions = PlatformRolePermissions.GetPermissions(PlatformRole.PlatformSupport);

        Assert.Contains(PlatformPermission.ManageTenants, permissions);
        Assert.Contains(PlatformPermission.ImpersonateTenant, permissions);
        Assert.Contains(PlatformPermission.ViewAuditLogs, permissions);
        Assert.Contains(PlatformPermission.ViewSystemMetrics, permissions);

        Assert.DoesNotContain(PlatformPermission.ManagePlatformBilling, permissions);
        Assert.DoesNotContain(PlatformPermission.ManagePlatformUsers, permissions);
    }

    [Fact]
    public void PlatformBillingAdmin_ShouldHaveBillingAndMetricsPermissions()
    {
        var permissions = PlatformRolePermissions.GetPermissions(PlatformRole.PlatformBillingAdmin);

        Assert.Contains(PlatformPermission.ManagePlatformBilling, permissions);
        Assert.Contains(PlatformPermission.ViewAuditLogs, permissions);
        Assert.Contains(PlatformPermission.ViewSystemMetrics, permissions);

        Assert.DoesNotContain(PlatformPermission.ImpersonateTenant, permissions);
        Assert.DoesNotContain(PlatformPermission.ManagePlatformUsers, permissions);
    }

    [Fact]
    public void PlatformAuditor_ShouldHaveOnlyAuditAndMetricsPermissions()
    {
        var permissions = PlatformRolePermissions.GetPermissions(PlatformRole.PlatformAuditor);

        Assert.Contains(PlatformPermission.ViewAuditLogs, permissions);
        Assert.Contains(PlatformPermission.ViewSystemMetrics, permissions);

        Assert.DoesNotContain(PlatformPermission.ManageTenants, permissions);
        Assert.DoesNotContain(PlatformPermission.ImpersonateTenant, permissions);
        Assert.DoesNotContain(PlatformPermission.ManagePlatformBilling, permissions);
        Assert.DoesNotContain(PlatformPermission.ManagePlatformUsers, permissions);
    }

    [Theory]
    [InlineData(PlatformRole.SuperAdmin, PlatformPermission.ManageTenants, true)]
    [InlineData(PlatformRole.SuperAdmin, PlatformPermission.ManagePlatformUsers, true)]
    [InlineData(PlatformRole.PlatformSupport, PlatformPermission.ImpersonateTenant, true)]
    [InlineData(PlatformRole.PlatformSupport, PlatformPermission.ManagePlatformBilling, false)]
    [InlineData(PlatformRole.PlatformAuditor, PlatformPermission.ViewAuditLogs, true)]
    [InlineData(PlatformRole.PlatformAuditor, PlatformPermission.ManageTenants, false)]
    public void HasPermission_ShouldReturnExpectedResult(PlatformRole role, PlatformPermission permission, bool expected)
    {
        var result = PlatformRolePermissions.HasPermission(role, permission);
        Assert.Equal(expected, result);
    }
}
