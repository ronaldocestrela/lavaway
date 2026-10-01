namespace CarWashSaaS.YardOperations.Application;

public sealed record CreateTeamMemberCommand(string FullName, string Role, string Email);
