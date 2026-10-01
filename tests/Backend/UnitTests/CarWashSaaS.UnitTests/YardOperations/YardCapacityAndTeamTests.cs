using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class YardCapacityAndTeamTests
{
    [Fact]
    public void YardCapacity_Create_Should_Succeed_With_Valid_Data()
    {
        var tenantId = Guid.NewGuid();

        var result = YardCapacity.Create(tenantId, 6, "Boxes de lavagem");

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value!.TotalBoxes);
        Assert.Equal("Boxes de lavagem", result.Value!.Description);
    }

    [Fact]
    public void YardCapacity_Create_Should_Reject_NonPositive_Capacity()
    {
        var tenantId = Guid.NewGuid();

        var result = YardCapacity.Create(tenantId, 0, "Capacidade inválida");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public void TeamMember_Create_Should_Reject_Empty_Name()
    {
        var tenantId = Guid.NewGuid();

        var result = TeamMember.Create(tenantId, "   ", "Operador de pátio", "operator@example.com");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }
}
