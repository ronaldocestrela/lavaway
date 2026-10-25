using System.Net;
using System.Reflection;
using Bunit;
using Bunit.TestDoubles;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Client.Web;
using CarWashSaaS.Client.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class PrivacyAndTermsComponentTests : BunitContext
{
    private sealed class FakeTokenStorage : ITokenStorage
    {
        public Task<string?> GetAccessTokenAsync() => Task.FromResult<string?>(null);
        public Task SetAccessTokenAsync(string token) => Task.CompletedTask;
        public Task<string?> GetRefreshTokenAsync() => Task.FromResult<string?>(null);
        public Task SetRefreshTokenAsync(string token) => Task.CompletedTask;
        public Task ClearAsync() => Task.CompletedTask;
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            });
        }
    }

    private void SetupHomeServices()
    {
        this.AddAuthorization();

        var tokenStorage = new FakeTokenStorage();
        Services.AddSingleton<ITokenStorage>(tokenStorage);
        var jwtProvider = new JwtAuthenticationStateProvider(tokenStorage);
        Services.AddSingleton(jwtProvider);

        var httpClient = new HttpClient(new FakeHttpMessageHandler())
        {
            BaseAddress = new Uri("http://localhost/")
        };
        Services.AddSingleton<AuthApiClient>(sp => new AuthApiClient(httpClient));
        Services.AddSingleton<SaasBillingApiClient>(sp => new SaasBillingApiClient(httpClient));

        var config = new ConfigurationBuilder().Build();
        Services.AddSingleton<IConfiguration>(config);
    }

    [Fact]
    public void PrivacyPolicyPage_ShouldNotHaveAuthorizeAttribute_AndShouldBePubliclyAccessible()
    {
        var authAttr = typeof(PrivacyPolicyPage).GetCustomAttribute<AuthorizeAttribute>();
        Assert.Null(authAttr);
    }

    [Fact]
    public void TermsOfServicePage_ShouldNotHaveAuthorizeAttribute_AndShouldBePubliclyAccessible()
    {
        var authAttr = typeof(TermsOfServicePage).GetCustomAttribute<AuthorizeAttribute>();
        Assert.Null(authAttr);
    }

    [Fact]
    public void PrivacyPolicyPage_ShouldRenderCompleteLgpdContent_WithDpoAndRoles()
    {
        var cut = Render<PrivacyPolicyPage>();

        // Título e identificação institucional
        Assert.Contains("Política de Privacidade", cut.Markup);
        Assert.Contains("LGPD", cut.Markup);

        // Duplo papel regulatório (Controladora vs Operadora)
        Assert.Contains("Controladora", cut.Markup);
        Assert.Contains("Operadora", cut.Markup);
        Assert.Contains("Lojista", cut.Markup);
        Assert.Contains("Motorista", cut.Markup);

        // Bases legais e finalidades
        Assert.Contains("Execução de Contrato", cut.Markup);
        Assert.Contains("Cumprimento de Obrigação Legal", cut.Markup);
        Assert.Contains("Legítimo Interesse", cut.Markup);

        // Cookies e LocalStorageTokenStorage
        Assert.Contains("LocalStorageTokenStorage", cut.Markup);
        Assert.Contains("Cookies", cut.Markup);

        // Canal do DPO / Encarregado (Art. 41)
        Assert.Contains("privacidade@lavaway.com.br", cut.Markup);
        Assert.Contains("Encarregado", cut.Markup);

        // Direitos dos Titulares (Art. 18)
        Assert.Contains("Art. 18", cut.Markup);
        Assert.Contains("Anonimização", cut.Markup);
        Assert.Contains("Portabilidade", cut.Markup);

        // Links de navegação e retorno
        var homeLink = cut.Find("a[href='/'], a[href='']");
        Assert.NotNull(homeLink);
    }

    [Fact]
    public void TermsOfServicePage_ShouldRenderCompleteClauses_WithB2bResponsibilities()
    {
        var cut = Render<TermsOfServicePage>();

        // Título e identificação
        Assert.Contains("Termos de Uso", cut.Markup);
        Assert.Contains("Lava Aê", cut.Markup);

        // Objeto e responsabilidades
        Assert.Contains("SaaS", cut.Markup);
        Assert.Contains("Responsabilidades do Estabelecimento", cut.Markup);
        Assert.Contains("WhatsApp", cut.Markup);

        // Planos, Faturamento e Rescisão
        Assert.Contains("Planos", cut.Markup);
        Assert.Contains("Cancelamento", cut.Markup);
        Assert.Contains("Propriedade Intelectual", cut.Markup);

        // Foro e legislação brasileira
        Assert.Contains("Legislação Aplicável", cut.Markup);

        // Links de navegação e retorno
        var homeLink = cut.Find("a[href='/'], a[href='']");
        Assert.NotNull(homeLink);
    }

    [Fact]
    public void HomePage_Footer_ShouldContainNavigableLinksToPrivacyAndTerms()
    {
        SetupHomeServices();

        var cut = Render<Home>();

        var termsLink = cut.Find("footer a[href='termos'], footer a[href='/termos']");
        var privacyLink = cut.Find("footer a[href='privacidade'], footer a[href='/privacidade']");

        Assert.NotNull(termsLink);
        Assert.Equal("Termos de Uso", termsLink.TextContent.Trim());

        Assert.NotNull(privacyLink);
        Assert.Contains("Privacidade", privacyLink.TextContent);
    }
}
