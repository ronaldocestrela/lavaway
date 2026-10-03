using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class ServiceEditorModalTests : BunitContext
{
    [Fact]
    public void Render_ShouldNotDisplayModal_WhenIsVisibleIsFalse()
    {
        var cut = Render<ServiceEditorModal>(parameters => parameters
            .Add(p => p.IsVisible, false));

        Assert.Empty(cut.FindAll(".modal-backdrop"));
    }

    [Fact]
    public void Render_ShouldDisplayServiceFieldsAndAllVehicleSizes_WhenIsVisibleIsTrue()
    {
        var model = ServiceFormModel.CreateDefault();

        var cut = Render<ServiceEditorModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Model, model));

        Assert.NotNull(cut.Find(".modal-backdrop"));
        Assert.NotNull(cut.Find("#service-name"));
        Assert.NotNull(cut.Find("#service-category"));

        // Verify that default categories from roadmap 2.3 are rendered
        foreach (var category in ServiceCategoryConstants.DefaultCategories)
        {
            Assert.Contains(category, cut.Markup);
        }

        // Verify that all 4 vehicle sizes are rendered
        foreach (var size in VehicleSizeConstants.All)
        {
            var displayName = VehicleSizeConstants.GetDisplayName(size);
            Assert.Contains(displayName, cut.Markup);
        }
    }

    [Fact]
    public void HandleSave_ShouldShowValidationError_WhenNameIsEmpty()
    {
        var model = ServiceFormModel.CreateDefault();
        model.Name = ""; // Invalid

        var cut = Render<ServiceEditorModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Model, model));

        var saveButton = cut.Find("button.btn-primary");
        saveButton.Click();

        var errorFeedback = cut.Find(".feedback-error");
        Assert.NotNull(errorFeedback);
        Assert.Contains("O nome do serviço é obrigatório", errorFeedback.TextContent);
    }

    [Fact]
    public void HandleSave_ShouldTriggerOnSaveCallback_WhenModelIsValid()
    {
        var model = ServiceFormModel.CreateDefault();
        model.Name = "Lavagem Completa Premium";
        model.Category = ServiceCategoryConstants.LavagemCompleta;

        ServiceFormModel? savedModel = null;

        var cut = Render<ServiceEditorModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Model, model)
            .Add(p => p.OnSave, m => savedModel = m));

        var saveButton = cut.Find("button.btn-primary");
        saveButton.Click();

        Assert.NotNull(savedModel);
        Assert.Equal("Lavagem Completa Premium", savedModel.Name);
        Assert.Equal(ServiceCategoryConstants.LavagemCompleta, savedModel.Category);
    }

    [Fact]
    public void HandleCancel_ShouldTriggerOnCancelCallback()
    {
        var wasCancelled = false;

        var cut = Render<ServiceEditorModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.OnCancel, () => wasCancelled = true));

        var cancelButton = cut.Find("button.btn-secondary");
        cancelButton.Click();

        Assert.True(wasCancelled);
    }

    [Fact]
    public void Render_ShouldNotDisplayErrorMessage_WhenErrorMessageIsNullOrWhiteSpace()
    {
        var model = ServiceFormModel.CreateDefault();

        var cut = Render<ServiceEditorModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Model, model)
            .Add(p => p.ErrorMessage, null));

        Assert.Empty(cut.FindAll(".feedback-error"));
    }

    [Fact]
    public void Render_ShouldDisplayErrorMessage_WhenErrorMessageIsProvided()
    {
        var model = ServiceFormModel.CreateDefault();
        const string expectedError = "Erro ao conectar com a API";

        var cut = Render<ServiceEditorModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Model, model)
            .Add(p => p.ErrorMessage, expectedError));

        var feedback = cut.Find(".feedback-error");
        Assert.NotNull(feedback);
        Assert.Contains(expectedError, feedback.TextContent);
    }
}
