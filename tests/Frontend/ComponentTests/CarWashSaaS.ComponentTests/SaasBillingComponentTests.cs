using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Shared.Contracts;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class SaasBillingComponentTests : BunitContext
{
    [Fact]
    public void PlanQuotaProgressBar_Should_Render_Counters_And_Progress()
    {
        var cut = Render<PlanQuotaProgressBar>(parameters => parameters
            .Add(p => p.Title, "Ordens de Serviço")
            .Add(p => p.Used, 120)
            .Add(p => p.Max, 150));

        Assert.Contains("Ordens de Serviço", cut.Markup);
        Assert.Contains("120", cut.Markup);
        Assert.Contains("150", cut.Markup);
        Assert.Contains("80%", cut.Markup);
    }

    [Fact]
    public void PlanQuotaProgressBar_When_Unlimited_Should_Render_Badge()
    {
        var cut = Render<PlanQuotaProgressBar>(parameters => parameters
            .Add(p => p.Title, "Mensagens WhatsApp")
            .Add(p => p.Used, 45)
            .Add(p => p.Max, 0)); // 0 = Ilimitado

        Assert.Contains("Ilimitado", cut.Markup);
        Assert.Contains("45", cut.Markup);
    }

    [Fact]
    public void DelinquencyAlertBanner_When_GracePeriod_Should_Render_Warning()
    {
        var endsAt = DateTimeOffset.UtcNow.AddDays(3);
        var cut = Render<DelinquencyAlertBanner>(parameters => parameters
            .Add(p => p.Status, TenantSubscriptionStatus.GracePeriod)
            .Add(p => p.GracePeriodEndsAtUtc, endsAt));

        Assert.Contains("Período de Tolerância Ativo", cut.Markup);
        Assert.Contains(endsAt.ToString("dd/MM/yyyy"), cut.Markup);
    }

    [Fact]
    public void DelinquencyAlertBanner_When_Delinquent_Should_Render_Suspension()
    {
        var cut = Render<DelinquencyAlertBanner>(parameters => parameters
            .Add(p => p.Status, TenantSubscriptionStatus.Delinquent));

        Assert.Contains("Operação Suspensa por Inadimplência", cut.Markup);
    }

    [Fact]
    public void DelinquencyAlertBanner_When_Active_Should_Render_Nothing()
    {
        var cut = Render<DelinquencyAlertBanner>(parameters => parameters
            .Add(p => p.Status, TenantSubscriptionStatus.Active));

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void UpgradePlanModal_When_Closed_Should_Render_Nothing()
    {
        var cut = Render<UpgradePlanModal>(parameters => parameters
            .Add(p => p.IsOpen, false));

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void UpgradePlanModal_When_Open_Should_Render_Plans()
    {
        var plans = new List<SaasPlanDto>
        {
            new(SaasPlanTier.Basic, "Básico", "Desc", 149m, 150, 300, false, false, false, false, 3),
            new(SaasPlanTier.Pro, "Pro", "Desc", 299m, 600, 1500, true, true, true, true, 10),
            new(SaasPlanTier.Enterprise, "Enterprise", "Desc", 599m, 0, 0, true, true, true, true, 999)
        };

        var cut = Render<UpgradePlanModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.CurrentTier, SaasPlanTier.Basic)
            .Add(p => p.Plans, plans));

        Assert.Contains("Planos de Assinatura Lavaway", cut.Markup);
        Assert.Contains("Básico", cut.Markup);
        Assert.Contains("Pro", cut.Markup);
        Assert.Contains("Enterprise", cut.Markup);
        Assert.Contains("149", cut.Markup);
        Assert.Contains("299", cut.Markup);
        Assert.Contains("599", cut.Markup);
    }
}
