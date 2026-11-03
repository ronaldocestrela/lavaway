using CarWashSaaS.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace CarWashSaaS.IntegrationTests;

public sealed class AppUrlResolverTests
{
    [Fact]
    public void GetBaseUrl_Should_Return_Origin_Header_When_Present_In_HttpContext()
    {
        var configuration = new ConfigurationBuilder().Build();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Origin = "https://app.clientdomain.com";

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var resolver = new AppUrlResolver(configuration, accessor);

        var baseUrl = resolver.GetBaseUrl();
        var domain = resolver.GetDisplayDomain();

        Assert.Equal("https://app.clientdomain.com", baseUrl);
        Assert.Equal("app.clientdomain.com", domain);
    }

    [Fact]
    public void GetBaseUrl_Should_Return_Referer_Origin_When_Origin_Missing_In_HttpContext()
    {
        var configuration = new ConfigurationBuilder().Build();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Referer = "https://painel.meulavajato.com.br/reception?tab=yard";

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var resolver = new AppUrlResolver(configuration, accessor);

        var baseUrl = resolver.GetBaseUrl();
        var domain = resolver.GetDisplayDomain();

        Assert.Equal("https://painel.meulavajato.com.br", baseUrl);
        Assert.Equal("painel.meulavajato.com.br", domain);
    }

    [Fact]
    public void GetBaseUrl_Should_Return_Host_When_No_Origin_Or_Referer_In_HttpContext()
    {
        var configuration = new ConfigurationBuilder().Build();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("sistema.lavacar.com:8443");

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var resolver = new AppUrlResolver(configuration, accessor);

        var baseUrl = resolver.GetBaseUrl();
        var domain = resolver.GetDisplayDomain();

        Assert.Equal("https://sistema.lavacar.com:8443", baseUrl);
        Assert.Equal("sistema.lavacar.com:8443", domain);
    }

    [Fact]
    public void GetBaseUrl_Should_Fall_Back_To_Configuration_When_No_HttpContext()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["App:BaseUrl"] = "https://custom.appdomain.com.br"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var resolver = new AppUrlResolver(configuration, null);

        var baseUrl = resolver.GetBaseUrl();
        var domain = resolver.GetDisplayDomain();

        Assert.Equal("https://custom.appdomain.com.br", baseUrl);
        Assert.Equal("custom.appdomain.com.br", domain);
    }

    [Fact]
    public void GetBaseUrl_Should_Fall_Back_To_Default_When_No_HttpContext_And_No_Config()
    {
        var configuration = new ConfigurationBuilder().Build();
        var resolver = new AppUrlResolver(configuration, null);

        var baseUrl = resolver.GetBaseUrl();
        var domain = resolver.GetDisplayDomain();

        Assert.Equal(AppUrlResolver.DefaultAppUrl, baseUrl);
        Assert.Equal("app.lavaway.com.br", domain);
    }
}

