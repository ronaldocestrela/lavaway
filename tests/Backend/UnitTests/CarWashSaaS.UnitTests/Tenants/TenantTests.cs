using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Domain;

namespace CarWashSaaS.UnitTests.Tenants;

public sealed class TenantTests
{
    [Fact]
    public void Create_ShouldTrimNameAndGenerateId()
    {
        var result = Tenant.Create("  Lavaway Centro  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Lavaway Centro", result.Value!.Name);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldRejectBlankName(string name)
    {
        var result = Tenant.Create(name);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }
}