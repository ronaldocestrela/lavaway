using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Client.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class ToastComponentTests : BunitContext
{
    [Fact]
    public void ToastService_ShouldAddToastAndTriggerOnChange_WhenShowSuccessCalled()
    {
        using var toastService = new ToastService();
        var changeCount = 0;
        toastService.OnChange += () => changeCount++;

        toastService.ShowSuccess("Operação realizada com sucesso!");

        Assert.Single(toastService.Toasts);
        var toast = toastService.Toasts[0];
        Assert.Equal("Operação realizada com sucesso!", toast.Message);
        Assert.Equal(ToastLevel.Success, toast.Level);
        Assert.Equal("Sucesso", toast.Title);
        Assert.True(changeCount > 0);
    }

    [Fact]
    public void ToastService_ShouldAddVariousLevels_Correctly()
    {
        using var toastService = new ToastService();

        toastService.ShowError("Erro ao processar", "Falha Crítica");
        toastService.ShowWarning("Atenção aos detalhes");
        toastService.ShowInfo("Informativo do sistema");

        Assert.Equal(3, toastService.Toasts.Count);
        Assert.Equal(ToastLevel.Error, toastService.Toasts[0].Level);
        Assert.Equal("Falha Crítica", toastService.Toasts[0].Title);

        Assert.Equal(ToastLevel.Warning, toastService.Toasts[1].Level);
        Assert.Equal("Atenção", toastService.Toasts[1].Title);

        Assert.Equal(ToastLevel.Info, toastService.Toasts[2].Level);
        Assert.Equal("Aviso", toastService.Toasts[2].Title);
    }

    [Fact]
    public void ToastService_ShouldRemoveToast_WhenDismissCalled()
    {
        using var toastService = new ToastService();
        toastService.ShowSuccess("Mensagem 1");
        toastService.ShowError("Mensagem 2");

        var firstId = toastService.Toasts[0].Id;
        toastService.Dismiss(firstId);

        Assert.Single(toastService.Toasts);
        Assert.Equal("Mensagem 2", toastService.Toasts[0].Message);
    }

    [Fact]
    public void ToastService_ShouldClearAllToasts_WhenClearCalled()
    {
        using var toastService = new ToastService();
        toastService.ShowSuccess("Mensagem 1");
        toastService.ShowSuccess("Mensagem 2");

        toastService.Clear();

        Assert.Empty(toastService.Toasts);
    }

    [Fact]
    public void ToastContainer_ShouldRenderToasts_WithCorrectMarkupAndClasses()
    {
        var toastService = new ToastService();
        Services.AddSingleton<IToastService>(toastService);

        var cut = Render<ToastContainer>();
        Assert.Empty(cut.FindAll(".toast-card"));

        cut.InvokeAsync(() => toastService.ShowSuccess("Veículo liberado!"));

        var cards = cut.FindAll(".toast-card");
        Assert.Single(cards);
        Assert.Contains("toast-success", cards[0].ClassName);
        Assert.Contains("Veículo liberado!", cards[0].TextContent);
        Assert.NotNull(cut.Find(".toast-progress-fill"));
    }

    [Fact]
    public void ToastContainer_ShouldDismiss_WhenCloseButtonClicked()
    {
        var toastService = new ToastService();
        Services.AddSingleton<IToastService>(toastService);

        var cut = Render<ToastContainer>();

        cut.InvokeAsync(() => toastService.ShowError("Falha na operação", "Atenção"));

        var closeBtn = cut.Find("button.toast-close-btn");
        Assert.NotNull(closeBtn);

        cut.InvokeAsync(() => closeBtn.Click());

        Assert.Empty(cut.FindAll(".toast-card"));
        Assert.Empty(toastService.Toasts);
    }
}
