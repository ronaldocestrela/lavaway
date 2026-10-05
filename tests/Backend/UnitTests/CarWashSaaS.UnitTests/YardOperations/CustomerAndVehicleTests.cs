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
    public void Customer_ShouldNormalizePhoneForSearchAndPreserveDisplayValue()
    {
        var result = Customer.Create(Guid.CreateVersion7(), " Maria Silva ", "+55 (11) 99999-9999");

        Assert.True(result.IsSuccess);
        Assert.Equal("+55 (11) 99999-9999", result.Value!.Phone);
        Assert.Equal("11999999999", result.Value.NormalizedPhone);
        Assert.Equal("Maria Silva", result.Value.Name);
    }

    [Fact]
    public void Customer_ShouldAllowDifferentCustomersToSharePhoneNumber()
    {
        var tenantId = Guid.CreateVersion7();

        var first = Customer.Create(tenantId, "Maria Silva", "+55 (11) 99999-9999");
        var second = Customer.Create(tenantId, "Joao Silva", "11 99999-9999");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.NormalizedPhone, second.Value!.NormalizedPhone);
    }

    [Theory]
    [InlineData("++--()")]
    [InlineData("   ")]
    public void Customer_ShouldRejectPhoneWithoutDigits(string phone)
    {
        var result = Customer.Create(Guid.CreateVersion7(), "Maria Silva", phone);

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

    [Fact]
    public void Customer_Update_ShouldUpdateNameAndPhoneProperly()
    {
        var customer = Customer.Create(Guid.CreateVersion7(), "Maria Silva", "+5511999999999").Value!;

        var updateResult = customer.Update("Maria Santos", "11 98888-7777");

        Assert.True(updateResult.IsSuccess);
        Assert.Equal("Maria Santos", customer.Name);
        Assert.Equal("11 98888-7777", customer.Phone);
        Assert.Equal("11988887777", customer.NormalizedPhone);
    }

    [Fact]
    public void Customer_Update_ShouldRejectInvalidData()
    {
        var customer = Customer.Create(Guid.CreateVersion7(), "Maria Silva", "+5511999999999").Value!;

        var invalidNameResult = customer.Update("", "+5511999999999");
        var invalidPhoneResult = customer.Update("Maria Santos", "---");

        Assert.False(invalidNameResult.IsSuccess);
        Assert.Equal(ErrorType.Validation, invalidNameResult.Error!.Type);
        Assert.False(invalidPhoneResult.IsSuccess);
        Assert.Equal(ErrorType.Validation, invalidPhoneResult.Error!.Type);
    }

    [Fact]
    public void Vehicle_Update_ShouldUpdatePlateAndSizeProperly()
    {
        var vehicle = Vehicle.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "ABC1D23", VehicleSize.HatchSedan).Value!;

        var updateResult = vehicle.Update("xyz-9999", VehicleSize.Suv);

        Assert.True(updateResult.IsSuccess);
        Assert.Equal("XYZ9999", vehicle.Plate);
        Assert.Equal(VehicleSize.Suv, vehicle.Size);
    }

    [Fact]
    public void Vehicle_Update_ShouldRejectInvalidPlate()
    {
        var vehicle = Vehicle.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "ABC1D23", VehicleSize.HatchSedan).Value!;

        var updateResult = vehicle.Update("invalid-plate", VehicleSize.PickupVan);

        Assert.False(updateResult.IsSuccess);
        Assert.Equal(ErrorType.Validation, updateResult.Error!.Type);
    }
}
