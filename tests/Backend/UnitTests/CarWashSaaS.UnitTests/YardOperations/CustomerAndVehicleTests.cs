using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class CustomerAndVehicleTests
{
    [Fact]
    public void Customer_ShouldRequireTenantAndName()
    {
        var result = Customer.Create(Guid.Empty, "Maria Silva", "+5511999999999");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public void Vehicle_ShouldNormalizePlateAndKeepTenantOwnership()
    {
        var tenantId = Guid.CreateVersion7();
        var customerId = Guid.CreateVersion7();

        var result = Vehicle.Create(tenantId, customerId, "abc-1d23", VehicleSize.HatchSedan);

        Assert.True(result.IsSuccess);
        Assert.Equal("ABC1D23", result.Value!.Plate);
        Assert.Equal(tenantId, result.Value.TenantId);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
    }

    [Theory]
    [InlineData("ABC123")]
    [InlineData("ABC-12@3")]
    public void Vehicle_ShouldRejectInvalidPlate(string plate)
    {
        var result = Vehicle.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), plate, VehicleSize.Suv);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }
}