using CarWashSaaS.Shared.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace CarWashSaaS.Api.Middleware;

public sealed class TenantResolverMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, CurrentTenantAccessor currentTenantAccessor)
    {
        var isAnonymousEndpoint = context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        if (!isAnonymousEndpoint && context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaims = context.User.FindAll("tenant_id").ToArray();
            if (tenantClaims.Length != 1 ||
                !Guid.TryParse(tenantClaims[0].Value, out var tenantId) ||
                tenantId == Guid.Empty)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            currentTenantAccessor.SetTenant(tenantId);
        }

        await next(context);
    }
}