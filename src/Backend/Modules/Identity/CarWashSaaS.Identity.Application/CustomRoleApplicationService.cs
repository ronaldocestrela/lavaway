using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Identity.Application;

public sealed class CustomRoleApplicationService(ICustomRoleRepository roleRepository)
{
    private static readonly IReadOnlyList<PermissionDefinitionDto> AvailablePermissions =
    [
        // Recepção & Ordens
        new("CreateWorkOrders", "Criar Ordens de Serviço", "Permite realizar check-in e abrir novas ordens de serviço", "Ordens de Serviço"),
        new("ManageWorkOrders", "Gerenciar Ordens de Serviço", "Permite cancelar ordens, editar detalhes e alterar histórico", "Ordens de Serviço"),
        new("UpdateWorkOrderStatus", "Atualizar Status & Checklist", "Permite mover veículos no Kanban, preencher checklists e finalizar", "Pátio & Operações"),

        // Clientes
        new("ViewCustomers", "Visualizar Clientes", "Permite consultar clientes, veículos e histórico de visitas", "Clientes & Veículos"),
        new("ManageCustomers", "Cadastrar & Editar Clientes", "Permite criar, alterar e excluir cadastros de clientes e veículos", "Clientes & Veículos"),

        // Caixa & Financeiro
        new("ManageCashier", "Operar Caixa & Pagamentos", "Permite abrir/fechar caixa, registrar recebimentos e visualizar sangrias", "Financeiro & Caixa"),
        new("ViewReports", "Visualizar Relatórios", "Permite acessar métricas de faturamento, desempenho e comissões", "Financeiro & Caixa"),

        // Serviços & Assinaturas
        new("ManageServices", "Gerenciar Serviços & Preços", "Permite criar e alterar preços da tabela de serviços e pacotes", "Serviços & Catálogo"),
        new("ManageSubscriptions", "Gerenciar Assinaturas", "Permite gerenciar planos de clubes de lavagem recorrentes", "Serviços & Catálogo"),

        // Loja & Configurações
        new("ConfigureStore", "Configurar Estabelecimento", "Permite alterar horários, dados da loja e capacidade de boxes", "Configurações & Loja"),
        new("ManageUsers", "Gerenciar Usuários", "Permite criar e gerenciar acessos de colaboradores", "Configurações & Loja"),
        new("ManageRoles", "Gerenciar Perfis de Acesso", "Permite criar, personalizar e excluir perfis de acesso e permissões", "Configurações & Loja"),
        new("ManageWhatsApp", "Gerenciar Conexão WhatsApp", "Permite parear instância do WhatsApp e gerenciar mensagens", "Comunicação & Mensagens")
    ];

    public Result<IReadOnlyList<PermissionDefinitionDto>> ListAvailablePermissions() =>
        Result<IReadOnlyList<PermissionDefinitionDto>>.Success(AvailablePermissions);

    public async Task<Result<CustomRoleDto>> CreateRoleAsync(
        Guid tenantId,
        CreateCustomRoleRequest request,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CustomRoleDto>.Failure(new Error("role.tenant_id.required", "TenantId is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<CustomRoleDto>.Failure(new Error("role.name.required", "Role name is required.", ErrorType.Validation));
        }

        var normalizedName = request.Name.Trim();
        var existingRole = await roleRepository.GetByNameAsync(tenantId, normalizedName, ct);
        if (existingRole is not null)
        {
            return Result<CustomRoleDto>.Failure(new Error("role.name.duplicate", $"A role named '{normalizedName}' already exists in this tenant.", ErrorType.Conflict));
        }

        var parsedPermissionsResult = ParsePermissions(request.Permissions);
        if (!parsedPermissionsResult.IsSuccess)
        {
            return Result<CustomRoleDto>.Failure(parsedPermissionsResult.Error!);
        }

        var roleResult = CustomRole.Create(tenantId, normalizedName, request.Description, parsedPermissionsResult.Value!);
        if (!roleResult.IsSuccess)
        {
            return Result<CustomRoleDto>.Failure(roleResult.Error!);
        }

        var role = roleResult.Value!;
        await roleRepository.AddAsync(role, ct);
        await roleRepository.SaveChangesAsync(ct);

        return Result<CustomRoleDto>.Success(MapToDto(role, 0));
    }

    public async Task<Result<CustomRoleDto>> UpdateRoleAsync(
        Guid tenantId,
        Guid roleId,
        UpdateCustomRoleRequest request,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || roleId == Guid.Empty)
        {
            return Result<CustomRoleDto>.Failure(new Error("role.invalid_parameters", "TenantId and RoleId are required.", ErrorType.Validation));
        }

        var role = await roleRepository.GetByIdAsync(tenantId, roleId, ct);
        if (role is null)
        {
            return Result<CustomRoleDto>.Failure(new Error("role.not_found", "Role was not found.", ErrorType.NotFound));
        }

        if (role.IsSystemRole)
        {
            return Result<CustomRoleDto>.Failure(new Error("role.system_role.immutable", "System roles cannot be modified.", ErrorType.Validation));
        }

        var normalizedName = request.Name.Trim();
        if (!string.Equals(role.Name, normalizedName, StringComparison.OrdinalIgnoreCase))
        {
            var conflict = await roleRepository.GetByNameAsync(tenantId, normalizedName, ct);
            if (conflict is not null && conflict.Id != roleId)
            {
                return Result<CustomRoleDto>.Failure(new Error("role.name.duplicate", $"A role named '{normalizedName}' already exists in this tenant.", ErrorType.Conflict));
            }
        }

        var parsedPermissionsResult = ParsePermissions(request.Permissions);
        if (!parsedPermissionsResult.IsSuccess)
        {
            return Result<CustomRoleDto>.Failure(parsedPermissionsResult.Error!);
        }

        var updateResult = role.Update(normalizedName, request.Description, parsedPermissionsResult.Value!);
        if (!updateResult.IsSuccess)
        {
            return Result<CustomRoleDto>.Failure(updateResult.Error!);
        }

        roleRepository.Update(role);
        await roleRepository.SaveChangesAsync(ct);

        var counts = await roleRepository.CountUsersPerRoleAsync(tenantId, ct);
        var userCount = counts.GetValueOrDefault(role.Id, 0);

        return Result<CustomRoleDto>.Success(MapToDto(role, userCount));
    }

    public async Task<Result> DeleteRoleAsync(
        Guid tenantId,
        Guid roleId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || roleId == Guid.Empty)
        {
            return Result.Failure(new Error("role.invalid_parameters", "TenantId and RoleId are required.", ErrorType.Validation));
        }

        var role = await roleRepository.GetByIdAsync(tenantId, roleId, ct);
        if (role is null)
        {
            return Result.Failure(new Error("role.not_found", "Role was not found.", ErrorType.NotFound));
        }

        if (!role.CanDelete())
        {
            return Result.Failure(new Error("role.system_role.immutable", "System roles cannot be deleted.", ErrorType.Validation));
        }

        var hasUsers = await roleRepository.HasAssignedUsersAsync(tenantId, roleId, ct);
        if (hasUsers)
        {
            return Result.Failure(new Error("role.in_use", "Não é possível excluir este perfil pois existem colaboradores vinculados a ele.", ErrorType.Conflict));
        }

        roleRepository.Remove(role);
        await roleRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<CustomRoleDto>>> ListRolesAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyList<CustomRoleDto>>.Failure(new Error("role.tenant_id.required", "TenantId is required.", ErrorType.Validation));
        }

        var roles = await roleRepository.ListByTenantAsync(tenantId, ct);
        var counts = await roleRepository.CountUsersPerRoleAsync(tenantId, ct);

        var dtos = roles
            .Select(r => MapToDto(r, counts.GetValueOrDefault(r.Id, 0)))
            .OrderBy(r => !r.IsSystemRole)
            .ThenBy(r => r.Name)
            .ToList();

        return Result<IReadOnlyList<CustomRoleDto>>.Success(dtos);
    }

    public async Task<Result<CustomRoleDto>> GetRoleByIdAsync(
        Guid tenantId,
        Guid roleId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || roleId == Guid.Empty)
        {
            return Result<CustomRoleDto>.Failure(new Error("role.invalid_parameters", "TenantId and RoleId are required.", ErrorType.Validation));
        }

        var role = await roleRepository.GetByIdAsync(tenantId, roleId, ct);
        if (role is null)
        {
            return Result<CustomRoleDto>.Failure(new Error("role.not_found", "Role was not found.", ErrorType.NotFound));
        }

        var counts = await roleRepository.CountUsersPerRoleAsync(tenantId, ct);
        return Result<CustomRoleDto>.Success(MapToDto(role, counts.GetValueOrDefault(role.Id, 0)));
    }

    public async Task<Result> EnsureDefaultRolesAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure(new Error("role.tenant_id.required", "TenantId is required.", ErrorType.Validation));
        }

        var existingRoles = await roleRepository.ListByTenantAsync(tenantId, ct);
        var existingNames = new HashSet<string>(existingRoles.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);

        var defaults = new[]
        {
            (Name: ShopRole.Administrator.ToString(), Description: "Acesso total à administração, configurações e financeiro", Role: ShopRole.Administrator),
            (Name: ShopRole.Receptionist.ToString(), Description: "Operação de caixa, abertura de ordens e atendimento ao cliente", Role: ShopRole.Receptionist),
            (Name: ShopRole.Operator.ToString(), Description: "Acompanhamento do pátio, checklists e atualização de serviços", Role: ShopRole.Operator)
        };

        foreach (var (name, desc, role) in defaults)
        {
            if (!existingNames.Contains(name))
            {
                var perms = ShopRolePermissions.GetPermissions(role);
                var created = CustomRole.Create(tenantId, name, desc, perms, isSystemRole: true);
                if (created.IsSuccess)
                {
                    await roleRepository.AddAsync(created.Value!, ct);
                }
            }
        }

        await roleRepository.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static Result<List<ShopPermission>> ParsePermissions(IEnumerable<string>? permissions)
    {
        if (permissions is null)
        {
            return Result<List<ShopPermission>>.Failure(new Error("role.permissions.required", "Permissions are required.", ErrorType.Validation));
        }

        var list = new List<ShopPermission>();
        foreach (var permStr in permissions)
        {
            if (Enum.TryParse<ShopPermission>(permStr, true, out var perm))
            {
                list.Add(perm);
            }
            else
            {
                return Result<List<ShopPermission>>.Failure(new Error("role.permission.invalid", $"Permission '{permStr}' is invalid.", ErrorType.Validation));
            }
        }

        if (list.Count == 0)
        {
            return Result<List<ShopPermission>>.Failure(new Error("role.permissions.required", "At least one permission must be assigned.", ErrorType.Validation));
        }

        return Result<List<ShopPermission>>.Success(list);
    }

    private static CustomRoleDto MapToDto(CustomRole role, int assignedCount)
    {
        return new CustomRoleDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystemRole,
            role.Permissions.Select(p => p.ToString()).ToList(),
            assignedCount);
    }
}
