namespace CarWashSaaS.Shared.Contracts;

public sealed record CreateTeamMemberRequest(
    string FullName,
    string Role,
    string? Email);
