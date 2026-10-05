using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class PixChargeDomainTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _workOrderId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(30);
        var result = PixCharge.Create(
            _tenantId,
            _workOrderId,
            120.50m,
            "TX123456",
            "data:image/svg+xml;base64,PHN2Zz48L3N2Zz4=",
            "00020126580014br.gov.bcb.pix...",
            expiresAt);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(_tenantId, result.Value.TenantId);
        Assert.Equal(_workOrderId, result.Value.WorkOrderId);
        Assert.Equal(120.50m, result.Value.Amount);
        Assert.Equal(PixChargeStatusConstants.Pending, result.Value.Status);
        Assert.Equal("TX123456", result.Value.TxId);
        Assert.True(result.Value.IsActiveAndPending());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_WithNegativeOrZeroAmount_ShouldFail(decimal invalidAmount)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(30);
        var result = PixCharge.Create(
            _tenantId,
            _workOrderId,
            invalidAmount,
            "TX123",
            "base64",
            "payload",
            expiresAt);

        Assert.False(result.IsSuccess);
        Assert.Equal("billing.amount_invalid", result.Error?.Code);
    }

    [Fact]
    public void Create_WithPastExpiration_ShouldFail()
    {
        var pastExpiration = DateTimeOffset.UtcNow.AddMinutes(-5);
        var result = PixCharge.Create(
            _tenantId,
            _workOrderId,
            100m,
            "TX123",
            "base64",
            "payload",
            pastExpiration);

        Assert.False(result.IsSuccess);
        Assert.Equal("billing.expiration_invalid", result.Error?.Code);
    }

    [Fact]
    public void MarkAsPaid_ShouldUpdateStatusAndTimestamp()
    {
        var charge = PixCharge.Create(
            _tenantId,
            _workOrderId,
            80m,
            "TX123",
            "base64",
            "payload",
            DateTimeOffset.UtcNow.AddMinutes(30)).Value!;

        var now = DateTimeOffset.UtcNow;
        var paidResult = charge.MarkAsPaid(now);

        Assert.True(paidResult.IsSuccess);
        Assert.Equal(PixChargeStatusConstants.Paid, charge.Status);
        Assert.Equal(now, charge.PaidAtUtc);
        Assert.False(charge.IsActiveAndPending());
    }

    [Fact]
    public void Cancel_WhenPending_ShouldCancel()
    {
        var charge = PixCharge.Create(
            _tenantId,
            _workOrderId,
            50m,
            "TX123",
            "base64",
            "payload",
            DateTimeOffset.UtcNow.AddMinutes(30)).Value!;

        var cancelResult = charge.Cancel();

        Assert.True(cancelResult.IsSuccess);
        Assert.Equal(PixChargeStatusConstants.Cancelled, charge.Status);
    }

    [Fact]
    public void Cancel_WhenAlreadyPaid_ShouldFail()
    {
        var charge = PixCharge.Create(
            _tenantId,
            _workOrderId,
            50m,
            "TX123",
            "base64",
            "payload",
            DateTimeOffset.UtcNow.AddMinutes(30)).Value!;

        charge.MarkAsPaid(DateTimeOffset.UtcNow);
        var cancelResult = charge.Cancel();

        Assert.False(cancelResult.IsSuccess);
        Assert.Equal("billing.already_paid", cancelResult.Error?.Code);
    }
}
