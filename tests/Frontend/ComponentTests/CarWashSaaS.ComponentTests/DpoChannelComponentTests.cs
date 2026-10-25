using System.Net;
using System.Reflection;
using Bunit;
using Bunit.TestDoubles;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Client.Web;
using CarWashSaaS.Client.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class DpoChannelComponentTests : BunitContext
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
    public void DpoChannelPage_ShouldNotHaveAuthorizeAttribute_AndShouldBePubliclyAccessible()
    {
        var authAttr = typeof(DpoChannelPage).GetCustomAttribute<AuthorizeAttribute>();
        Assert.Null(authAttr);
    }

    [Fact]
    public void DpoChannelPage_ShouldRenderDpoOfficialEmail_AndDeadlines()
    {
        var cut = Render<DpoChannelPage>();

        // Título e identificação do canal do Encarregado (Art. 41)
        Assert.Contains("Canal do Encarregado de Proteção de Dados", cut.Markup);
        Assert.Contains("Art. 41", cut.Markup);
        Assert.Contains("privacidade@lavaway.com.br", cut.Markup);

        // Prazos legais da ANPD
        Assert.Contains("15", cut.Markup); // Prazo de até 15 dias para declaração completa
        Assert.Contains("simplificada", cut.Markup, StringComparison.OrdinalIgnoreCase);

        // Direitos do Titular (Art. 18)
        Assert.Contains("Art. 18", cut.Markup);
        Assert.Contains("Acesso", cut.Markup);
        Assert.Contains("Anonimização", cut.Markup);
        Assert.Contains("Portabilidade", cut.Markup);
        Assert.Contains("Revogação", cut.Markup);

        // Distinção de papéis (Controladora vs Operadora)
        Assert.Contains("Controladora", cut.Markup);
        Assert.Contains("Operadora", cut.Markup);
        Assert.Contains("Lojista", cut.Markup);
        Assert.Contains("Motorista", cut.Markup);

        // Link de retorno para a Política de Privacidade e Página Inicial
        var privacyLink = cut.Find("a[href='privacidade'], a[href='/privacidade']");
        Assert.NotNull(privacyLink);
    }

    [Fact]
    public void DpoChannelPage_ShouldRenderInteractiveMailtoLinksForRights()
    {
        var cut = Render<DpoChannelPage>();

        // Deve conter links do tipo mailto estruturados para facilitar a solicitação do titular
        var mailtoLinks = cut.FindAll("a[href^='mailto:privacidade@lavaway.com.br']");
        Assert.NotEmpty(mailtoLinks);
    }
}
