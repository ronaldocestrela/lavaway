using System.Net;
using Bunit;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Client.Web;
using CarWashSaaS.Client.Web.Pages;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class AuthenticationComponentTests : BunitContext
{
    private sealed class TestHostEnvironment : IWebAssemblyHostEnvironment
    {
        public string Environment { get; set; } = "Development";
        public string BaseAddress { get; set; } = "http://localhost/";
    }

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
                Content = new StringContent("{}")
            });
        }
    }

    private void SetupServices(string environment)
    {
        var env = new TestHostEnvironment { Environment = environment };
        Services.AddSingleton<IWebAssemblyHostEnvironment>(env);

        var tokenStorage = new FakeTokenStorage();
        Services.AddSingleton<ITokenStorage>(tokenStorage);
        Services.AddSingleton<JwtAuthenticationStateProvider>(sp => new JwtAuthenticationStateProvider(tokenStorage));

        var httpClient = new HttpClient(new FakeHttpMessageHandler())
        {
            BaseAddress = new Uri("http://localhost/")
        };
        Services.AddSingleton<AuthApiClient>(sp => new AuthApiClient(httpClient));

        var config = new ConfigurationBuilder().Build();
        Services.AddSingleton<IConfiguration>(config);
    }

    [Fact]
    public void Authentication_ShouldRenderDevQuickFill_WhenInDevelopmentEnvironment()
    {
        SetupServices("Development");

        var cut = Render<Authentication>(parameters => parameters
            .Add(p => p.Action, null));

        Assert.Contains("dev-quick-fill", cut.Markup);
        Assert.Contains("Credenciais de Teste / Dev:", cut.Markup);
        Assert.Contains("admin@lavaway.com", cut.Markup);
        Assert.Contains("recepcao@lavaway.com", cut.Markup);
        Assert.Contains("platform@lavaway.com", cut.Markup);
    }

    [Fact]
    public void Authentication_ShouldNotRenderDevQuickFill_WhenInProductionEnvironment()
    {
        SetupServices("Production");

        var cut = Render<Authentication>(parameters => parameters
            .Add(p => p.Action, null));

        Assert.DoesNotContain("dev-quick-fill", cut.Markup);
        Assert.DoesNotContain("quick-btn", cut.Markup);
        Assert.DoesNotContain("Credenciais de Teste / Dev:", cut.Markup);
        Assert.DoesNotContain("Admin Loja (admin@lavaway.com)", cut.Markup);
        Assert.DoesNotContain("recepcao@lavaway.com", cut.Markup);
        Assert.DoesNotContain("platform@lavaway.com", cut.Markup);
    }
}
