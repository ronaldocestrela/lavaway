using System.Net;
using System.Text;
using System.Text.Json;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.UnitTests.Frontend;

public sealed class AuthApiClientTests
{
    [Fact]
    public async Task LoginAsync_WhenSuccessful_ShouldReturnSuccessWithTokens()
    {
        var expectedResponse = new AuthTokenResponse(
            "test_access_token",
            "test_refresh_token",
            3600,
            "Bearer",
            Guid.NewGuid(),
            "admin@lavaway.com",
            Guid.NewGuid(),
            "Administrator",
            ["ConfigureStore", "ManageUsers"]);

        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/auth/login", request.RequestUri!.AbsolutePath);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(expectedResponse), Encoding.UTF8, "application/json")
            });
        });

        var api = new AuthApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") });
        var result = await api.LoginAsync(new LoginRequest("admin@lavaway.com", "Password123!"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("test_access_token", result.Value.AccessToken);
        Assert.Equal("test_refresh_token", result.Value.RefreshToken);
        Assert.Equal("Administrator", result.Value.Role);
    }

    [Fact]
    public async Task LoginAsync_WhenInvalidCredentials_ShouldReturnUnauthorizedResult()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"code\":\"auth.invalid_credentials\",\"description\":\"Invalid email or password.\"}", Encoding.UTF8, "application/json")
            });
        });

        var api = new AuthApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") });
        var result = await api.LoginAsync(new LoginRequest("admin@lavaway.com", "WrongPassword"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
        Assert.Equal("auth.invalid_credentials", result.Error.Code);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenValid_ShouldReturnNewTokens()
    {
        var expectedResponse = new AuthTokenResponse(
            "new_access_token",
            "new_refresh_token",
            3600,
            "Bearer",
            Guid.NewGuid(),
            "user@lavaway.com",
            Guid.NewGuid(),
            "Operator",
            ["UpdateWorkOrderStatus"]);

        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/auth/refresh", request.RequestUri!.AbsolutePath);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(expectedResponse), Encoding.UTF8, "application/json")
            });
        });

        var api = new AuthApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") });
        var result = await api.RefreshTokenAsync(new RefreshTokenRequest("old_refresh_token"));

        Assert.True(result.IsSuccess);
        Assert.Equal("new_access_token", result.Value!.AccessToken);
        Assert.Equal("new_refresh_token", result.Value.RefreshToken);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
