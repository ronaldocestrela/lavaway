using CarWashSaaS.Identity.Domain;

namespace CarWashSaaS.UnitTests.Identity;

public sealed class ShopRolePermissionsTests
{
    [Fact]
    public void Administrator_ShouldHaveFullAccess()
    {
        var permissions = ShopRolePermissions.GetPermissions(ShopRole.Administrator);

        Assert.Contains(ShopPermission.ConfigureStore, permissions);
        Assert.Contains(ShopPermission.ManageUsers, permissions);
        Assert.Contains(ShopPermission.ManageWorkOrders, permissions);
        Assert.Contains(ShopPermission.ViewReports, permissions);
    }

    [Fact]
    public void Receptionist_ShouldHaveOperationalAccess_ButNotStoreConfiguration()
    {
        var permissions = ShopRolePermissions.GetPermissions(ShopRole.Receptionist);

        Assert.Contains(ShopPermission.CreateWorkOrders, permissions);
        Assert.Contains(ShopPermission.ViewCustomers, permissions);
        Assert.DoesNotContain(ShopPermission.ConfigureStore, permissions);
        Assert.DoesNotContain(ShopPermission.ManageUsers, permissions);
    }

    [Fact]
    public void Operator_ShouldNotManageUsers_OrStoreConfiguration()
    {
        var permissions = ShopRolePermissions.GetPermissions(ShopRole.Operator);

        Assert.Contains(ShopPermission.UpdateWorkOrderStatus, permissions);
        Assert.DoesNotContain(ShopPermission.ManageUsers, permissions);
        Assert.DoesNotContain(ShopPermission.ConfigureStore, permissions);
    }
}
