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
    ManageRoles,
    ManageWorkOrders,
    CreateWorkOrders,
    UpdateWorkOrderStatus,
    ViewCustomers,
    ManageCustomers,
    ManageCashier,
    ManageServices,
    ViewReports,
    ManageSubscriptions,
    ManageWhatsApp
}

public static class AuthorizationPolicyNames
{
    public const string Administrator = "AdministratorPolicy";
    public const string Receptionist = "ReceptionistPolicy";
    public const string Operator = "OperatorPolicy";
    public const string CreateWorkOrders = "CreateWorkOrdersPolicy";
    public const string ViewCustomers = "ViewCustomersPolicy";
    public const string UpdateWorkOrderStatus = "UpdateWorkOrderStatusPolicy";
    public const string ManageRoles = "ManageRolesPolicy";
    public const string ManageCashier = "ManageCashierPolicy";
    public const string ManageServices = "ManageServicesPolicy";
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
                ShopPermission.ManageRoles,
                ShopPermission.ManageWorkOrders,
                ShopPermission.CreateWorkOrders,
                ShopPermission.UpdateWorkOrderStatus,
                ShopPermission.ViewCustomers,
                ShopPermission.ManageCustomers,
                ShopPermission.ManageCashier,
                ShopPermission.ManageServices,
                ShopPermission.ViewReports,
                ShopPermission.ManageSubscriptions,
                ShopPermission.ManageWhatsApp
            ],
            ShopRole.Receptionist =>
            [
                ShopPermission.CreateWorkOrders,
                ShopPermission.UpdateWorkOrderStatus,
                ShopPermission.ViewCustomers,
                ShopPermission.ManageCustomers,
                ShopPermission.ManageCashier,
                ShopPermission.ManageSubscriptions,
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
