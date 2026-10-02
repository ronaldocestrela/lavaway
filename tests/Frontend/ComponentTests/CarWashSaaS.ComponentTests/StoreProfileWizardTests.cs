using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Client.Core;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class StoreProfileWizardTests : BunitContext
{
    [Fact]
    public void InitialRender_ShouldShowStep1_WithIdentificationFields()
    {
        var model = new StoreProfileFormModel();

        var cut = Render<StoreProfileWizard>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.IsEditMode, false));

        var step1Panel = cut.Find("[data-testid='step-1-panel']");
        Assert.NotNull(step1Panel);
        Assert.NotNull(cut.Find("#legalName"));
        Assert.NotNull(cut.Find("#tradeName"));
        Assert.NotNull(cut.Find("#cnpj"));
    }

    [Fact]
    public void NextStep_ShouldShowErrorMessage_WhenStep1IsInvalid()
    {
        var model = new StoreProfileFormModel();

        var cut = Render<StoreProfileWizard>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.IsEditMode, false));

        var nextButton = cut.Find("button.button-primary");
        nextButton.Click();

        var feedback = cut.Find(".feedback-error");
        Assert.NotNull(feedback);
        Assert.Contains("Razão Social é obrigatória", feedback.TextContent);
    }

    [Fact]
    public void NextStep_ShouldAdvanceToStep2_WhenStep1IsValid()
    {
        var model = new StoreProfileFormModel
        {
            LegalName = "LavaWay Centro Ltda",
            TradeName = "LavaWay Centro",
            Cnpj = "11.222.333/0001-81"
        };

        var cut = Render<StoreProfileWizard>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.IsEditMode, false));

        var nextButton = cut.Find("button.button-primary");
        nextButton.Click();

        var step2Panel = cut.Find("[data-testid='step-2-panel']");
        Assert.NotNull(step2Panel);
        Assert.NotNull(cut.Find("#phone"));
        Assert.NotNull(cut.Find("#postalCode"));
        Assert.NotNull(cut.Find("#street"));
    }

    [Fact]
    public void PrevStep_ShouldReturnToStep1_PreservingData()
    {
        var model = new StoreProfileFormModel
        {
            LegalName = "LavaWay Centro Ltda",
            TradeName = "LavaWay Centro",
            Cnpj = "11.222.333/0001-81",
            Phone = "(11) 99999-9999"
        };

        var cut = Render<StoreProfileWizard>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.IsEditMode, false));

        // Advance to step 2
        cut.Find("button.button-primary").Click();
        Assert.NotNull(cut.Find("[data-testid='step-2-panel']"));

        // Click Back
        cut.Find("button.button-secondary").Click();
        Assert.NotNull(cut.Find("[data-testid='step-1-panel']"));

        var legalNameInput = cut.Find("#legalName");
        Assert.Equal("LavaWay Centro Ltda", legalNameInput.GetAttribute("value"));
    }

    [Fact]
    public void Submit_ShouldInvokeOnSaveCallback_WhenAllDataIsValid()
    {
        var model = new StoreProfileFormModel
        {
            LegalName = "LavaWay Centro Ltda",
            TradeName = "LavaWay Centro",
            Cnpj = "11.222.333/0001-81",
            Phone = "(11) 99999-9999",
            PostalCode = "01000-000",
            Street = "Rua Augusta, 100",
            City = "São Paulo",
            State = "SP"
        };

        StoreProfileFormModel? savedModel = null;

        var cut = Render<StoreProfileWizard>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.IsEditMode, false)
            .Add(p => p.OnSave, EventCallback.Factory.Create<StoreProfileFormModel>(this, m => savedModel = m)));

        // Step 1 -> Step 2
        cut.Find("button.button-primary").Click();

        // Step 2 -> Step 3
        cut.Find("button.button-primary").Click();

        var step3Panel = cut.Find("[data-testid='step-3-panel']");
        Assert.NotNull(step3Panel);

        // Click submit
        cut.Find("button.button-save").Click();

        Assert.NotNull(savedModel);
        Assert.Equal("LavaWay Centro Ltda", savedModel.LegalName);
        Assert.Equal("11.222.333/0001-81", savedModel.Cnpj);
    }
}
