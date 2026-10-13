using CarWashSaaS.Api.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;

namespace CarWashSaaS.IntegrationTests;

public sealed class JwtQueryTokenEventsTests
{
    [Fact]
    public async Task HandleMessageReceivedAsync_ShouldExtractTokenFromQuery_WhenPathStartsWithWorkOrders()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/work-orders/01a1327a-0a17-7aa7-af10-dcb48cf61273/inspection/photos/01a13283-37a1-7f39-bef2-76cc9f945518";
        httpContext.Request.QueryString = new QueryString("?access_token=valid_test_token");

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var options = new JwtBearerOptions();
        var context = new MessageReceivedContext(httpContext, scheme, options);

        await JwtQueryTokenEvents.HandleMessageReceivedAsync(context);

        Assert.Equal("valid_test_token", context.Token);
    }

    [Fact]
    public async Task HandleMessageReceivedAsync_ShouldExtractTokenFromQuery_WhenPathStartsWithHubs()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/hubs/yard";
        httpContext.Request.QueryString = new QueryString("?access_token=hub_signalr_token");

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var options = new JwtBearerOptions();
        var context = new MessageReceivedContext(httpContext, scheme, options);

        await JwtQueryTokenEvents.HandleMessageReceivedAsync(context);

        Assert.Equal("hub_signalr_token", context.Token);
    }

    [Fact]
    public async Task HandleMessageReceivedAsync_ShouldNotExtractToken_WhenPathIsNotWorkOrdersOrHubs()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/tenants/profile";
        httpContext.Request.QueryString = new QueryString("?access_token=some_token");

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var options = new JwtBearerOptions();
        var context = new MessageReceivedContext(httpContext, scheme, options);

        await JwtQueryTokenEvents.HandleMessageReceivedAsync(context);

        Assert.Null(context.Token);
    }

    [Fact]
    public async Task HandleMessageReceivedAsync_ShouldNotSetToken_WhenQueryHasNoAccessToken()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/work-orders/01a1327a-0a17-7aa7-af10-dcb48cf61273/inspection/photos/01a13283-37a1-7f39-bef2-76cc9f945518";

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var options = new JwtBearerOptions();
        var context = new MessageReceivedContext(httpContext, scheme, options);

        await JwtQueryTokenEvents.HandleMessageReceivedAsync(context);

        Assert.Null(context.Token);
    }
}
