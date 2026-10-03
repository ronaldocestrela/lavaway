namespace CarWashSaaS.YardOperations.Application;

public sealed record UpdateTeamMemberCommand(string FullName, string Role, string? Email);
