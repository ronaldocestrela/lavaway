using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Identity.Domain;

public sealed class CustomRole : IMustHaveTenant
{
    private readonly List<ShopPermission> _permissions = [];

    private CustomRole()
    {
    }

    private CustomRole(
        Guid id,
        Guid tenantId,
        string name,
        string? description,
        IEnumerable<ShopPermission> permissions,
        bool isSystemRole,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Description = description;
        _permissions.AddRange(permissions.Distinct());
        IsSystemRole = isSystemRole;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsSystemRole { get; private set; }
    public IReadOnlyCollection<ShopPermission> Permissions => _permissions.AsReadOnly();
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public static Result<CustomRole> Create(
        Guid tenantId,
        string name,
        string? description,
        IEnumerable<ShopPermission> permissions,
        bool isSystemRole = false)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CustomRole>.Failure(new Error("role.tenant_id.required", "TenantId is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<CustomRole>.Failure(new Error("role.name.required", "Role name is required.", ErrorType.Validation));
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length > 100)
        {
            return Result<CustomRole>.Failure(new Error("role.name.too_long", "Role name cannot exceed 100 characters.", ErrorType.Validation));
        }

        var distinctPermissions = permissions?.Distinct().ToList() ?? [];
        if (distinctPermissions.Count == 0)
        {
            return Result<CustomRole>.Failure(new Error("role.permissions.required", "At least one permission must be assigned.", ErrorType.Validation));
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription?.Length > 500)
        {
            return Result<CustomRole>.Failure(new Error("role.description.too_long", "Role description cannot exceed 500 characters.", ErrorType.Validation));
        }

        var role = new CustomRole(
            Guid.CreateVersion7(),
            tenantId,
            trimmedName,
            trimmedDescription,
            distinctPermissions,
            isSystemRole,
            DateTimeOffset.UtcNow);

        return Result<CustomRole>.Success(role);
    }

    public Result Update(string name, string? description, IEnumerable<ShopPermission> permissions)
    {
        if (IsSystemRole)
        {
            return Result.Failure(new Error("role.system_role.immutable", "System roles cannot be modified.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(new Error("role.name.required", "Role name is required.", ErrorType.Validation));
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length > 100)
        {
            return Result.Failure(new Error("role.name.too_long", "Role name cannot exceed 100 characters.", ErrorType.Validation));
        }

        var distinctPermissions = permissions?.Distinct().ToList() ?? [];
        if (distinctPermissions.Count == 0)
        {
            return Result.Failure(new Error("role.permissions.required", "At least one permission must be assigned.", ErrorType.Validation));
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription?.Length > 500)
        {
            return Result.Failure(new Error("role.description.too_long", "Role description cannot exceed 500 characters.", ErrorType.Validation));
        }

        Name = trimmedName;
        Description = trimmedDescription;
        _permissions.Clear();
        _permissions.AddRange(distinctPermissions);
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        return Result.Success();
    }

    public bool CanDelete() => !IsSystemRole;
}
