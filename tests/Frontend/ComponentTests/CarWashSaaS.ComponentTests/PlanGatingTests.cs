using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Client.Web.Layout;
using CarWashSaaS.Shared.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class PlanGatingTests : BunitContext
{
    private readonly TenantPlanStateService _planState = new(null!);

    public PlanGatingTests()
    {
        Services.AddSingleton<IToastService, ToastService>();
        Services.AddSingleton(_planState);
        Services.AddSingleton(new SaasBillingApiClient(new HttpClient()));
    }

    private static TenantSubscriptionOverviewDto CreateOverview(
        SaasPlanTier tier = SaasPlanTier.Basic,
        bool hasSubscriptions = false,
        bool hasLoyalty = false,
        bool hasCommissions = false,
        bool hasAiChatbot = false,
        int maxTeam = 3,
        int maxBoxes = 2)
    {
        return new TenantSubscriptionOverviewDto(
            TenantId: Guid.NewGuid(),
            TenantName: "Lava Jato Exemplo",
            PlanTier: tier,
            PlanName: tier.ToString(),
            MonthlyPrice: 149m,
            Status: TenantSubscriptionStatus.Active,
            CurrentPeriodStartUtc: DateTimeOffset.UtcNow.AddDays(-10),
            CurrentPeriodEndUtc: DateTimeOffset.UtcNow.AddDays(20),
            GracePeriodEndsAtUtc: null,
            NextBillingDateUtc: DateTimeOffset.UtcNow.AddDays(20),
            UsedWorkOrders: 10,
            MaxWorkOrders: 150,
            UsedWhatsAppMessages: 20,
            MaxWhatsAppMessages: 300,
            GatewaySubscriptionId: null,
            PendingInvoice: null,
            Invoices: [],
            HasCustomerSubscriptions: hasSubscriptions,
            HasLoyalty: hasLoyalty,
            HasCommissions: hasCommissions,
            HasAiChatbot: hasAiChatbot,
            MaxTeamMembers: maxTeam,
            MaxBoxes: maxBoxes);
    }

    [Fact]
    public void PlanGate_When_Allowed_Should_Render_ChildContent()
    {
        var cut = Render<PlanGate>(parameters => parameters
            .Add(p => p.IsAllowed, true)
            .Add(p => p.FeatureTitle, "Clube de Assinaturas")
            .AddChildContent("<p id=\"inner-content\">Conteúdo Liberado</p>"));

        Assert.Contains("Conteúdo Liberado", cut.Markup);
        Assert.DoesNotContain("Recurso Exclusivo", cut.Markup);
    }

    [Fact]
    public void PlanGate_When_Not_Allowed_Should_Render_UpgradePrompt()
    {
        var cut = Render<PlanGate>(parameters => parameters
            .Add(p => p.IsAllowed, false)
            .Add(p => p.FeatureTitle, "Clube de Assinaturas")
            .Add(p => p.FeatureDescription, "Fidelize seus clientes com mensalidades recorrentes.")
            .Add(p => p.RequiredPlanName, "Plano Pro")
            .AddChildContent("<p id=\"inner-content\">Conteúdo Secreto</p>"));

        Assert.DoesNotContain("Conteúdo Secreto", cut.Markup);
        Assert.Contains("Clube de Assinaturas", cut.Markup);
        Assert.Contains("Plano Pro", cut.Markup);
        Assert.Contains("Fidelize seus clientes com mensalidades recorrentes.", cut.Markup);
        Assert.Contains("Fazer Upgrade Agora", cut.Markup);
    }

    [Fact]
    public void PlanGate_When_Click_Upgrade_Should_Open_UpgradeModal()
    {
        var cut = Render<PlanGate>(parameters => parameters
            .Add(p => p.IsAllowed, false)
            .Add(p => p.FeatureTitle, "Programa de Fidelidade")
            .Add(p => p.RequiredPlanName, "Plano Pro"));

        var btn = cut.Find("button.btn-upgrade-gate");
        btn.Click();

        // O modal de upgrade deve abrir
        Assert.NotNull(cut.FindComponent<UpgradePlanModal>());
    }

    [Fact]
    public void TenantPlanStateService_Properties_Should_Reflect_Overview()
    {
        var overview = CreateOverview(
            tier: SaasPlanTier.Pro,
            hasSubscriptions: true,
            hasLoyalty: true,
            hasCommissions: true,
            hasAiChatbot: true,
            maxTeam: 10,
            maxBoxes: 5);

        var service = new TenantPlanStateService(null!);
        var eventTriggered = false;
        service.OnChange += () => eventTriggered = true;

        service.SetOverviewForTesting(overview);

        Assert.True(eventTriggered);
        Assert.True(service.IsLoaded);
        Assert.True(service.HasCustomerSubscriptions);
        Assert.True(service.HasLoyalty);
        Assert.True(service.HasCommissions);
        Assert.True(service.HasAiChatbot);
        Assert.Equal(10, service.MaxTeamMembers);
        Assert.Equal(5, service.MaxBoxes);
        Assert.Equal(SaasPlanTier.Pro, service.CurrentTier);
        Assert.True(service.HasFeature(SaasFeature.CustomerSubscriptions));
        Assert.True(service.HasFeature(SaasFeature.Loyalty));
        Assert.True(service.HasFeature(SaasFeature.Commissions));
        Assert.True(service.HasFeature(SaasFeature.AiChatbot));

        // Teste de Reset
        service.Reset();
        Assert.False(service.IsLoaded);
        Assert.Null(service.Overview);
    }

    [Fact]
    public void NavMenu_When_BasicPlan_Should_Render_ProBadges()
    {
        _planState.SetOverviewForTesting(CreateOverview(
            tier: SaasPlanTier.Basic,
            hasSubscriptions: false,
            hasLoyalty: false));

        var auth = this.AddAuthorization();
        auth.SetAuthorized("admin@lavaway.com");
        auth.SetRoles("Administrator");

        var cut = Render<NavMenu>();

        Assert.Contains("🔒 PRO", cut.Markup);
    }

    [Fact]
    public void NavMenu_When_ProPlan_Should_Not_Render_ProBadges()
    {
        _planState.SetOverviewForTesting(CreateOverview(
            tier: SaasPlanTier.Pro,
            hasSubscriptions: true,
            hasLoyalty: true));

        var auth = this.AddAuthorization();
        auth.SetAuthorized("admin@lavaway.com");
        auth.SetRoles("Administrator");

        var cut = Render<NavMenu>();

        Assert.DoesNotContain("🔒 PRO", cut.Markup);
    }

    [Fact]
    public void YardCapacityCard_When_MaxAllowedBoxes_Is_Set_Should_Display_Plan_Limit()
    {
        var capacity = new YardCapacityDto(
            Id: Guid.NewGuid(),
            TotalBoxes: 2,
            Description: "Pátio Principal");

        var cut = Render<YardCapacityCard>(parameters => parameters
            .Add(p => p.Capacity, capacity)
            .Add(p => p.MaxAllowedBoxes, 2)
            .Add(p => p.PlanName, "Básico"));

        // Clicar em alterar capacidade para validar hints
        var editBtn = cut.Find("#btn-edit-capacity");
        editBtn.Click();

        Assert.Contains("Capacidade de 1 a 2 boxes simultâneos no Plano Básico", cut.Markup);
        var input = cut.Find("input#capacity-boxes");
        Assert.Equal("2", input.GetAttribute("max"));
    }
}
