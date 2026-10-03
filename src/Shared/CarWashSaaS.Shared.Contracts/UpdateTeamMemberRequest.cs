namespace CarWashSaaS.Shared.Contracts;

public sealed record UpdateTeamMemberRequest(
    string FullName,
    string Role,
    string? Email);
