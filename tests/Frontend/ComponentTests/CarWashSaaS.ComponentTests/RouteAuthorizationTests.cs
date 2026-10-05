using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Bunit;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Client.Web;
using CarWashSaaS.Client.Web.Layout;
using CarWashSaaS.Client.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class RouteAuthorizationTests : BunitContext
{
    private sealed class FakeTokenStorage : ITokenStorage
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public bool ClearCalled { get; private set; }

        public Task<string?> GetAccessTokenAsync() => Task.FromResult(AccessToken);
        public Task SetAccessTokenAsync(string token)
        {
            AccessToken = token;
            return Task.CompletedTask;
        }

        public Task<string?> GetRefreshTokenAsync() => Task.FromResult(RefreshToken);
        public Task SetRefreshTokenAsync(string token)
        {
            RefreshToken = token;
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            AccessToken = null;
            RefreshToken = null;
            ClearCalled = true;
            return Task.CompletedTask;
        }
    }

    private static string CreateJwt(Dictionary<string, object> payload)
    {
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var jsonPayload = JsonSerializer.Serialize(payload);
        var encodedPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonPayload)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var signature = "mock_signature";
        return $"{header}.{encodedPayload}.{signature}";
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldReturnAnonymous_WhenTokenIsEmpty()
    {
        var storage = new FakeTokenStorage { AccessToken = "" };
        var provider = new JwtAuthenticationStateProvider(storage);

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldReturnAnonymousAndClearStorage_WhenTokenIsMalformed()
    {
        var storage = new FakeTokenStorage { AccessToken = "not-a-valid-jwt-token" };
        var provider = new JwtAuthenticationStateProvider(storage);

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
        Assert.True(storage.ClearCalled);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldReturnAnonymousAndClearStorage_WhenTokenIsExpired()
    {
        var expiredTime = DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeSeconds();
        var token = CreateJwt(new Dictionary<string, object>
        {
            ["sub"] = Guid.NewGuid().ToString(),
            ["email"] = "admin@lavaway.com",
            ["role"] = "Administrator",
            ["exp"] = expiredTime
        });

        var storage = new FakeTokenStorage { AccessToken = token };
        var provider = new JwtAuthenticationStateProvider(storage);

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
        Assert.True(storage.ClearCalled);
        Assert.Null(storage.AccessToken);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldReturnAuthenticated_WhenTokenIsValidAndNotExpired()
    {
        var validFutureTime = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();
        var token = CreateJwt(new Dictionary<string, object>
        {
            ["sub"] = Guid.NewGuid().ToString(),
            ["email"] = "admin@lavaway.com",
            ["role"] = "Administrator",
            ["exp"] = validFutureTime
        });

        var storage = new FakeTokenStorage { AccessToken = token };
        var provider = new JwtAuthenticationStateProvider(storage);

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("admin@lavaway.com", state.User.Identity?.Name);
        Assert.True(state.User.IsInRole("Administrator"));
        Assert.False(storage.ClearCalled);
    }

    [Theory]
    [InlineData("http://localhost/reception", "http://localhost/login?returnUrl=reception")]
    [InlineData("http://localhost/yard", "http://localhost/login?returnUrl=yard")]
    [InlineData("http://localhost/customers", "http://localhost/login?returnUrl=customers")]
    [InlineData("http://localhost/scheduling", "http://localhost/login?returnUrl=scheduling")]
    [InlineData("http://localhost/after-sales", "http://localhost/login?returnUrl=after-sales")]
    [InlineData("http://localhost/cashier", "http://localhost/login?returnUrl=cashier")]
    [InlineData("http://localhost/loyalty", "http://localhost/login?returnUrl=loyalty")]
    [InlineData("http://localhost/subscriptions", "http://localhost/login?returnUrl=subscriptions")]
    [InlineData("http://localhost/settings/profile", "http://localhost/login?returnUrl=settings%2Fprofile")]
    [InlineData("http://localhost/settings/services", "http://localhost/login?returnUrl=settings%2Fservices")]
    [InlineData("http://localhost/settings/team", "http://localhost/login?returnUrl=settings%2Fteam")]
    [InlineData("http://localhost/settings/whatsapp", "http://localhost/login?returnUrl=settings%2Fwhatsapp")]
    public void RedirectToLogin_ShouldRedirectToLoginWithReturnUrl_WhenNavigatingProtectedRoutes(
        string currentUri,
        string expectedRedirectUri)
    {
        var navMan = Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo(currentUri);

        Render<RedirectToLogin>();

        Assert.Equal(expectedRedirectUri, navMan.Uri);
    }

    [Theory]
    [InlineData(typeof(ReceptionPage), "Administrator,Receptionist")]
    [InlineData(typeof(YardPage), "Administrator,Receptionist,Operator")]
    [InlineData(typeof(CustomersPage), "Administrator,Receptionist")]
    [InlineData(typeof(SchedulingPage), "Administrator,Receptionist")]
    [InlineData(typeof(AfterSalesPage), "Administrator,Receptionist")]
    [InlineData(typeof(CashierPage), "Administrator,Receptionist")]
    [InlineData(typeof(LoyaltyPage), "Administrator,Receptionist")]
    [InlineData(typeof(SubscriptionsPage), "Administrator,Receptionist")]
    [InlineData(typeof(StoreProfilePage), "Administrator")]
    [InlineData(typeof(ServicesPage), "Administrator")]
    [InlineData(typeof(CapacityAndTeamPage), "Administrator")]
    [InlineData(typeof(WhatsAppSettingsPage), "Administrator")]
    [InlineData(typeof(NewWorkOrderPage), "Administrator,Receptionist")]
    [InlineData(typeof(InspectionPage), "Administrator,Receptionist,Operator")]
    public void ProtectedPages_MustHaveAuthorizeAttributeWithProperRoles(Type pageType, string expectedRoles)
    {
        var authAttr = pageType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authAttr);
        Assert.Equal(expectedRoles, authAttr.Roles);
    }
}
