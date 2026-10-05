using CarWashSaaS.Billing.Domain;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class DailyCashClosingDomainTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void Close_WithValidData_CalculatesExpectedCashAndDifference()
    {
        var date = new DateOnly(2026, 10, 5);

        // Supplies = 200, Cash = 300, Bleeds = 50 => Expected = 450
        // Actual in drawer = 450 => Difference = 0
        var result = DailyCashClosing.Close(
            _tenantId,
            date,
            _userId,
            "Gestor Marcos",
            totalIncome: 1200m,
            totalPix: 500m,
            totalCash: 300m,
            totalCreditCard: 300m,
            totalDebitCard: 100m,
            totalSupplies: 200m,
            totalBleeds: 50m,
            actualCashInDrawer: 450m,
            notes: "Fechamento regular");

        Assert.True(result.IsSuccess);
        var closing = result.Value!;
        Assert.Equal(date, closing.ClosingDate);
        Assert.Equal(1200m, closing.TotalIncome);
        Assert.Equal(450m, closing.ExpectedCashInDrawer);
        Assert.Equal(450m, closing.ActualCashInDrawer);
        Assert.Equal(0m, closing.CashDifference);
        Assert.Equal(DailyCashClosingStatus.Closed, closing.Status);
    }

    [Fact]
    public void Close_WithDiscrepancy_RegistersDifferenceCorrectly()
    {
        var date = new DateOnly(2026, 10, 5);

        // Expected = 100 + 200 - 0 = 300
        // Actual = 280 => Difference = -20
        var result = DailyCashClosing.Close(
            _tenantId,
            date,
            _userId,
            "Gestor Marcos",
            totalIncome: 500m,
            totalPix: 300m,
            totalCash: 200m,
            totalCreditCard: 0m,
            totalDebitCard: 0m,
            totalSupplies: 100m,
            totalBleeds: 0m,
            actualCashInDrawer: 280m,
            notes: "Falta de R$ 20");

        Assert.True(result.IsSuccess);
        var closing = result.Value!;
        Assert.Equal(300m, closing.ExpectedCashInDrawer);
        Assert.Equal(280m, closing.ActualCashInDrawer);
        Assert.Equal(-20m, closing.CashDifference);
    }

    [Fact]
    public void Reopen_WhenClosed_ShouldSucceed()
    {
        var closing = DailyCashClosing.Close(
            _tenantId,
            new DateOnly(2026, 10, 5),
            _userId,
            "Gestor",
            500m, 200m, 300m, 0m, 0m, 0m, 0m).Value!;

        var reopenResult = closing.Reopen("Ajuste de lançamento");

        Assert.True(reopenResult.IsSuccess);
        Assert.Equal(DailyCashClosingStatus.Reopened, closing.Status);
        Assert.Contains("Ajuste de lançamento", closing.Notes);
    }
}
