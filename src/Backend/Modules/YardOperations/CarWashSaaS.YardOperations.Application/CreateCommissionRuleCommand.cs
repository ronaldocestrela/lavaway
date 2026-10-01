namespace CarWashSaaS.YardOperations.Application;

public sealed record CreateCommissionRuleCommand(string ServiceName, string RoleName, decimal Percentage);
