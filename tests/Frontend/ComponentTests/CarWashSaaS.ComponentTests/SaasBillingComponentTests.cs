using System.Net;
using System.Text.Json;
using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Shared.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class SaasBillingComponentTests : BunitContext
{
    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
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

        Assert.Contains("Planos de Assinatura Lava Aê", cut.Markup);
        Assert.Contains("Básico", cut.Markup);
        Assert.Contains("Pro", cut.Markup);
        Assert.Contains("Enterprise", cut.Markup);
        Assert.Contains("149", cut.Markup);
        Assert.Contains("299", cut.Markup);
        Assert.Contains("599", cut.Markup);
    }

    [Fact]
    public void EditSaasPlanModal_WhenClosed_ShouldRenderNothing()
    {
        var cut = Render<EditSaasPlanModal>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.Plan, null));

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void EditSaasPlanModal_WhenOpen_ShouldRenderPlanDetailsAndBoxesAndTeamMembers()
    {
        var plan = new SaasPlanDto(
            Tier: SaasPlanTier.Pro,
            Name: "Plano Pro Estética",
            Description: "Ideal para estética",
            MonthlyPrice: 299.90m,
            MaxWorkOrdersPerCycle: 600,
            MaxWhatsAppMessagesPerCycle: 1500,
            HasCustomerSubscriptions: true,
            HasLoyalty: true,
            HasCommissions: true,
            HasAiChatbot: true,
            MaxTeamMembers: 12,
            MaxBoxes: 6,
            IsActive: true);

        var cut = Render<EditSaasPlanModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Plan, plan));

        Assert.Contains("Gerenciar Plano SaaS: Plano Pro Estética", cut.Markup);
        Assert.Contains("Tamanho Máximo da Equipe", cut.Markup);
        Assert.Contains("Quantidade de Boxes de Atendimento", cut.Markup);
        Assert.Contains("Funções &amp; Módulos Habilitados no Plano", cut.Markup);
        Assert.Contains("Clube de Assinaturas", cut.Markup);
        Assert.Contains("Programa de Fidelidade", cut.Markup);
        Assert.Contains("Rateio &amp; Comissões", cut.Markup);
        Assert.Contains("Chatbot com IA no WhatsApp", cut.Markup);
    }

    [Fact]
    public async Task EditSaasPlanModal_WhenSaveClicked_ShouldTriggerOnSaveWithUpdatedRequest()
    {
        var plan = new SaasPlanDto(
            Tier: SaasPlanTier.Basic,
            Name: "Básico",
            Description: "Entrada",
            MonthlyPrice: 149m,
            MaxWorkOrdersPerCycle: 150,
            MaxWhatsAppMessagesPerCycle: 300,
            HasCustomerSubscriptions: false,
            HasLoyalty: false,
            HasCommissions: false,
            HasAiChatbot: false,
            MaxTeamMembers: 3,
            MaxBoxes: 2,
            IsActive: true);

        UpdateSaasPlanRequest? savedRequest = null;

        var cut = Render<EditSaasPlanModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Plan, plan)
            .Add(p => p.OnSave, (UpdateSaasPlanRequest req) => { savedRequest = req; }));

        var saveButton = cut.Find("#btn-save-plan");
        await saveButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.NotNull(savedRequest);
        Assert.Equal("Básico", savedRequest.Name);
        Assert.Equal(149m, savedRequest.MonthlyPrice);
        Assert.Equal(3, savedRequest.MaxTeamMembers);
        Assert.Equal(2, savedRequest.MaxBoxes);
    }

    [Fact]
    public void UpgradePlanModal_When_Open_Should_Render_Stripe_Checkout_Button()
    {
        var plans = new List<SaasPlanDto>
        {
            new(SaasPlanTier.Basic, "Basic", "Básico", 99m, 150, 300, false, false, false, false, 3),
            new(SaasPlanTier.Pro, "Pro", "Profissional", 199m, 600, 1500, true, true, true, true, 10)
        };

        var cut = Render<UpgradePlanModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.CurrentTier, SaasPlanTier.Basic)
            .Add(p => p.Plans, plans));

        Assert.Contains("Contratar no Cartão (Stripe)", cut.Markup);
    }

    [Fact]
    public async Task UpgradePlanModal_When_StripeCheckoutClicked_Should_Trigger_OnConfirmCheckout()
    {
        var plans = new List<SaasPlanDto>
        {
            new(SaasPlanTier.Basic, "Basic", "Básico", 99m, 150, 300, false, false, false, false, 3),
            new(SaasPlanTier.Pro, "Pro", "Profissional", 199m, 600, 1500, true, true, true, true, 10)
        };

        SaasPlanTier? checkoutTier = null;

        var cut = Render<UpgradePlanModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.CurrentTier, SaasPlanTier.Basic)
            .Add(p => p.Plans, plans)
            .Add(p => p.OnConfirmCheckout, (SaasPlanTier tier) => { checkoutTier = tier; }));

        var stripeButton = cut.Find(".btn-modal-stripe-checkout");
        await stripeButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Equal(SaasPlanTier.Pro, checkoutTier);
    }

    [Fact]
    public void OverrideTenantPlanModal_WhenClosed_ShouldRenderNothing()
    {
        Services.AddSingleton<IToastService>(new ToastService());
        var httpClient = new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        Services.AddSingleton(new SaasBillingApiClient(httpClient));

        var cut = Render<OverrideTenantPlanModal>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.TenantId, Guid.NewGuid()));

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void OverrideTenantPlanModal_WhenOpen_ShouldRenderTenantNameAndCurrentPlan()
    {
        Services.AddSingleton<IToastService>(new ToastService());
        var httpClient = new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
        }))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        Services.AddSingleton(new SaasBillingApiClient(httpClient));

        var tenantId = Guid.NewGuid();
        var cut = Render<OverrideTenantPlanModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.TenantId, tenantId)
            .Add(p => p.TenantName, "Lava-Jato Jardins")
            .Add(p => p.CurrentTier, SaasPlanTier.Pro)
            .Add(p => p.CurrentStatus, TenantSubscriptionStatus.Active));

        Assert.Contains("Modificar Plano do Estabelecimento", cut.Markup);
        Assert.Contains("Lava-Jato Jardins", cut.Markup);
        Assert.Contains("PRO", cut.Markup);
        Assert.Contains("Active", cut.Markup);
        Assert.NotNull(cut.Find("#newPlanTier"));
        Assert.NotNull(cut.Find("#overrideReason"));
        Assert.NotNull(cut.Find("#btn-save-override-plan"));
    }

    [Fact]
    public async Task OverrideTenantPlanModal_WhenReasonIsEmpty_ShouldShowValidationError()
    {
        Services.AddSingleton<IToastService>(new ToastService());
        var httpClient = new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
        }))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        Services.AddSingleton(new SaasBillingApiClient(httpClient));

        var tenantId = Guid.NewGuid();
        var cut = Render<OverrideTenantPlanModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.TenantId, tenantId)
            .Add(p => p.TenantName, "Lava-Jato Jardins")
            .Add(p => p.CurrentTier, SaasPlanTier.Basic));

        var saveButton = cut.Find("#btn-save-override-plan");
        await saveButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("O motivo/justificativa é obrigatório", cut.Markup);
    }

    [Fact]
    public async Task OverrideTenantPlanModal_WhenValidSubmission_ShouldCallApiAndTriggerCallback()
    {
        var toastService = new ToastService();
        Services.AddSingleton<IToastService>(toastService);

        var tenantId = Guid.NewGuid();
        var overviewResponse = new TenantSubscriptionOverviewDto(
            TenantId: tenantId,
            TenantName: "Lava-Jato Jardins",
            PlanTier: SaasPlanTier.Enterprise,
            PlanName: "Enterprise",
            MonthlyPrice: 599m,
            Status: TenantSubscriptionStatus.Active,
            CurrentPeriodStartUtc: DateTimeOffset.UtcNow,
            CurrentPeriodEndUtc: DateTimeOffset.UtcNow.AddMonths(1),
            GracePeriodEndsAtUtc: null,
            NextBillingDateUtc: DateTimeOffset.UtcNow.AddMonths(1),
            UsedWorkOrders: 0,
            MaxWorkOrders: 0,
            UsedWhatsAppMessages: 0,
            MaxWhatsAppMessages: 0,
            GatewaySubscriptionId: null,
            PendingInvoice: null,
            Invoices: []);

        var httpClient = new HttpClient(new FakeHttpMessageHandler(req =>
        {
            if (req.Method == HttpMethod.Post && req.RequestUri!.ToString().Contains($"subscriptions/{tenantId}/override"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(overviewResponse), System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
            };
        }))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        Services.AddSingleton(new SaasBillingApiClient(httpClient));

        TenantSubscriptionOverviewDto? receivedOverview = null;

        var cut = Render<OverrideTenantPlanModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.TenantId, tenantId)
            .Add(p => p.TenantName, "Lava-Jato Jardins")
            .Add(p => p.CurrentTier, SaasPlanTier.Basic)
            .Add(p => p.OnPlanOverridden, (TenantSubscriptionOverviewDto dto) => { receivedOverview = dto; }));

        var textarea = cut.Find("#overrideReason");
        textarea.Change("Upgrade manual autorizado pelo setor de faturamento");

        var saveButton = cut.Find("#btn-save-override-plan");
        await saveButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.NotNull(receivedOverview);
        Assert.Equal(SaasPlanTier.Enterprise, receivedOverview.PlanTier);
        Assert.Equal("Enterprise", receivedOverview.PlanName);
    }
}

