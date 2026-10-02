using Bunit;
using CarWashSaaS.Client.Components;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class BrandReceiptPreviewTests : BunitContext
{
    [Fact]
    public void Render_ShouldDisplayTradeNameAndFallbackLogo_WhenLogoUrlIsEmpty()
    {
        var cut = Render<BrandReceiptPreview>(parameters => parameters
            .Add(p => p.TradeName, "Lava Rápido Estrela")
            .Add(p => p.Phone, "(11) 98765-4321")
            .Add(p => p.Address, "Av. Paulista, 1000 - Bela Vista, SP")
            .Add(p => p.PrimaryColor, "#2563EB")
            .Add(p => p.SecondaryColor, "#0EA5E9"));

        Assert.Contains("Lava Rápido Estrela", cut.Markup);
        Assert.Contains("(11) 98765-4321", cut.Markup);
        Assert.Contains("Av. Paulista, 1000", cut.Markup);

        // Fallback logo should show initial letter
        var fallbackLogo = cut.Find(".brand-logo-fallback");
        Assert.NotNull(fallbackLogo);
        Assert.Equal("L", fallbackLogo.TextContent.Trim());
    }

    [Fact]
    public void Render_ShouldDisplayLogoImage_WhenLogoUrlIsProvided()
    {
        var cut = Render<BrandReceiptPreview>(parameters => parameters
            .Add(p => p.TradeName, "Lava Rápido Estrela")
            .Add(p => p.LogoUrl, "https://example.com/logo.png"));

        var logoImg = cut.Find("img.brand-logo-img");
        Assert.NotNull(logoImg);
        Assert.Equal("https://example.com/logo.png", logoImg.GetAttribute("src"));
    }

    [Fact]
    public void Render_ShouldApplyCustomBrandColors_AsCssVariables()
    {
        var cut = Render<BrandReceiptPreview>(parameters => parameters
            .Add(p => p.TradeName, "Lava Rápido Estrela")
            .Add(p => p.PrimaryColor, "#DC2626")
            .Add(p => p.SecondaryColor, "#F97316"));

        var container = cut.Find(".brand-receipt-preview-container");
        var style = container.GetAttribute("style");
        Assert.NotNull(style);
        Assert.Contains("--brand-primary: #DC2626", style);
        Assert.Contains("--brand-secondary: #F97316", style);
    }

    [Fact]
    public void SwitchView_ShouldToggleBetweenReceiptAndTrackingViews()
    {
        var cut = Render<BrandReceiptPreview>(parameters => parameters
            .Add(p => p.TradeName, "Lava Rápido Estrela"));

        // Default view is Receipt
        Assert.NotNull(cut.Find(".receipt-card-mockup"));

        // Click tracking tab
        var trackingTab = cut.Find("[data-testid='tab-tracking']");
        trackingTab.Click();

        // Should now show tracking view mockup
        Assert.NotNull(cut.Find(".tracking-card-mockup"));
        Assert.Contains("Acompanhamento do Veículo", cut.Markup);

        // Switch back to receipt
        var receiptTab = cut.Find("[data-testid='tab-receipt']");
        receiptTab.Click();

        Assert.NotNull(cut.Find(".receipt-card-mockup"));
        Assert.Contains("Comprovante de Atendimento", cut.Markup);
    }
}
