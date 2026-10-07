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
            var isPlatformUser = string.Equals(context.User.FindFirst("is_platform_admin")?.Value, "true", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(context.User.FindFirst("user_realm")?.Value, "Platform", StringComparison.OrdinalIgnoreCase);

            if (isPlatformUser)
            {
                if (context.Request.Headers.TryGetValue("X-Impersonate-Tenant-Id", out var impersonateHeader) &&
                    Guid.TryParse(impersonateHeader, out var impersonateTenantId) &&
                    impersonateTenantId != Guid.Empty)
                {
                    currentTenantAccessor.SetTenant(impersonateTenantId);
                }

                await next(context);
                return;
            }

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
