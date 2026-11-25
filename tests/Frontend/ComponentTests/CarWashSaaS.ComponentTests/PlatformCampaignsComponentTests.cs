using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class PlatformCampaignsComponentTests : BunitContext
{
    private sealed class FakeToastService : IToastService
    {
        public event Action? OnChange;
        public IReadOnlyList<ToastItem> Toasts => [];
        public void Show(string message, ToastLevel level = ToastLevel.Info, string? title = null, int durationMs = 4500) { }
        public void ShowSuccess(string message, string? title = "Sucesso", int durationMs = 4000) { }
        public void ShowError(string message, string? title = "Erro", int durationMs = 5500) { }
        public void ShowWarning(string message, string? title = "Atenção", int durationMs = 4500) { }
        public void ShowInfo(string message, string? title = "Aviso", int durationMs = 4000) { }
        public void Dismiss(Guid id) { }
        public void Clear() { }
    }

    [Fact]
    public void CampaignEditorModal_WhenClosed_ShouldNotRender()
    {
        Services.AddSingleton<IToastService>(new FakeToastService());
        Services.AddSingleton(new PlatformCampaignsApiClient(new HttpClient()));

        var cut = Render<CampaignEditorModal>(parameters => parameters
            .Add(p => p.IsOpen, false));

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void CampaignEditorModal_WhenOpen_ShouldRenderFieldsAndPresets()
    {
        Services.AddSingleton<IToastService>(new FakeToastService());
        Services.AddSingleton(new PlatformCampaignsApiClient(new HttpClient()));

        var cut = Render<CampaignEditorModal>(parameters => parameters
            .Add(p => p.IsOpen, true));

        Assert.Contains("NOVA CAMPANHA", cut.Markup);
        Assert.Contains("Nome da Campanha", cut.Markup);
        Assert.Contains("Slug do Link Curto", cut.Markup);
        Assert.Contains("Atalhos rápidos:", cut.Markup);
        Assert.Contains("Parâmetros UTM", cut.Markup);
    }

    [Fact]
    public void CampaignQrCodeModal_WhenClosed_ShouldNotRender()
    {
        Services.AddSingleton<IToastService>(new FakeToastService());
        Services.AddSingleton(new PlatformCampaignsApiClient(new HttpClient()));

        var cut = Render<CampaignQrCodeModal>(parameters => parameters
            .Add(p => p.IsOpen, false));

        Assert.Empty(cut.Markup.Trim());
    }
}
