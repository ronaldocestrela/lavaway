using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class CustomerSubscriptionDomainTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _planId = Guid.NewGuid();

    [Fact]
    public void Create_Should_Succeed_With_Valid_Initial_Plates()
    {
        var now = DateTimeOffset.UtcNow;
        var result = CustomerSubscription.Create(
            _tenantId,
            _customerId,
            "Carlos Silva",
            "11988887777",
            _planId,
            "Plano Mensal 4 Lavagens",
            creditsPerCycle: 4,
            allowedPlatesLimit: 2,
            initialPlates: ["ABC1D23", "XYZ9876"],
            periodStartUtc: now,
            periodEndUtc: now.AddDays(30),
            cardLastFourDigits: "1234",
            cardBrand: "Visa");

        Assert.True(result.IsSuccess);
        var sub = result.Value!;
        Assert.Equal(4, sub.TotalCreditsInCycle);
        Assert.Equal(0, sub.UsedCreditsInCycle);
        Assert.Equal(4, sub.AvailableCredits);
        Assert.Equal(SubscriptionStatusConstants.Active, sub.Status);
        Assert.Equal(2, sub.AuthorizedPlates.Count);
        Assert.True(sub.HasPlate("ABC1D23"));
        Assert.True(sub.HasPlate("XYZ9876"));
    }

    [Fact]
    public void AddAuthorizedPlate_Should_Enforce_Allowed_Plates_Limit()
    {
        var now = DateTimeOffset.UtcNow;
        var sub = CustomerSubscription.Create(
            _tenantId,
            _customerId,
            "Carlos Silva",
            "11988887777",
            _planId,
            "Plano Single",
            creditsPerCycle: 2,
            allowedPlatesLimit: 1,
            initialPlates: ["ABC1D23"],
            periodStartUtc: now,
            periodEndUtc: now.AddDays(30)).Value!;

        var addSecondPlateResult = sub.AddAuthorizedPlate("XYZ9876", allowedPlatesLimit: 1, now);

        Assert.False(addSecondPlateResult.IsSuccess);
        Assert.Equal("subscription.plate_limit_reached", addSecondPlateResult.Error!.Code);
        Assert.Single(sub.AuthorizedPlates);
    }

    [Fact]
    public void AddAuthorizedPlate_Should_Prevent_Duplicate_Plate()
    {
        var now = DateTimeOffset.UtcNow;
        var sub = CustomerSubscription.Create(
            _tenantId,
            _customerId,
            "Carlos Silva",
            "11988887777",
            _planId,
            "Plano Duo",
            creditsPerCycle: 4,
            allowedPlatesLimit: 2,
            initialPlates: ["ABC1D23"],
            periodStartUtc: now,
            periodEndUtc: now.AddDays(30)).Value!;

        var duplicateResult = sub.AddAuthorizedPlate("abc-1d23", allowedPlatesLimit: 2, now);

        Assert.False(duplicateResult.IsSuccess);
        Assert.Equal("subscription.plate_duplicate", duplicateResult.Error!.Code);
    }

    [Fact]
    public void ConsumeCredit_Should_Deduct_Credit_When_Plate_Authorized_And_Balance_Available()
    {
        var now = DateTimeOffset.UtcNow;
        var sub = CustomerSubscription.Create(
            _tenantId,
            _customerId,
            "Carlos Silva",
            "11988887777",
            _planId,
            "Plano Mensal",
            creditsPerCycle: 2,
            allowedPlatesLimit: 1,
            initialPlates: ["ABC1D23"],
            periodStartUtc: now.AddDays(-1),
            periodEndUtc: now.AddDays(29)).Value!;

        var workOrderId = Guid.NewGuid();
        var usageResult = sub.ConsumeCredit("abc1d23", workOrderId, "Lavagem Completa", now, "Uso 1");

        Assert.True(usageResult.IsSuccess);
        Assert.NotNull(usageResult.Value);
        Assert.Equal(1, sub.UsedCreditsInCycle);
        Assert.Equal(1, sub.AvailableCredits);
        Assert.Single(sub.Usages);
        Assert.Equal("ABC1D23", usageResult.Value.Plate);
        Assert.Equal(workOrderId, usageResult.Value.WorkOrderId);
    }

    [Fact]
    public void ConsumeCredit_Should_Fail_When_Plate_Not_Authorized()
    {
        var now = DateTimeOffset.UtcNow;
        var sub = CustomerSubscription.Create(
            _tenantId,
            _customerId,
            "Carlos Silva",
            "11988887777",
            _planId,
            "Plano Mensal",
            creditsPerCycle: 2,
            allowedPlatesLimit: 1,
            initialPlates: ["ABC1D23"],
            periodStartUtc: now.AddDays(-1),
            periodEndUtc: now.AddDays(29)).Value!;

        var usageResult = sub.ConsumeCredit("ZZZ9999", Guid.NewGuid(), "Lavagem", now);

        Assert.False(usageResult.IsSuccess);
        Assert.Equal("subscription.plate_unauthorized", usageResult.Error!.Code);
        Assert.Equal(0, sub.UsedCreditsInCycle);
        Assert.Equal(2, sub.AvailableCredits);
    }

    [Fact]
    public void ConsumeCredit_Should_Fail_When_Credits_Exhausted()
    {
        var now = DateTimeOffset.UtcNow;
        var sub = CustomerSubscription.Create(
            _tenantId,
            _customerId,
            "Carlos Silva",
            "11988887777",
            _planId,
            "Plano Mensal",
            creditsPerCycle: 1,
            allowedPlatesLimit: 1,
            initialPlates: ["ABC1D23"],
            periodStartUtc: now.AddDays(-1),
            periodEndUtc: now.AddDays(29)).Value!;

        // Consome o único crédito disponível
        var firstUsage = sub.ConsumeCredit("ABC1D23", Guid.NewGuid(), "Lavagem", now);
        Assert.True(firstUsage.IsSuccess);
        Assert.Equal(0, sub.AvailableCredits);

        // Tenta consumir o segundo
        var secondUsage = sub.ConsumeCredit("ABC1D23", Guid.NewGuid(), "Segunda Lavagem", now);
        Assert.False(secondUsage.IsSuccess);
        Assert.Equal("subscription.credits_exhausted", secondUsage.Error!.Code);
        Assert.Equal(1, sub.UsedCreditsInCycle);
    }

    [Fact]
    public void ConsumeCredit_Should_Fail_When_Subscription_Is_Canceled_Or_Expired()
    {
        var now = DateTimeOffset.UtcNow;
        var sub = CustomerSubscription.Create(
            _tenantId,
            _customerId,
            "Carlos Silva",
            "11988887777",
            _planId,
            "Plano Mensal",
            creditsPerCycle: 2,
            allowedPlatesLimit: 1,
            initialPlates: ["ABC1D23"],
            periodStartUtc: now.AddDays(-30),
            periodEndUtc: now.AddDays(-1)).Value!; // Expirado ontem

        var usageResult = sub.ConsumeCredit("ABC1D23", Guid.NewGuid(), "Lavagem", now);
        Assert.False(usageResult.IsSuccess);
        Assert.Equal("subscription.period_expired", usageResult.Error!.Code);

        // Agora com status cancelado
        sub.Cancel("Cliente desistiu", now);
        var canceledUsage = sub.ConsumeCredit("ABC1D23", Guid.NewGuid(), "Lavagem", now.AddDays(-15));
        Assert.False(canceledUsage.IsSuccess);
        Assert.Equal("subscription.inactive", canceledUsage.Error!.Code);
    }

    [Fact]
    public void ConsumeCredit_Should_Prevent_Duplicate_Usage_For_Same_WorkOrder()
    {
        var now = DateTimeOffset.UtcNow;
        var sub = CustomerSubscription.Create(
            _tenantId,
            _customerId,
            "Carlos Silva",
            "11988887777",
            _planId,
            "Plano Mensal",
            creditsPerCycle: 4,
            allowedPlatesLimit: 1,
            initialPlates: ["ABC1D23"],
            periodStartUtc: now.AddDays(-1),
            periodEndUtc: now.AddDays(29)).Value!;

        var workOrderId = Guid.NewGuid();
        var first = sub.ConsumeCredit("ABC1D23", workOrderId, "Lavagem", now);
        Assert.True(first.IsSuccess);

        var duplicate = sub.ConsumeCredit("ABC1D23", workOrderId, "Lavagem", now);
        Assert.False(duplicate.IsSuccess);
        Assert.Equal("subscription.work_order_already_used", duplicate.Error!.Code);
        Assert.Equal(1, sub.UsedCreditsInCycle);
    }

    [Fact]
    public void CancelUsageForWorkOrder_Should_Refund_Credit_Successfully()
    {
        var now = DateTimeOffset.UtcNow;
        var sub = CustomerSubscription.Create(
            _tenantId,
            _customerId,
            "Carlos Silva",
            "11988887777",
            _planId,
            "Plano Mensal",
            creditsPerCycle: 2,
            allowedPlatesLimit: 1,
            initialPlates: ["ABC1D23"],
            periodStartUtc: now.AddDays(-1),
            periodEndUtc: now.AddDays(29)).Value!;

        var workOrderId = Guid.NewGuid();
        sub.ConsumeCredit("ABC1D23", workOrderId, "Lavagem", now);
        Assert.Equal(1, sub.UsedCreditsInCycle);
        Assert.Equal(1, sub.AvailableCredits);

        var refundResult = sub.CancelUsageForWorkOrder(workOrderId);
        Assert.True(refundResult.IsSuccess);
        Assert.Equal(0, sub.UsedCreditsInCycle);
        Assert.Equal(2, sub.AvailableCredits);
        Assert.Empty(sub.Usages);
    }

    [Fact]
    public void RenewCycle_Should_Reset_Usage_And_Update_Period()
    {
        var now = DateTimeOffset.UtcNow;
        var sub = CustomerSubscription.Create(
            _tenantId,
            _customerId,
            "Carlos Silva",
            "11988887777",
            _planId,
            "Plano Mensal",
            creditsPerCycle: 2,
            allowedPlatesLimit: 1,
            initialPlates: ["ABC1D23"],
            periodStartUtc: now.AddDays(-30),
            periodEndUtc: now).Value!;

        sub.ConsumeCredit("ABC1D23", Guid.NewGuid(), "Lavagem 1", now.AddDays(-10));
        sub.ConsumeCredit("ABC1D23", Guid.NewGuid(), "Lavagem 2", now.AddDays(-5));
        Assert.Equal(0, sub.AvailableCredits);

        var newStart = now;
        var newEnd = now.AddDays(30);
        sub.RenewCycle(newStart, newEnd, creditsToGrant: 2);

        Assert.Equal(SubscriptionStatusConstants.Active, sub.Status);
        Assert.Equal(newStart, sub.CurrentPeriodStartUtc);
        Assert.Equal(newEnd, sub.CurrentPeriodEndUtc);
        Assert.Equal(0, sub.UsedCreditsInCycle);
        Assert.Equal(2, sub.AvailableCredits);
    }
}
