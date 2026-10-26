using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Client.Web.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class CookieConsentComponentTests : BunitContext
{
    private readonly InMemoryCookieConsentService _consentService;
    private readonly ToastService _toastService;

    public CookieConsentComponentTests()
    {
        _consentService = new InMemoryCookieConsentService();
        _toastService = new ToastService();

        Services.AddSingleton<ICookieConsentService>(_consentService);
        Services.AddSingleton<IToastService>(_toastService);
    }

    [Fact]
    public void CookieConsentBanner_WhenUserHasNotDecided_ShouldRenderBannerWithLgpdOptions()
    {
        var cut = Render<CookieConsentBanner>();

        Assert.NotNull(cut.Find("#cookie-consent-banner"));
        Assert.Contains("Privacidade", cut.Find(".cookie-badge-text").TextContent);
        Assert.Contains("LGPD", cut.Find(".cookie-badge-text").TextContent);
        Assert.Contains("Google Analytics", cut.Markup);
        Assert.Contains("Lei nº 13.709/2018", cut.Markup);

        var acceptBtn = cut.Find("#btn-cookie-accept-all");
        var rejectBtn = cut.Find("#btn-cookie-reject");
        var customizeBtn = cut.Find("#btn-cookie-customize");

        Assert.NotNull(acceptBtn);
        Assert.NotNull(rejectBtn);
        Assert.NotNull(customizeBtn);
    }

    [Fact]
    public async Task CookieConsentBanner_WhenUserClicksAcceptAll_ShouldGrantConsentAndHideBanner()
    {
        var cut = Render<CookieConsentBanner>();

        var acceptBtn = cut.Find("#btn-cookie-accept-all");
        acceptBtn.Click();

        var consent = await _consentService.GetConsentAsync();
        Assert.NotNull(consent);
        Assert.True(consent.Analytics);
        Assert.True(consent.Necessary);

        // O banner deve ser ocultado após a decisão
        Assert.Empty(cut.FindAll("#cookie-consent-banner"));
    }

    [Fact]
    public async Task CookieConsentBanner_WhenUserClicksNecessaryOnly_ShouldDenyAnalyticsAndHideBanner()
    {
        var cut = Render<CookieConsentBanner>();

        var rejectBtn = cut.Find("#btn-cookie-reject");
        rejectBtn.Click();

        var consent = await _consentService.GetConsentAsync();
        Assert.NotNull(consent);
        Assert.False(consent.Analytics);
        Assert.True(consent.Necessary);

        // O banner deve ser ocultado após a rejeição de não essenciais
        Assert.Empty(cut.FindAll("#cookie-consent-banner"));
    }

    [Fact]
    public async Task CookieConsentBanner_WhenCustomizing_CanToggleAnalyticsAndSave()
    {
        var cut = Render<CookieConsentBanner>();

        var customizeBtn = cut.Find("#btn-cookie-customize");
        customizeBtn.Click();

        // O painel de personalização deve estar visível com checkbox
        var customizePanel = cut.Find("#cookie-customize-panel");
        Assert.NotNull(customizePanel);
        Assert.Contains("1. Cookies Estritamente Necessários", customizePanel.TextContent);
        Assert.Contains("Sempre Ativo", customizePanel.TextContent);
        Assert.Contains("2. Cookies Analíticos (Google Analytics)", customizePanel.TextContent);
        Assert.Contains("G-FCCZJVXZG9", customizePanel.TextContent);

        // Marca a opção analítica
        var checkbox = cut.Find("#chk-cookie-analytics");
        checkbox.Change(true);

        var saveBtn = cut.Find("#btn-cookie-save-custom");
        saveBtn.Click();

        var consent = await _consentService.GetConsentAsync();
        Assert.NotNull(consent);
        Assert.True(consent.Analytics);
        Assert.Empty(cut.FindAll("#cookie-consent-banner"));
    }

    [Fact]
    public async Task CookieConsentBanner_WhenUserAlreadyDecided_ShouldNotRenderBanner()
    {
        await _consentService.SaveConsentAsync(true);

        var cut = Render<CookieConsentBanner>();

        Assert.Empty(cut.FindAll("#cookie-consent-banner"));
    }

    [Fact]
    public async Task PrivacyPolicyPage_WhenTogglingConsent_ShouldUpdateCookieConsentState()
    {
        var cut = Render<PrivacyPolicyPage>();

        // Inicialmente desativado (pois não houve consentimento prévio)
        Assert.Contains("Cookies Analíticos: DESATIVADOS", cut.Markup);

        var toggleBtn = cut.Find("#btn-toggle-analytics-consent");
        Assert.Contains("Habilitar Cookies Analíticos", toggleBtn.TextContent);

        // Clica para ativar
        toggleBtn.Click();

        var consent = await _consentService.GetConsentAsync();
        Assert.NotNull(consent);
        Assert.True(consent.Analytics);
        Assert.Contains("Cookies Analíticos: ATIVOS", cut.Markup);

        // Clica para revogar (Art. 8º, § 5º da LGPD)
        toggleBtn = cut.Find("#btn-toggle-analytics-consent");
        Assert.Contains("Revogar Consentimento", toggleBtn.TextContent);
        toggleBtn.Click();

        consent = await _consentService.GetConsentAsync();
        Assert.NotNull(consent);
        Assert.False(consent.Analytics);
        Assert.Contains("Cookies Analíticos: DESATIVADOS", cut.Markup);
    }
}
