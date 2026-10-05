using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class CommissionCalculatorTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void CalculateItemCommission_WhenRuleMatches_CalculatesCorrectPercentageAndAmount()
    {
        // Arrange
        var item = WorkOrderItem.Create(_tenantId, Guid.NewGuid(), "Polimento Técnico", 300.00m, 120, 1).Value!;
        var rules = new List<CommissionRule>
        {
            CommissionRule.Create(_tenantId, "Polimento Técnico", "Detailer", 30.0m).Value!,
            CommissionRule.Create(_tenantId, "Lavagem Simples", "Lavador", 10.0m).Value!
        };

        // Act
        var (percentage, amount) = CommissionCalculator.CalculateItemCommission(item, "Detailer", rules);

        // Assert
        Assert.Equal(30.0m, percentage);
        Assert.Equal(90.00m, amount); // 300 * 30% = 90
    }

    [Fact]
    public void CalculateItemCommission_WhenNoRuleMatchesRole_ReturnsZero()
    {
        var item = WorkOrderItem.Create(_tenantId, Guid.NewGuid(), "Polimento Técnico", 300.00m, 120, 1).Value!;
        var rules = new List<CommissionRule>
        {
            CommissionRule.Create(_tenantId, "Polimento Técnico", "Detailer", 30.0m).Value!
        };

        var (percentage, amount) = CommissionCalculator.CalculateItemCommission(item, "Ajudante", rules);

        Assert.Equal(0m, percentage);
        Assert.Equal(0m, amount);
    }

    [Fact]
    public void CalculateItemCommission_WhenNoRuleMatchesService_ReturnsZero()
    {
        var item = WorkOrderItem.Create(_tenantId, Guid.NewGuid(), "Higienização Interna", 250.00m, 90, 1).Value!;
        var rules = new List<CommissionRule>
        {
            CommissionRule.Create(_tenantId, "Polimento Técnico", "Detailer", 30.0m).Value!
        };

        var (percentage, amount) = CommissionCalculator.CalculateItemCommission(item, "Detailer", rules);

        Assert.Equal(0m, percentage);
        Assert.Equal(0m, amount);
    }
}
