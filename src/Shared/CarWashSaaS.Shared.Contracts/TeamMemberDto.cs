namespace CarWashSaaS.Shared.Contracts;

public sealed record TeamMemberDto(
    Guid Id,
    string FullName,
    string Role,
    string Email,
    bool IsActive,
    Guid? UserId = null,
    bool HasSystemAccess = false,
    string? SystemRole = null,
    Guid? CustomRoleId = null);
