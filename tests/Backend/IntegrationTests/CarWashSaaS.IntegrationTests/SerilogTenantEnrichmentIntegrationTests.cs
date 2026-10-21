using System.Security.Claims;
using CarWashSaaS.Api.Middleware;
using CarWashSaaS.Shared.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace CarWashSaaS.IntegrationTests;

public sealed class SerilogTenantEnrichmentIntegrationTests
{
    [Fact]
    public async Task TenantResolverMiddleware_ShouldEnrichLogContextWithTenantIdAndUserId()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid().ToString();
        var sink = new TestMemorySink();

        using var serilogLogger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var loggerFactory = new LoggerFactory().AddSerilog(serilogLogger);
        var appLogger = loggerFactory.CreateLogger("TestModule");

        var accessor = new CurrentTenantAccessor();
        var context = CreateAuthenticatedContext(
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("sub", userId),
            new Claim("user_realm", "Shop"));

        var middleware = new TenantResolverMiddleware(_ =>
        {
            appLogger.LogInformation("Executando operacao de dominio/aplicacao no tenant");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, accessor);

        Assert.Equal(tenantId, accessor.TenantId);
        var emittedEvent = Assert.Single(sink.Events);
        Assert.True(emittedEvent.Properties.TryGetValue("TenantId", out var tenantProp));
        Assert.Equal(tenantId.ToString(), tenantProp.ToString().Trim('"'));

        Assert.True(emittedEvent.Properties.TryGetValue("UserId", out var userProp));
        Assert.Equal(userId, userProp.ToString().Trim('"'));

        Assert.True(emittedEvent.Properties.TryGetValue("UserRealm", out var realmProp));
        Assert.Equal("Shop", realmProp.ToString().Trim('"'));
    }

    [Fact]
    public async Task TenantResolverMiddleware_ShouldEnrichLogContextWithImpersonatedTenantId_ForPlatformUser()
    {
        var impersonatedTenantId = Guid.NewGuid();
        var platformUserId = Guid.NewGuid().ToString();
        var sink = new TestMemorySink();

        using var serilogLogger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var loggerFactory = new LoggerFactory().AddSerilog(serilogLogger);
        var appLogger = loggerFactory.CreateLogger("PlatformSupport");

        var accessor = new CurrentTenantAccessor();
        var context = CreateAuthenticatedContext(
            new Claim("user_realm", "Platform"),
            new Claim("is_platform_admin", "true"),
            new Claim("sub", platformUserId));
        context.Request.Headers["X-Impersonate-Tenant-Id"] = impersonatedTenantId.ToString();

        var middleware = new TenantResolverMiddleware(_ =>
        {
            appLogger.LogInformation("Auditoria ou suporte de plataforma em tenant impersonado");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, accessor);

        Assert.Equal(impersonatedTenantId, accessor.TenantId);
        var emittedEvent = Assert.Single(sink.Events);
        Assert.True(emittedEvent.Properties.TryGetValue("TenantId", out var tenantProp));
        Assert.Equal(impersonatedTenantId.ToString(), tenantProp.ToString().Trim('"'));

        Assert.True(emittedEvent.Properties.TryGetValue("UserRealm", out var realmProp));
        Assert.Equal("Platform", realmProp.ToString().Trim('"'));
    }

    [Fact]
    public async Task TenantResolverMiddleware_ShouldNotEnrichTenantId_WhenAnonymousOrUnauthenticated()
    {
        var sink = new TestMemorySink();

        using var serilogLogger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var loggerFactory = new LoggerFactory().AddSerilog(serilogLogger);
        var appLogger = loggerFactory.CreateLogger("AnonymousEndpoint");

        var accessor = new CurrentTenantAccessor();
        var context = new DefaultHttpContext();

        var middleware = new TenantResolverMiddleware(_ =>
        {
            appLogger.LogInformation("Consulta publica ou endpoint anonimo acessado");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, accessor);

        Assert.Null(accessor.TenantId);
        var emittedEvent = Assert.Single(sink.Events);
        Assert.False(emittedEvent.Properties.ContainsKey("TenantId"));
        Assert.False(emittedEvent.Properties.ContainsKey("UserId"));
    }

    private static DefaultHttpContext CreateAuthenticatedContext(params Claim[] claims)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        return context;
    }

    private sealed class TestMemorySink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }
}
