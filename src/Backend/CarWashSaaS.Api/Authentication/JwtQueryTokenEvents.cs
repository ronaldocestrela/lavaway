using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;

namespace CarWashSaaS.Api.Authentication;

public static class JwtQueryTokenEvents
{
    public static Task HandleMessageReceivedAsync(MessageReceivedContext context)
    {
        var accessToken = context.Request.Query["access_token"];
        var path = context.HttpContext.Request.Path;

        if (!string.IsNullOrEmpty(accessToken) &&
            (path.StartsWithSegments("/work-orders") || path.StartsWithSegments("/hubs")))
        {
            context.Token = accessToken;
        }

        return Task.CompletedTask;
    }
}
