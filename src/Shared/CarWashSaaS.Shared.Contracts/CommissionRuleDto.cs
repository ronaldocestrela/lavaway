namespace CarWashSaaS.Shared.Contracts;

public sealed record CommissionRuleDto(
    Guid Id,
    string ServiceName,
    string RoleName,
    decimal Percentage);
