using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public static class CommissionCalculator
{
    public static (decimal Percentage, decimal CommissionAmount) CalculateItemCommission(
        WorkOrderItem item,
        string? operatorRole,
        IReadOnlyCollection<CommissionRule> rules)
    {
        if (string.IsNullOrWhiteSpace(operatorRole) || rules.Count == 0)
        {
            return (0m, 0m);
        }

        var normalizedRole = operatorRole.Trim();
        var matchingRule = rules.FirstOrDefault(r =>
            string.Equals(r.ServiceName, item.ServiceName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.RoleName, normalizedRole, StringComparison.OrdinalIgnoreCase));

        if (matchingRule is null)
        {
            return (0m, 0m);
        }

        var percentage = matchingRule.Percentage;
        var amount = Math.Round(item.TotalAmount * (percentage / 100m), 2, MidpointRounding.AwayFromZero);
        return (percentage, amount);
    }
}
