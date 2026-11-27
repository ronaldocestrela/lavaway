using System.Text;
using CarWashSaaS.Api.Endpoints;
using CarWashSaaS.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CarWashSaaS.IntegrationTests;

public class PublicSeoEndpointsTests
{
    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seo:LandingTemplateUrl"] = "http://localhost/prerendered/index.html",
                ["Seo:LlmsTemplateUrl"] = "http://localhost/llms.txt"
            })
            .Build());
        services.AddScoped<PublicLandingPageService>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void MapPublicSeoEndpoints_RegistersAllSeoRoutes()
    {
        var app = new TestEndpointRouteBuilder(CreateServiceProvider());

        app.MapPublicSeoEndpoints();

        var endpoints = app.DataSources.SelectMany(ds => ds.Endpoints).OfType<RouteEndpoint>().ToList();

        var patterns = endpoints.Select(e => e.RoutePattern.RawText).ToList();

        Assert.Contains("/public/landing", patterns);
        Assert.Contains("/public/llms.txt", patterns);
        Assert.Contains("/llms.txt", patterns);
        Assert.Contains("/robots.txt", patterns);
        Assert.Contains("/sitemap.xml", patterns);
    }

    [Fact]
    public void MapPublicSeoEndpoints_RobotsAndSitemap_HaveAllowAnonymous()
    {
        var app = new TestEndpointRouteBuilder(CreateServiceProvider());

        app.MapPublicSeoEndpoints();

        var endpoints = app.DataSources.SelectMany(ds => ds.Endpoints).OfType<RouteEndpoint>().ToList();

        var robotsEndpoint = endpoints.FirstOrDefault(e => e.RoutePattern.RawText == "/robots.txt");
        var sitemapEndpoint = endpoints.FirstOrDefault(e => e.RoutePattern.RawText == "/sitemap.xml");
        var llmsEndpoint = endpoints.FirstOrDefault(e => e.RoutePattern.RawText == "/llms.txt");

        Assert.NotNull(robotsEndpoint);
        Assert.NotNull(sitemapEndpoint);
        Assert.NotNull(llmsEndpoint);

        // Verifica metadata AllowAnonymous
        Assert.Contains(robotsEndpoint.Metadata, m => m is Microsoft.AspNetCore.Authorization.IAllowAnonymous);
        Assert.Contains(sitemapEndpoint.Metadata, m => m is Microsoft.AspNetCore.Authorization.IAllowAnonymous);
        Assert.Contains(llmsEndpoint.Metadata, m => m is Microsoft.AspNetCore.Authorization.IAllowAnonymous);
    }

    private sealed class TestEndpointRouteBuilder : IEndpointRouteBuilder
    {
        public TestEndpointRouteBuilder(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public IServiceProvider ServiceProvider { get; }
        public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(ServiceProvider);
    }
}
