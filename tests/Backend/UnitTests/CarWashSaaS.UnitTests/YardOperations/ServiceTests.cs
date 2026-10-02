using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class ServiceTests
{
    [Fact]
    public void Create_ShouldRequireAtLeastOnePricePerVehicleSize()
    {
        var result = Service.Create(Guid.CreateVersion7(), "Lavagem completa", "Lavagem", []);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public void Create_ShouldRejectDuplicateVehicleSizePrices()
    {
        var prices = new[]
        {
            ServicePrice.Create(Guid.CreateVersion7(), VehicleSize.HatchSedan, 80m, 45).Value!,
            ServicePrice.Create(Guid.CreateVersion7(), VehicleSize.HatchSedan, 95m, 60).Value!
        };

        var result = Service.Create(prices[0].TenantId, "Lavagem completa", "Lavagem", prices);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
    }
}
