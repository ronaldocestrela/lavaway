namespace CarWashSaaS.Shared.Contracts;

public sealed record CreateCommissionRuleRequest(
    string ServiceName,
    string RoleName,
    decimal Percentage);
