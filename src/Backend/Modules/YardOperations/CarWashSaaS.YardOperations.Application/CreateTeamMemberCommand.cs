namespace CarWashSaaS.YardOperations.Application;

public sealed record CreateTeamMemberCommand(
    string FullName,
    string Role,
    string? Email,
    bool EnableSystemAccess = false,
    string? SystemRole = null,
    string? Password = null);
