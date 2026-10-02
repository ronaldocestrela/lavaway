using CarWashSaaS.Client.Core;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.UnitTests.Frontend;

public sealed class StoreProfileFormModelTests
{
    [Fact]
    public void ValidateStep1_ShouldSucceed_WhenLegalNameTradeNameAndCnpjAreValid()
    {
        var model = new StoreProfileFormModel
        {
            LegalName = "LavaWay Auto Center Ltda",
            TradeName = "LavaWay Centro",
            Cnpj = "11.222.333/0001-81"
        };

        var (isValid, errorMessage) = model.ValidateStep1();

        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Theory]
    [InlineData("", "LavaWay Centro", "11222333000181", "Razão Social é obrigatória.")]
    [InlineData("LavaWay Auto Center Ltda", "", "11222333000181", "Nome Fantasia é obrigatório.")]
    [InlineData("LavaWay Auto Center Ltda", "LavaWay Centro", "11111111111111", "CNPJ informado é inválido.")]
    [InlineData("LavaWay Auto Center Ltda", "LavaWay Centro", "123456", "CNPJ informado é inválido.")]
    public void ValidateStep1_ShouldFail_WhenInputsAreInvalid(
        string legalName, string tradeName, string cnpj, string expectedError)
    {
        var model = new StoreProfileFormModel
        {
            LegalName = legalName,
            TradeName = tradeName,
            Cnpj = cnpj
        };

        var (isValid, errorMessage) = model.ValidateStep1();

        Assert.False(isValid);
        Assert.Equal(expectedError, errorMessage);
    }

    [Fact]
    public void ValidateStep2_ShouldSucceed_WhenAddressAndPhoneAreValid()
    {
        var model = new StoreProfileFormModel
        {
            Phone = "(11) 99999-9999",
            PostalCode = "01000-000",
            Street = "Rua das Flores, 123",
            City = "São Paulo",
            State = "SP"
        };

        var (isValid, errorMessage) = model.ValidateStep2();

        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Theory]
    [InlineData("", "01000-000", "Rua A", "São Paulo", "SP", "Telefone de contato é obrigatório.")]
    [InlineData("(11) 99999-9999", "", "Rua A", "São Paulo", "SP", "CEP é obrigatório.")]
    [InlineData("(11) 99999-9999", "01000-000", "", "São Paulo", "SP", "Logradouro/Endereço é obrigatório.")]
    [InlineData("(11) 99999-9999", "01000-000", "Rua A", "", "SP", "Cidade é obrigatória.")]
    [InlineData("(11) 99999-9999", "01000-000", "Rua A", "São Paulo", "S", "UF deve ser uma sigla válida de 2 letras (ex: SP).")]
    public void ValidateStep2_ShouldFail_WhenInputsAreInvalid(
        string phone, string cep, string street, string city, string state, string expectedError)
    {
        var model = new StoreProfileFormModel
        {
            Phone = phone,
            PostalCode = cep,
            Street = street,
            City = city,
            State = state
        };

        var (isValid, errorMessage) = model.ValidateStep2();

        Assert.False(isValid);
        Assert.Equal(expectedError, errorMessage);
    }

    [Fact]
    public void FromDto_ShouldMapAndFormatCorrectly()
    {
        var dto = new StoreProfileDto(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "LavaWay Ltda",
            "LavaWay",
            "11222333000181",
            "+5511999999999",
            "Rua A",
            "São Paulo",
            "SP",
            "01000000",
            null,
            null,
            null);

        var model = StoreProfileFormModel.FromDto(dto);

        Assert.Equal("LavaWay Ltda", model.LegalName);
        Assert.Equal("11.222.333/0001-81", model.Cnpj);
        Assert.Equal("01000-000", model.PostalCode);
    }
}
