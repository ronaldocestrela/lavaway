using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class SubscriptionPlanDomainTests
{
    [Fact]
    public void Create_Should_Succeed_When_Valid_Data_Provided()
    {
        var tenantId = Guid.NewGuid();
        var result = SubscriptionPlan.Create(
            tenantId,
            "Plano Mensal Gold",
            "4 lavagens completas por mês",
            199.90m,
            4,
            2,
            30);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal(tenantId, result.Value.TenantId);
        Assert.Equal("Plano Mensal Gold", result.Value.Name);
        Assert.Equal(199.90m, result.Value.MonthlyPrice);
        Assert.Equal(4, result.Value.CreditsPerCycle);
        Assert.Equal(2, result.Value.AllowedPlatesLimit);
        Assert.Equal(30, result.Value.BillingIntervalDays);
        Assert.True(result.Value.IsActive);
    }

    [Theory]
    [InlineData("", "Nome obrigatório")]
    [InlineData("   ", "Nome obrigatório")]
    public void Create_Should_Fail_When_Name_Is_Empty(string invalidName, string reason)
    {
        _ = reason;
        var result = SubscriptionPlan.Create(
            Guid.NewGuid(),
            invalidName,
            "Desc",
            100m,
            2,
            1);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("plan.name_required", result.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_Should_Fail_When_Price_Is_Not_Positive(decimal invalidPrice)
    {
        var result = SubscriptionPlan.Create(
            Guid.NewGuid(),
            "Plano Básico",
            "Desc",
            invalidPrice,
            2,
            1);

        Assert.False(result.IsSuccess);
        Assert.Equal("plan.price_invalid", result.Error!.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_Should_Fail_When_Credits_Are_Not_Positive(int invalidCredits)
    {
        var result = SubscriptionPlan.Create(
            Guid.NewGuid(),
            "Plano Básico",
            "Desc",
            100m,
            invalidCredits,
            1);

        Assert.False(result.IsSuccess);
        Assert.Equal("plan.credits_invalid", result.Error!.Code);
    }

    [Fact]
    public void Update_Should_Modify_Plan_Properties_Successfully()
    {
        var plan = SubscriptionPlan.Create(
            Guid.NewGuid(),
            "Plano Antigo",
            "Desc",
            100m,
            2,
            1).Value!;

        var updateResult = plan.Update(
            "Plano Novo",
            "Nova Descrição",
            150m,
            3,
            2,
            false);

        Assert.True(updateResult.IsSuccess);
        Assert.Equal("Plano Novo", plan.Name);
        Assert.Equal("Nova Descrição", plan.Description);
        Assert.Equal(150m, plan.MonthlyPrice);
        Assert.Equal(3, plan.CreditsPerCycle);
        Assert.Equal(2, plan.AllowedPlatesLimit);
        Assert.False(plan.IsActive);
        Assert.NotNull(plan.UpdatedUtc);
    }
}
