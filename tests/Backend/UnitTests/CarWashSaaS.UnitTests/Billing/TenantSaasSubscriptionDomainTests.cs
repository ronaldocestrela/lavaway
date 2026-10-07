using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class TenantSaasSubscriptionDomainTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void CreateTrial_Should_Initialize_With_14_Days_And_Trial_Status()
    {
        var result = TenantSaasSubscription.CreateTrial(_tenantId);

        Assert.True(result.IsSuccess);
        var sub = result.Value!;
        Assert.Equal(_tenantId, sub.TenantId);
        Assert.Equal(TenantSubscriptionStatus.Trial, sub.Status);
        Assert.Equal(SaasPlanTier.Pro, sub.PlanTier);
        Assert.Equal(0m, sub.MonthlyPrice);
        Assert.True(sub.IsOperationalAllowed());
        Assert.True(sub.CurrentPeriodEndUtc > DateTimeOffset.UtcNow.AddDays(13));
    }

    [Fact]
    public void Activate_Should_Transition_To_Active_And_Clear_GracePeriod()
    {
        var trialSub = TenantSaasSubscription.CreateTrial(_tenantId).Value!;
        var plan = SaasPlan.GetStandardPlans().First(p => p.Tier == SaasPlanTier.Basic);

        var activateResult = trialSub.Activate(plan, gatewaySubscriptionId: "sub_123", gatewayCustomerId: "cus_123");

        Assert.True(activateResult.IsSuccess);
        Assert.Equal(TenantSubscriptionStatus.Active, trialSub.Status);
        Assert.Equal(SaasPlanTier.Basic, trialSub.PlanTier);
        Assert.Equal(149.00m, trialSub.MonthlyPrice);
        Assert.Equal("sub_123", trialSub.GatewaySubscriptionId);
        Assert.Null(trialSub.GracePeriodEndsAtUtc);
        Assert.True(trialSub.IsOperationalAllowed());
    }

    [Fact]
    public void MarkOverdue_Should_Grant_5_Days_GracePeriod_When_First_Overdue()
    {
        var plan = SaasPlan.GetStandardPlans().First(p => p.Tier == SaasPlanTier.Pro);
        var sub = TenantSaasSubscription.CreateActive(_tenantId, plan, "cus_1", "sub_1").Value!;
        var now = DateTimeOffset.UtcNow;

        var overdueResult = sub.MarkOverdue(now, gracePeriodDays: 5);

        Assert.True(overdueResult.IsSuccess);
        Assert.Equal(TenantSubscriptionStatus.GracePeriod, sub.Status);
        Assert.NotNull(sub.GracePeriodEndsAtUtc);
        Assert.True(sub.GracePeriodEndsAtUtc.Value >= now.AddDays(4.9));
        Assert.True(sub.IsOperationalAllowed(), "Durante o período de tolerância a operação deve continuar permitida");
    }

    [Fact]
    public void MarkOverdue_Should_Suspend_To_Delinquent_When_GracePeriod_Has_Expired()
    {
        var plan = SaasPlan.GetStandardPlans().First(p => p.Tier == SaasPlanTier.Pro);
        var sub = TenantSaasSubscription.CreateActive(_tenantId, plan, "cus_1", "sub_1").Value!;
        var startOfOverdue = DateTimeOffset.UtcNow.AddDays(-6);

        // Primeiro marca como overdue no passado
        sub.MarkOverdue(startOfOverdue, gracePeriodDays: 5);
        Assert.Equal(TenantSubscriptionStatus.GracePeriod, sub.Status);

        // Agora executa a verificação no momento presente (6 dias depois)
        var verifyNow = DateTimeOffset.UtcNow;
        var secondCheck = sub.MarkOverdue(verifyNow);

        Assert.True(secondCheck.IsSuccess);
        Assert.Equal(TenantSubscriptionStatus.Delinquent, sub.Status);
        Assert.False(sub.IsOperationalAllowed(), "Após expirar o período de tolerância o acesso operacional deve ser bloqueado");
    }

    [Fact]
    public void RenewCycle_Should_Restore_Active_Status_And_Update_Cycle_Dates()
    {
        var plan = SaasPlan.GetStandardPlans().First(p => p.Tier == SaasPlanTier.Pro);
        var sub = TenantSaasSubscription.CreateActive(_tenantId, plan, "cus_1", "sub_1").Value!;
        sub.Suspend("Inadimplência prolongada");
        Assert.Equal(TenantSubscriptionStatus.Delinquent, sub.Status);

        var newStart = DateTimeOffset.UtcNow;
        var newEnd = newStart.AddMonths(1);

        var renewResult = sub.RenewCycle(newStart, newEnd);

        Assert.True(renewResult.IsSuccess);
        Assert.Equal(TenantSubscriptionStatus.Active, sub.Status);
        Assert.Equal(newStart, sub.CurrentPeriodStartUtc);
        Assert.Equal(newEnd, sub.CurrentPeriodEndUtc);
        Assert.Null(sub.GracePeriodEndsAtUtc);
        Assert.True(sub.IsOperationalAllowed());
    }

    [Fact]
    public void ChangePlan_Should_Update_Tier_And_Monthly_Price()
    {
        var basicPlan = SaasPlan.GetStandardPlans().First(p => p.Tier == SaasPlanTier.Basic);
        var enterprisePlan = SaasPlan.GetStandardPlans().First(p => p.Tier == SaasPlanTier.Enterprise);

        var sub = TenantSaasSubscription.CreateActive(_tenantId, basicPlan, "cus_1", "sub_1").Value!;
        Assert.Equal(SaasPlanTier.Basic, sub.PlanTier);

        var changeResult = sub.ChangePlan(enterprisePlan);

        Assert.True(changeResult.IsSuccess);
        Assert.Equal(SaasPlanTier.Enterprise, sub.PlanTier);
        Assert.Equal(599.00m, sub.MonthlyPrice);
    }
}
