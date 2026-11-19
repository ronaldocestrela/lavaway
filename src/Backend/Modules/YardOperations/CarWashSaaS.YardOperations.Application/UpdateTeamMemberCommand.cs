namespace CarWashSaaS.YardOperations.Application;

public sealed record UpdateTeamMemberCommand(
    string FullName,
    string Role,
    string? Email,
    bool EnableSystemAccess = false,
    string? SystemRole = null,
    string? Password = null,
    Guid? CustomRoleId = null);
