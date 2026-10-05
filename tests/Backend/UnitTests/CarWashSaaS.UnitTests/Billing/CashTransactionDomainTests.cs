using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class CashTransactionDomainTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void CreateIncome_WithValidData_ShouldSucceed()
    {
        // Act
        var result = CashTransaction.CreateIncome(
            _tenantId,
            150.00m,
            PaymentMethodConstants.Cash,
            "Pagamento OS #1001",
            workOrderId: Guid.NewGuid(),
            registeredByUserName: "Carlos");

        // Assert
        Assert.True(result.IsSuccess);
        var tx = result.Value!;
        Assert.NotEqual(Guid.Empty, tx.Id);
        Assert.Equal(_tenantId, tx.TenantId);
        Assert.Equal(150.00m, tx.Amount);
        Assert.Equal(CashTransactionType.Income, tx.Type);
        Assert.Equal(PaymentMethodConstants.Cash, tx.PaymentMethod);
        Assert.Equal("Carlos", tx.RegisteredByUserName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void CreateIncome_WithInvalidAmount_ShouldFail(decimal amount)
    {
        var result = CashTransaction.CreateIncome(
            _tenantId,
            amount,
            PaymentMethodConstants.Pix,
            "Descrição");

        Assert.False(result.IsSuccess);
        Assert.Equal("cash_transaction.amount.invalid", result.Error!.Code);
    }

    [Fact]
    public void CreateIncome_WithInvalidPaymentMethod_ShouldFail()
    {
        var result = CashTransaction.CreateIncome(
            _tenantId,
            50m,
            "Cheque",
            "Descrição");

        Assert.False(result.IsSuccess);
        Assert.Equal("cash_transaction.payment_method.invalid", result.Error!.Code);
    }

    [Fact]
    public void CreateBleed_WithValidData_ShouldSucceed()
    {
        var result = CashTransaction.CreateBleed(
            _tenantId,
            80.00m,
            "Compra de shampoo automotivo",
            registeredByUserName: "Gestor");

        Assert.True(result.IsSuccess);
        var tx = result.Value!;
        Assert.Equal(CashTransactionType.Bleed, tx.Type);
        Assert.Equal(PaymentMethodConstants.Cash, tx.PaymentMethod);
        Assert.Equal(80.00m, tx.Amount);
    }

    [Fact]
    public void CreateBleed_WithoutDescription_ShouldFail()
    {
        var result = CashTransaction.CreateBleed(
            _tenantId,
            50m,
            "   ");

        Assert.False(result.IsSuccess);
        Assert.Equal("cash_transaction.description.required", result.Error!.Code);
    }

    [Fact]
    public void CreateSupply_WithValidData_ShouldSucceed()
    {
        var result = CashTransaction.CreateSupply(
            _tenantId,
            200.00m,
            "Fundo de troco inicial",
            registeredByUserName: "Gestor");

        Assert.True(result.IsSuccess);
        var tx = result.Value!;
        Assert.Equal(CashTransactionType.Supply, tx.Type);
        Assert.Equal(PaymentMethodConstants.Cash, tx.PaymentMethod);
        Assert.Equal(200.00m, tx.Amount);
    }
}
