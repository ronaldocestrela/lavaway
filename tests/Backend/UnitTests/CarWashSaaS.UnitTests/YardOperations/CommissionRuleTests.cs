using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class CommissionRuleTests
{
    [Fact]
    public void Create_Should_Succeed_With_Valid_Data()
    {
        var tenantId = Guid.NewGuid();

        var result = CommissionRule.Create(tenantId, "Lavagem completa", "Operador", 12.5m);

        Assert.True(result.IsSuccess);
        Assert.Equal(12.5m, result.Value!.Percentage);
        Assert.Equal("Operador", result.Value!.RoleName);
    }

    [Fact]
    public void Create_Should_Reject_Percentage_Above_100()
    {
        var tenantId = Guid.NewGuid();

        var result = CommissionRule.Create(tenantId, "Detalhamento", "Líder", 101m);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }
}
