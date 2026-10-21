using CarWashSaaS.Shared.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

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

                using (PushLogProperties(currentTenantAccessor, context))
                {
                    await next(context);
                }
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

        using (PushLogProperties(currentTenantAccessor, context))
        {
            await next(context);
        }
    }

    private static IDisposable? PushLogProperties(CurrentTenantAccessor currentTenantAccessor, HttpContext context)
    {
        var disposables = new List<IDisposable>(3);

        if (currentTenantAccessor.TenantId.HasValue)
        {
            disposables.Add(LogContext.PushProperty("TenantId", currentTenantAccessor.TenantId.Value));
        }

        var userId = context.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            disposables.Add(LogContext.PushProperty("UserId", userId));
        }

        var userRealm = context.User.FindFirst("user_realm")?.Value;
        if (!string.IsNullOrWhiteSpace(userRealm))
        {
            disposables.Add(LogContext.PushProperty("UserRealm", userRealm));
        }

        return disposables.Count switch
        {
            0 => null,
            1 => disposables[0],
            _ => new CompositeDisposable(disposables)
        };
    }

    private sealed class CompositeDisposable(List<IDisposable> disposables) : IDisposable
    {
        public void Dispose()
        {
            for (var i = disposables.Count - 1; i >= 0; i--)
            {
                disposables[i].Dispose();
            }
        }
    }
}
