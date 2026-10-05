using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Shared.Contracts;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class SubscriptionComponentsTests : BunitContext
{
    [Fact]
    public void CreateSubscriptionPlanModal_WhenClosed_ShouldNotRenderBackdrop()
    {
        var cut = Render<CreateSubscriptionPlanModal>(parameters => parameters
            .Add(p => p.IsOpen, false));

        Assert.Empty(cut.FindAll(".modal-backdrop"));
    }

    [Fact]
    public void CreateSubscriptionPlanModal_WhenOpen_ShouldRenderFieldsAndTriggerCreate()
    {
        CreateSubscriptionPlanRequest? captured = null;

        var cut = Render<CreateSubscriptionPlanModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.OnCreate, EventCallback.Factory.Create<CreateSubscriptionPlanRequest>(this, req => captured = req)));

        Assert.NotEmpty(cut.FindAll(".modal-backdrop"));
        Assert.Contains("Novo Plano de Assinatura", cut.Markup);

        cut.Find("#plan-name").Change("Plano Diamante");
        cut.Find("#plan-price").Change("149.90");
        cut.Find("#plan-credits").Change("5");
        cut.Find("#plan-plates").Change("2");

        cut.Find(".btn-primary").Click();

        Assert.NotNull(captured);
        Assert.Equal("Plano Diamante", captured.Name);
        Assert.Equal(149.90m, captured.MonthlyPrice);
        Assert.Equal(5, captured.CreditsPerCycle);
        Assert.Equal(2, captured.AllowedPlatesLimit);
    }

    [Fact]
    public void SubscribeCustomerModal_WhenOpen_ShouldRenderAvailablePlansAndValidatePlates()
    {
        SubscribeCustomerRequest? captured = null;
        var plan = new SubscriptionPlanDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Plano Mensal 2 Lavagens",
            "Desc",
            99.90m,
            30,
            2,
            1,
            true,
            DateTimeOffset.UtcNow);

        var cut = Render<SubscribeCustomerModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.AvailablePlans, [plan])
            .Add(p => p.OnSubscribed, EventCallback.Factory.Create<SubscribeCustomerRequest>(this, req => captured = req)));

        Assert.Contains("Plano Mensal 2 Lavagens", cut.Markup);

        cut.Find("#sub-cust-name").Change("Arthur Dent");
        cut.Find("#sub-cust-phone").Change("11999998888");
        cut.Find("#sub-plan").Change(plan.Id.ToString());
        cut.Find("#sub-plates").Change("ABC1D23");

        cut.Find(".btn-success").Click();

        Assert.NotNull(captured);
        Assert.Equal("Arthur Dent", captured.CustomerName);
        Assert.Equal(plan.Id, captured.PlanId);
        Assert.Single(captured.InitialPlates);
        Assert.Equal("ABC1D23", captured.InitialPlates[0]);
    }

    [Fact]
    public void SubscriptionUsageHistoryModal_ShouldRenderUsages()
    {
        var usage = new SubscriptionUsageDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "XYZ9876",
            "Lavagem Geral",
            DateTimeOffset.UtcNow,
            "Sem observações");

        var cut = Render<SubscriptionUsageHistoryModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.CustomerName, "Rodrigo Silva")
            .Add(p => p.PlanName, "Plano Prata")
            .Add(p => p.Usages, [usage]));

        Assert.Contains("Rodrigo Silva", cut.Markup);
        Assert.Contains("Plano Prata", cut.Markup);
        Assert.Contains("XYZ9876", cut.Markup);
        Assert.Contains("Lavagem Geral", cut.Markup);
    }

    [Fact]
    public void ManageSubscriptionPlatesModal_ShouldRenderAuthorizedPlatesChips()
    {
        string? removedPlate = null;

        var cut = Render<ManageSubscriptionPlatesModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.CustomerName, "Cliente Teste")
            .Add(p => p.AllowedLimit, 2)
            .Add(p => p.AuthorizedPlates, ["ABC1D23"])
            .Add(p => p.OnRemovePlate, EventCallback.Factory.Create<string>(this, plate => removedPlate = plate)));

        Assert.Contains("ABC1D23", cut.Markup);
        Assert.Contains("(1 / 2)", cut.Markup);

        cut.Find(".chip-remove").Click();
        Assert.Equal("ABC1D23", removedPlate);
    }
}
