namespace CarWashSaaS.Shared.Contracts;

public sealed record CustomRoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    IReadOnlyList<string> Permissions,
    int AssignedUsersCount = 0);

public sealed record CreateCustomRoleRequest(
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions);

public sealed record UpdateCustomRoleRequest(
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions);

public sealed record PermissionDefinitionDto(
    string Code,
    string Name,
    string Description,
    string Category);
