using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.UnitTests.Identity;

public sealed class CustomRoleDomainTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ShouldReturnSuccess()
    {
        var roleResult = CustomRole.Create(
            _tenantId,
            "Supervisor de Pátio",
            "Responsável pelo fluxo de veículos e checklist",
            [ShopPermission.CreateWorkOrders, ShopPermission.UpdateWorkOrderStatus, ShopPermission.ViewCustomers]);

        Assert.True(roleResult.IsSuccess);
        var role = roleResult.Value!;
        Assert.NotEqual(Guid.Empty, role.Id);
        Assert.Equal(_tenantId, role.TenantId);
        Assert.Equal("Supervisor de Pátio", role.Name);
        Assert.Equal("Responsável pelo fluxo de veículos e checklist", role.Description);
        Assert.False(role.IsSystemRole);
        Assert.Equal(3, role.Permissions.Count);
        Assert.Contains(ShopPermission.CreateWorkOrders, role.Permissions);
        Assert.Contains(ShopPermission.UpdateWorkOrderStatus, role.Permissions);
        Assert.Contains(ShopPermission.ViewCustomers, role.Permissions);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldReturnValidationFailure()
    {
        var result = CustomRole.Create(
            Guid.Empty,
            "Gerente",
            null,
            [ShopPermission.ConfigureStore]);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("role.tenant_id.required", result.Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidName_ShouldReturnValidationFailure(string? name)
    {
        var result = CustomRole.Create(
            _tenantId,
            name!,
            null,
            [ShopPermission.ConfigureStore]);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("role.name.required", result.Error.Code);
    }

    [Fact]
    public void Create_WithNameExceeding100Chars_ShouldReturnValidationFailure()
    {
        var longName = new string('A', 101);
        var result = CustomRole.Create(
            _tenantId,
            longName,
            null,
            [ShopPermission.ConfigureStore]);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("role.name.too_long", result.Error.Code);
    }

    [Fact]
    public void Create_WithNoPermissions_ShouldReturnValidationFailure()
    {
        var result = CustomRole.Create(
            _tenantId,
            "Vendedor",
            null,
            []);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("role.permissions.required", result.Error.Code);
    }

    [Fact]
    public void Update_CustomRoleWithValidData_ShouldSucceed()
    {
        var role = CustomRole.Create(
            _tenantId,
            "Supervisor",
            "Descrição inicial",
            [ShopPermission.ViewCustomers]).Value!;

        var updateResult = role.Update(
            "Supervisor Geral",
            "Nova descrição",
            [ShopPermission.ViewCustomers, ShopPermission.ManageWorkOrders]);

        Assert.True(updateResult.IsSuccess);
        Assert.Equal("Supervisor Geral", role.Name);
        Assert.Equal("Nova descrição", role.Description);
        Assert.Equal(2, role.Permissions.Count);
        Assert.Contains(ShopPermission.ManageWorkOrders, role.Permissions);
        Assert.NotNull(role.UpdatedAtUtc);
    }

    [Fact]
    public void Update_SystemRole_ShouldReturnBusinessFailure()
    {
        var systemRole = CustomRole.Create(
            _tenantId,
            "Administrator",
            "Perfil do sistema",
            [ShopPermission.ConfigureStore],
            isSystemRole: true).Value!;

        var updateResult = systemRole.Update("Admin Renomeado", "Desc", [ShopPermission.ConfigureStore]);

        Assert.False(updateResult.IsSuccess);
        Assert.Equal(ErrorType.Validation, updateResult.Error!.Type);
        Assert.Equal("role.system_role.immutable", updateResult.Error.Code);
    }

    [Fact]
    public void CanDelete_SystemRole_ShouldReturnFalse()
    {
        var systemRole = CustomRole.Create(
            _tenantId,
            "Administrator",
            "Perfil do sistema",
            [ShopPermission.ConfigureStore],
            isSystemRole: true).Value!;

        Assert.False(systemRole.CanDelete());
    }

    [Fact]
    public void CanDelete_CustomRole_ShouldReturnTrue()
    {
        var customRole = CustomRole.Create(
            _tenantId,
            "Polidor Especialista",
            null,
            [ShopPermission.UpdateWorkOrderStatus]).Value!;

        Assert.True(customRole.CanDelete());
    }
}
