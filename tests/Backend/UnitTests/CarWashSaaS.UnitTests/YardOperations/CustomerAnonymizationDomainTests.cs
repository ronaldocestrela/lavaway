using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class CustomerAnonymizationDomainTests
{
    [Fact]
    public void Customer_Anonymize_Should_Set_Anonymized_Name_And_Clear_Phone_And_Set_Hash()
    {
        var tenantId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Rodrigo Silveira", "11988887777").Value!;

        var anonymizeResult = customer.Anonymize("abcdef0123456789abcdef0123456789");

        Assert.True(anonymizeResult.IsSuccess);
        Assert.True(customer.IsAnonymized);
        Assert.NotNull(customer.AnonymizedAtUtc);
        Assert.Equal(Customer.AnonymizedDefaultName, customer.Name);
        Assert.Equal(string.Empty, customer.Phone);
        Assert.Equal("abcdef0123456789abcdef0123456789", customer.NormalizedPhone);
    }

    [Fact]
    public void Customer_Anonymize_Should_Fail_When_Already_Anonymized()
    {
        var tenantId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Rodrigo Silveira", "11988887777").Value!;
        customer.Anonymize("abcdef0123456789abcdef0123456789");

        var secondResult = customer.Anonymize("1234567890abcdef1234567890abcdef");

        Assert.False(secondResult.IsSuccess);
        Assert.Equal(ErrorType.Conflict, secondResult.Error!.Type);
        Assert.Equal("customer.already_anonymized", secondResult.Error.Code);
    }

    [Fact]
    public void Customer_Anonymize_Should_Fail_When_Hash_Is_Empty_Or_Too_Long()
    {
        var tenantId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Rodrigo Silveira", "11988887777").Value!;

        var emptyHashResult = customer.Anonymize(" ");
        var tooLongHashResult = customer.Anonymize(new string('a', 33));

        Assert.False(emptyHashResult.IsSuccess);
        Assert.Equal(ErrorType.Validation, emptyHashResult.Error!.Type);
        Assert.False(tooLongHashResult.IsSuccess);
        Assert.Equal(ErrorType.Validation, tooLongHashResult.Error!.Type);
    }

    [Fact]
    public void Customer_Update_Should_Fail_When_Customer_Is_Anonymized()
    {
        var tenantId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Rodrigo Silveira", "11988887777").Value!;
        customer.Anonymize("abcdef0123456789abcdef0123456789");

        var updateResult = customer.Update("Novo Nome", "11999990000");

        Assert.False(updateResult.IsSuccess);
        Assert.Equal(ErrorType.Conflict, updateResult.Error!.Type);
        Assert.Equal("customer.anonymized", updateResult.Error.Code);
    }

    [Fact]
    public void Vehicle_DisassociateCustomer_Should_Set_CustomerId_To_Null()
    {
        var tenantId = Guid.CreateVersion7();
        var customerId = Guid.CreateVersion7();
        var vehicle = Vehicle.Create(tenantId, customerId, "ABC1D23", VehicleSize.HatchSedan).Value!;

        var disassociateResult = vehicle.DisassociateCustomer();

        Assert.True(disassociateResult.IsSuccess);
        Assert.Null(vehicle.CustomerId);
    }

    [Fact]
    public void Vehicle_ReassignCustomer_Should_Assign_New_CustomerId_And_Reject_Empty_Guid()
    {
        var tenantId = Guid.CreateVersion7();
        var customerId = Guid.CreateVersion7();
        var newCustomerId = Guid.CreateVersion7();
        var vehicle = Vehicle.Create(tenantId, customerId, "ABC1D23", VehicleSize.HatchSedan).Value!;

        vehicle.DisassociateCustomer();
        var reassignResult = vehicle.ReassignCustomer(newCustomerId);

        Assert.True(reassignResult.IsSuccess);
        Assert.Equal(newCustomerId, vehicle.CustomerId);

        var rejectEmptyResult = vehicle.ReassignCustomer(Guid.Empty);
        Assert.False(rejectEmptyResult.IsSuccess);
        Assert.Equal(ErrorType.Validation, rejectEmptyResult.Error!.Type);
    }
}
