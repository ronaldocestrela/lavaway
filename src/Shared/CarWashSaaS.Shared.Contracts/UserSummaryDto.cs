namespace CarWashSaaS.Shared.Contracts;

public sealed record UserSummaryDto(
    Guid Id,
    Guid TenantId,
    string Email,
    string Role,
    IReadOnlyCollection<string> Permissions);
