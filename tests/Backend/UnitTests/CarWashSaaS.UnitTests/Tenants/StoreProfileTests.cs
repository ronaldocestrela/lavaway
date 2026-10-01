using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Domain;

namespace CarWashSaaS.UnitTests.Tenants;

public sealed class StoreProfileTests
{
    [Fact]
    public void Create_Should_Succeed_With_Valid_Profile_Data()
    {
        var tenantId = Guid.CreateVersion7();

        var result = StoreProfile.Create(
            tenantId,
            "LavaWay Auto Center Ltda",
            "LavaWay Centro",
            "11222333000181",
            "+5511999999999",
            "Rua das Flores, 123",
            "São Paulo",
            "SP",
            "01000-000");

        Assert.True(result.IsSuccess);
        Assert.Equal(tenantId, result.Value!.TenantId);
        Assert.Equal("LavaWay Auto Center Ltda", result.Value.LegalName);
        Assert.Equal("LavaWay Centro", result.Value.TradeName);
        Assert.Equal("11222333000181", result.Value.Cnpj);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
    }

    [Theory]
    [InlineData("", "LavaWay Centro", "12345678000199", "+5511999999999", "Rua das Flores, 123", "São Paulo", "SP", "01000-000")]
    [InlineData("LavaWay Auto Center Ltda", "", "12345678000199", "+5511999999999", "Rua das Flores, 123", "São Paulo", "SP", "01000-000")]
    [InlineData("LavaWay Auto Center Ltda", "LavaWay Centro", "123", "+5511999999999", "Rua das Flores, 123", "São Paulo", "SP", "01000-000")]
    [InlineData("LavaWay Auto Center Ltda", "LavaWay Centro", "12345678000199", "", "Rua das Flores, 123", "São Paulo", "SP", "01000-000")]
    public void Create_Should_Reject_Invalid_Profile_Data(
        string legalName,
        string tradeName,
        string cnpj,
        string phone,
        string street,
        string city,
        string state,
        string postalCode)
    {
        var result = StoreProfile.Create(
            Guid.CreateVersion7(),
            legalName,
            tradeName,
            cnpj,
            phone,
            street,
            city,
            state,
            postalCode);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public void Create_Should_Succeed_With_Valid_Branding_Data()
    {
        var tenantId = Guid.CreateVersion7();

        var result = StoreProfile.Create(
            tenantId,
            "LavaWay Auto Center Ltda",
            "LavaWay Centro",
            "11222333000181",
            "+5511999999999",
            "Rua das Flores, 123",
            "São Paulo",
            "SP",
            "01000-000",
            "https://cdn.example.com/tenants/logo.png",
            "#FF6600",
            "#0D1B2A");

        Assert.True(result.IsSuccess);
        Assert.Equal("https://cdn.example.com/tenants/logo.png", result.Value!.LogoUrl);
        Assert.Equal("#FF6600", result.Value.BrandPrimaryColor);
        Assert.Equal("#0D1B2A", result.Value.BrandSecondaryColor);
    }

    [Theory]
    [InlineData("https://cdn.example.com/tenants/logo.png", "#GGGGGG", "#0D1B2A")]
    [InlineData("https://cdn.example.com/tenants/logo.png", "#FF6600", "#ZZZZZZ")]
    [InlineData("not-a-url", "#FF6600", "#0D1B2A")]
    public void Create_Should_Reject_Invalid_Branding_Data(
        string logoUrl,
        string primaryColor,
        string secondaryColor)
    {
        var result = StoreProfile.Create(
            Guid.CreateVersion7(),
            "LavaWay Auto Center Ltda",
            "LavaWay Centro",
            "11222333000181",
            "+5511999999999",
            "Rua das Flores, 123",
            "São Paulo",
            "SP",
            "01000-000",
            logoUrl,
            primaryColor,
            secondaryColor);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public void Update_Should_Succeed_With_Valid_Profile_Data()
    {
        var tenantId = Guid.CreateVersion7();
        var original = StoreProfile.Create(
            tenantId,
            "LavaWay Auto Center Ltda",
            "LavaWay Centro",
            "11222333000181",
            "+5511999999999",
            "Rua das Flores, 123",
            "São Paulo",
            "SP",
            "01000-000");

        Assert.True(original.IsSuccess);

        var result = original.Value!.Update(
            "LavaWay Auto Center Atualizada Ltda",
            "LavaWay Centro Atualizado",
            "11222333000181",
            "+5511888888888",
            "Avenida Paulista, 456",
            "Rio de Janeiro",
            "RJ",
            "22000-000");

        Assert.True(result.IsSuccess);
        Assert.Equal("LavaWay Auto Center Atualizada Ltda", result.Value!.LegalName);
        Assert.Equal("LavaWay Centro Atualizado", result.Value.TradeName);
        Assert.Equal("11222333000181", result.Value.Cnpj);
        Assert.Equal("Avenida Paulista, 456", result.Value.Street);
        Assert.Equal("RJ", result.Value.State);
        Assert.Equal(tenantId, result.Value.TenantId);
    }

    [Theory]
    [InlineData("", "LavaWay Centro", "11222333000181", "+5511999999999", "Rua das Flores, 123", "São Paulo", "SP", "01000-000")]
    [InlineData("LavaWay Auto Center Ltda", "", "11222333000181", "+5511999999999", "Rua das Flores, 123", "São Paulo", "SP", "01000-000")]
    [InlineData("LavaWay Auto Center Ltda", "LavaWay Centro", "12345678000199", "+5511999999999", "Rua das Flores, 123", "São Paulo", "SP", "01000-000")]
    public void Update_Should_Reject_Invalid_Profile_Data(
        string legalName,
        string tradeName,
        string cnpj,
        string phone,
        string street,
        string city,
        string state,
        string postalCode)
    {
        var original = StoreProfile.Create(
            Guid.CreateVersion7(),
            "LavaWay Auto Center Ltda",
            "LavaWay Centro",
            "11222333000181",
            "+5511999999999",
            "Rua das Flores, 123",
            "São Paulo",
            "SP",
            "01000-000");

        Assert.True(original.IsSuccess);

        var result = original.Value!.Update(
            legalName,
            tradeName,
            cnpj,
            phone,
            street,
            city,
            state,
            postalCode);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }
}
