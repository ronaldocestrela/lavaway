using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.UnitTests.Identity;

public sealed class CustomRoleApplicationServiceTests
{
    private readonly FakeCustomRoleRepository _roleRepository = new();
    private readonly CustomRoleApplicationService _service;
    private readonly Guid _tenantId = Guid.NewGuid();

    public CustomRoleApplicationServiceTests()
    {
        _service = new CustomRoleApplicationService(_roleRepository);
    }

    [Fact]
    public async Task CreateRoleAsync_WithValidData_ShouldSucceed()
    {
        var request = new CreateCustomRoleRequest(
            "Líder de Estética",
            "Supervisão de polimentos e vitrificações",
            ["CreateWorkOrders", "UpdateWorkOrderStatus"]);

        var result = await _service.CreateRoleAsync(_tenantId, request);

        Assert.True(result.IsSuccess);
        Assert.Equal("Líder de Estética", result.Value!.Name);
        Assert.Equal(2, result.Value.Permissions.Count);
        Assert.False(result.Value.IsSystemRole);
    }

    [Fact]
    public async Task CreateRoleAsync_WithDuplicateNameInSameTenant_ShouldReturnConflict()
    {
        var existingRole = CustomRole.Create(
            _tenantId,
            "Gerente",
            null,
            [ShopPermission.ConfigureStore]).Value!;
        await _roleRepository.AddAsync(existingRole);

        var request = new CreateCustomRoleRequest(
            "Gerente",
            "Outro gerente",
            ["ViewReports"]);

        var result = await _service.CreateRoleAsync(_tenantId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("role.name.duplicate", result.Error.Code);
    }

    [Fact]
    public async Task CreateRoleAsync_WithSameNameInDifferentTenant_ShouldSucceed()
    {
        var otherTenantId = Guid.NewGuid();
        var existingRole = CustomRole.Create(
            otherTenantId,
            "Gerente",
            null,
            [ShopPermission.ConfigureStore]).Value!;
        await _roleRepository.AddAsync(existingRole);

        var request = new CreateCustomRoleRequest(
            "Gerente",
            "Gerente do tenant local",
            ["ViewReports"]);

        var result = await _service.CreateRoleAsync(_tenantId, request);

        Assert.True(result.IsSuccess);
        Assert.Equal("Gerente", result.Value!.Name);
    }

    [Fact]
    public async Task CreateRoleAsync_WithInvalidPermission_ShouldReturnValidationFailure()
    {
        var request = new CreateCustomRoleRequest(
            "Cargo Inválido",
            null,
            ["NonExistentPermission"]);

        var result = await _service.CreateRoleAsync(_tenantId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("role.permission.invalid", result.Error.Code);
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenRoleNotFound_ShouldReturnNotFound()
    {
        var request = new UpdateCustomRoleRequest("Novo Nome", null, ["ViewCustomers"]);

        var result = await _service.UpdateRoleAsync(_tenantId, Guid.NewGuid(), request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenRoleIsSystem_ShouldReturnValidationFailure()
    {
        var systemRole = CustomRole.Create(
            _tenantId,
            "Administrator",
            "Sistema",
            [ShopPermission.ConfigureStore],
            isSystemRole: true).Value!;
        await _roleRepository.AddAsync(systemRole);

        var request = new UpdateCustomRoleRequest("Admin Modificado", null, ["ViewCustomers"]);

        var result = await _service.UpdateRoleAsync(_tenantId, systemRole.Id, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("role.system_role.immutable", result.Error.Code);
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenRenamingToExistingNameInTenant_ShouldReturnConflict()
    {
        var role1 = CustomRole.Create(_tenantId, "Cargo A", null, [ShopPermission.ViewCustomers]).Value!;
        var role2 = CustomRole.Create(_tenantId, "Cargo B", null, [ShopPermission.ViewCustomers]).Value!;
        await _roleRepository.AddAsync(role1);
        await _roleRepository.AddAsync(role2);

        var request = new UpdateCustomRoleRequest("Cargo B", null, ["ViewCustomers"]);

        var result = await _service.UpdateRoleAsync(_tenantId, role1.Id, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
    }

    [Fact]
    public async Task DeleteRoleAsync_WhenSystemRole_ShouldReturnValidationFailure()
    {
        var systemRole = CustomRole.Create(_tenantId, "Administrator", null, [ShopPermission.ConfigureStore], isSystemRole: true).Value!;
        await _roleRepository.AddAsync(systemRole);

        var result = await _service.DeleteRoleAsync(_tenantId, systemRole.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("role.system_role.immutable", result.Error.Code);
    }

    [Fact]
    public async Task DeleteRoleAsync_WhenHasAssignedUsers_ShouldReturnConflict()
    {
        var customRole = CustomRole.Create(_tenantId, "Caixa Líder", null, [ShopPermission.ManageCashier]).Value!;
        await _roleRepository.AddAsync(customRole);
        _roleRepository.RolesWithUsers.Add(customRole.Id);

        var result = await _service.DeleteRoleAsync(_tenantId, customRole.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("role.in_use", result.Error.Code);
    }

    [Fact]
    public async Task DeleteRoleAsync_WhenValidAndUnused_ShouldSucceed()
    {
        var customRole = CustomRole.Create(_tenantId, "Temporário", null, [ShopPermission.ViewCustomers]).Value!;
        await _roleRepository.AddAsync(customRole);

        var result = await _service.DeleteRoleAsync(_tenantId, customRole.Id);

        Assert.True(result.IsSuccess);
        var remaining = await _roleRepository.GetByIdAsync(_tenantId, customRole.Id);
        Assert.Null(remaining);
    }

    [Fact]
    public async Task ListRolesAsync_ShouldReturnRolesWithUserCounts()
    {
        var role1 = CustomRole.Create(_tenantId, "Admin", null, [ShopPermission.ConfigureStore], isSystemRole: true).Value!;
        var role2 = CustomRole.Create(_tenantId, "Lavador", null, [ShopPermission.UpdateWorkOrderStatus]).Value!;
        await _roleRepository.AddAsync(role1);
        await _roleRepository.AddAsync(role2);
        _roleRepository.UserCounts[role1.Id] = 2;
        _roleRepository.UserCounts[role2.Id] = 5;

        var result = await _service.ListRolesAsync(_tenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        var adminDto = result.Value.First(r => r.Id == role1.Id);
        Assert.Equal(2, adminDto.AssignedUsersCount);
        Assert.True(adminDto.IsSystemRole);
    }

    [Fact]
    public void ListAvailablePermissions_ShouldReturnCategorizedPermissions()
    {
        var permissionsResult = _service.ListAvailablePermissions();

        Assert.True(permissionsResult.IsSuccess);
        var permissions = permissionsResult.Value!;
        Assert.NotEmpty(permissions);
        Assert.Contains(permissions, p => p.Code == "CreateWorkOrders" && p.Category == "Ordens de Serviço");
        Assert.Contains(permissions, p => p.Code == "ManageCashier" && p.Category == "Financeiro & Caixa");
        Assert.Contains(permissions, p => p.Code == "ConfigureStore" && p.Category == "Configurações & Loja");
    }

    private sealed class FakeCustomRoleRepository : ICustomRoleRepository
    {
        public List<CustomRole> Roles { get; } = [];
        public HashSet<Guid> RolesWithUsers { get; } = [];
        public Dictionary<Guid, int> UserCounts { get; } = [];

        public Task<CustomRole?> GetByIdAsync(Guid tenantId, Guid roleId, CancellationToken ct = default)
        {
            var role = Roles.FirstOrDefault(r => r.TenantId == tenantId && r.Id == roleId);
            return Task.FromResult(role);
        }

        public Task<CustomRole?> GetByNameAsync(Guid tenantId, string name, CancellationToken ct = default)
        {
            var role = Roles.FirstOrDefault(r => r.TenantId == tenantId && string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(role);
        }

        public Task<IReadOnlyCollection<CustomRole>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        {
            var list = Roles.Where(r => r.TenantId == tenantId).ToArray();
            return Task.FromResult<IReadOnlyCollection<CustomRole>>(list);
        }

        public Task<Dictionary<Guid, int>> CountUsersPerRoleAsync(Guid tenantId, CancellationToken ct = default)
        {
            return Task.FromResult(new Dictionary<Guid, int>(UserCounts));
        }

        public Task<bool> HasAssignedUsersAsync(Guid tenantId, Guid roleId, CancellationToken ct = default)
        {
            var has = RolesWithUsers.Contains(roleId) || (UserCounts.TryGetValue(roleId, out var count) && count > 0);
            return Task.FromResult(has);
        }

        public Task AddAsync(CustomRole role, CancellationToken ct = default)
        {
            Roles.Add(role);
            return Task.CompletedTask;
        }

        public void Update(CustomRole role)
        {
        }

        public void Remove(CustomRole role)
        {
            Roles.Remove(role);
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }
}
