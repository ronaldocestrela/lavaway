using System.Security.Claims;
using CarWashSaaS.Api.Middleware;
using CarWashSaaS.Shared.Configuration;
using Microsoft.AspNetCore.Http;

namespace CarWashSaaS.IntegrationTests;

public sealed class TenantResolverMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ShouldResolveTenantFromAuthenticatedClaim()
    {
        var tenantId = Guid.NewGuid();
        var accessor = new CurrentTenantAccessor();
        var context = CreateAuthenticatedContext(new Claim("tenant_id", tenantId.ToString()));
        context.Request.Headers["tenant_id"] = Guid.NewGuid().ToString();
        var nextWasCalled = false;
        var middleware = new TenantResolverMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, accessor);

        Assert.True(nextWasCalled);
        Assert.Equal(tenantId, accessor.TenantId);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvokeAsync_ShouldRejectMalformedTenantClaim(string claimValue)
    {
        var accessor = new CurrentTenantAccessor();
        var context = CreateAuthenticatedContext(new Claim("tenant_id", claimValue));
        var nextWasCalled = false;
        var middleware = new TenantResolverMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, accessor);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextWasCalled);
        Assert.Null(accessor.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_ShouldRejectMissingOrAmbiguousTenantClaim()
    {
        foreach (var claims in new[]
                 {
                     Array.Empty<Claim>(),
                     new[] { new Claim("tenant_id", Guid.NewGuid().ToString()), new Claim("tenant_id", Guid.NewGuid().ToString()) }
                 })
        {
            var accessor = new CurrentTenantAccessor();
            var context = CreateAuthenticatedContext(claims);
            var middleware = new TenantResolverMiddleware(_ => Task.CompletedTask);

            await middleware.InvokeAsync(context, accessor);

            Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
            Assert.Null(accessor.TenantId);
        }
    }

    [Fact]
    public async Task InvokeAsync_ShouldAllowPlatformUserWithoutTenantClaim()
    {
        var accessor = new CurrentTenantAccessor();
        var context = CreateAuthenticatedContext(
            new Claim("user_realm", "Platform"),
            new Claim("is_platform_admin", "true"),
            new Claim("role", "SuperAdmin"));

        var nextWasCalled = false;
        var middleware = new TenantResolverMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, accessor);

        Assert.True(nextWasCalled);
        Assert.Null(accessor.TenantId);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ShouldResolveImpersonatedTenantWhenHeaderProvidedForPlatformUser()
    {
        var targetTenantId = Guid.NewGuid();
        var accessor = new CurrentTenantAccessor();
        var context = CreateAuthenticatedContext(
            new Claim("user_realm", "Platform"),
            new Claim("is_platform_admin", "true"));
        context.Request.Headers["X-Impersonate-Tenant-Id"] = targetTenantId.ToString();

        var nextWasCalled = false;
        var middleware = new TenantResolverMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, accessor);

        Assert.True(nextWasCalled);
        Assert.Equal(targetTenantId, accessor.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_ShouldIgnoreImpersonatedTenantHeader_WhenUserIsNotPlatformUser()
    {
        var legitimateTenantId = Guid.NewGuid();
        var maliciousTargetTenantId = Guid.NewGuid();
        var accessor = new CurrentTenantAccessor();
        var context = CreateAuthenticatedContext(
            new Claim("tenant_id", legitimateTenantId.ToString()),
            new Claim("role", "Administrator"));
        context.Request.Headers["X-Impersonate-Tenant-Id"] = maliciousTargetTenantId.ToString();

        var nextWasCalled = false;
        var middleware = new TenantResolverMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, accessor);

        Assert.True(nextWasCalled);
        Assert.Equal(legitimateTenantId, accessor.TenantId);
        Assert.NotEqual(maliciousTargetTenantId, accessor.TenantId);
    }

    private static DefaultHttpContext CreateAuthenticatedContext(params Claim[] claims)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        return context;
    }
}

