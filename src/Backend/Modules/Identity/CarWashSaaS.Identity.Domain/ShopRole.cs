namespace CarWashSaaS.Identity.Domain;

public enum ShopRole
{
    Administrator,
    Receptionist,
    Operator
}

public enum ShopPermission
{
    ConfigureStore,
    ManageUsers,
    ManageWorkOrders,
    CreateWorkOrders,
    UpdateWorkOrderStatus,
    ViewCustomers,
    ViewReports
}

public static class AuthorizationPolicyNames
{
    public const string Administrator = "AdministratorPolicy";
    public const string Receptionist = "ReceptionistPolicy";
    public const string Operator = "OperatorPolicy";
    public const string CreateWorkOrders = "CreateWorkOrdersPolicy";
    public const string ViewCustomers = "ViewCustomersPolicy";
}

public static class ShopRolePermissions
{
    public static IReadOnlyCollection<ShopPermission> GetPermissions(ShopRole role)
    {
        return role switch
        {
            ShopRole.Administrator =>
            [
                ShopPermission.ConfigureStore,
                ShopPermission.ManageUsers,
                ShopPermission.ManageWorkOrders,
                ShopPermission.CreateWorkOrders,
                ShopPermission.UpdateWorkOrderStatus,
                ShopPermission.ViewCustomers,
                ShopPermission.ViewReports
            ],
            ShopRole.Receptionist =>
            [
                ShopPermission.CreateWorkOrders,
                ShopPermission.UpdateWorkOrderStatus,
                ShopPermission.ViewCustomers,
                ShopPermission.ViewReports
            ],
            ShopRole.Operator =>
            [
                ShopPermission.UpdateWorkOrderStatus,
                ShopPermission.ViewCustomers,
                ShopPermission.ViewReports
            ],
            _ => []
        };
    }

    public static bool HasPermission(ShopRole role, ShopPermission permission)
    {
        return GetPermissions(role).Contains(permission);
    }
}