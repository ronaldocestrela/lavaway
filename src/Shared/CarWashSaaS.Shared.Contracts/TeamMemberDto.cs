namespace CarWashSaaS.Shared.Contracts;

public sealed record TeamMemberDto(
    Guid Id,
    string FullName,
    string Role,
    string Email,
    bool IsActive);
