using System.Security.Claims;
using System.Text.Json;
using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Application;
using Microsoft.AspNetCore.Http;

namespace CarWashSaaS.Api.Endpoints;

public static class PlatformEndpoints
{
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder app)
    {
        var platformGroup = app.MapGroup("/platform");

        // Auth
        platformGroup.MapPost("/auth/login", async (
            PlatformLoginRequest request,
            PlatformAuthApplicationService authService,
            HttpContext context,
            CancellationToken ct) =>
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var userAgent = context.Request.Headers.UserAgent.ToString();

            var result = await authService.AuthenticateAsync(request, ipAddress, userAgent, ct);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Value);
            }

            return result.Error!.Type switch
            {
                ErrorType.Unauthorized => Results.Json(new { result.Error.Code, result.Error.Description }, statusCode: StatusCodes.Status401Unauthorized),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).AllowAnonymous();

        // Platform Users Management
        platformGroup.MapPost("/users", async (
            CreatePlatformUserRequest request,
            PlatformAuthApplicationService authService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var adminIdClaim = user.FindFirst("sub")?.Value;
            var adminEmailClaim = user.FindFirst("email")?.Value ?? "admin@lavaway.com";
            var adminRoleClaim = user.FindFirst("role")?.Value ?? "SuperAdmin";
            var adminId = Guid.TryParse(adminIdClaim, out var parsedId) ? parsedId : Guid.NewGuid();

            var result = await authService.CreatePlatformUserAsync(request, adminId, adminEmailClaim, adminRoleClaim, ct);
            if (result.IsSuccess)
            {
                return Results.Created($"/platform/users/{result.Value!.Id}", result.Value);
            }

            return result.Error!.Type switch
            {
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformSuperAdmin);

        platformGroup.MapGet("/users", async (
            PlatformAuthApplicationService authService,
            CancellationToken ct) =>
        {
            var result = await authService.ListPlatformUsersAsync(ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        }).RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformSuperAdmin);

        // Audit Trail
        platformGroup.MapGet("/audit", async (
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            Guid? actorId,
            string? actorEmail,
            string? action,
            string? targetType,
            string? targetId,
            Guid? tenantId,
            string? outcome,
            string? searchTerm,
            int page,
            int pageSize,
            AuditTrailApplicationService auditService,
            CancellationToken ct) =>
        {
            var filter = new AuditQueryFilter(
                fromUtc,
                toUtc,
                actorId,
                actorEmail,
                action,
                targetType,
                targetId,
                tenantId,
                outcome,
                searchTerm,
                page <= 0 ? 1 : page,
                pageSize <= 0 ? 20 : pageSize);

            var result = await auditService.SearchAuditEventsAsync(filter, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        }).RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformAuditor);

        platformGroup.MapGet("/audit/{id:guid}", async (
            Guid id,
            AuditTrailApplicationService auditService,
            CancellationToken ct) =>
        {
            var result = await auditService.GetAuditEventByIdAsync(id, ct);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Value);
            }

            return result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                _ => Results.BadRequest(new { result.Error!.Code, result.Error.Description })
            };
        }).RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformAuditor);

        platformGroup.MapPost("/audit", async (
            RecordAuditEventRequest request,
            AuditTrailApplicationService auditService,
            HttpContext context,
            CancellationToken ct) =>
        {
            var enrichedRequest = request with
            {
                IpAddress = string.IsNullOrWhiteSpace(request.IpAddress)
                    ? context.Connection.RemoteIpAddress?.ToString()
                    : request.IpAddress,
                UserAgent = string.IsNullOrWhiteSpace(request.UserAgent)
                    ? context.Request.Headers.UserAgent.ToString()
                    : request.UserAgent
            };

            var result = await auditService.RecordEventAsync(enrichedRequest, ct);
            if (result.IsSuccess)
            {
                return Results.Created($"/platform/audit/{result.Value}", new { id = result.Value });
            }

            return Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        }).RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformUser);

        // Global Tenants Management
        platformGroup.MapGet("/tenants", async (
            string? searchTerm,
            TenantStatus? status,
            int? page,
            int? pageSize,
            GlobalTenantApplicationService tenantService,
            CancellationToken ct) =>
        {
            var request = new GetGlobalTenantsRequest(
                SearchTerm: searchTerm,
                Status: status,
                Page: page ?? 1,
                PageSize: pageSize ?? 20);

            var result = await tenantService.GetTenantsAsync(request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        }).RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformSupport);

        platformGroup.MapGet("/tenants/{id:guid}", async (
            Guid id,
            GlobalTenantApplicationService tenantService,
            CancellationToken ct) =>
        {
            var result = await tenantService.GetTenantByIdAsync(id, ct);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Value);
            }

            return result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                _ => Results.BadRequest(new { result.Error!.Code, result.Error.Description })
            };
        }).RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformSupport);

        platformGroup.MapPut("/tenants/{id:guid}/status", async (
            Guid id,
            UpdateTenantStatusRequest request,
            GlobalTenantApplicationService tenantService,
            AuditTrailApplicationService auditService,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken ct) =>
        {
            var actorIdClaim = user.FindFirst("sub")?.Value;
            var actorEmailClaim = user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value ?? "admin@lavaway.com";
            var actorRoleClaim = user.FindFirst("role")?.Value ?? user.FindFirst(ClaimTypes.Role)?.Value ?? "SuperAdmin";
            var actorRealm = user.FindFirst("user_realm")?.Value ?? "Platform";
            var actorId = Guid.TryParse(actorIdClaim, out var parsedId) ? parsedId : Guid.NewGuid();

            var result = await tenantService.UpdateTenantStatusAsync(id, request, ct);
            if (!result.IsSuccess)
            {
                return result.Error!.Type switch
                {
                    ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                    _ => Results.BadRequest(new { result.Error!.Code, result.Error.Description })
                };
            }

            // Registrar trilha de auditoria
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var userAgent = context.Request.Headers.UserAgent.ToString();
            var detailsJson = JsonSerializer.Serialize(new
            {
                tenantId = id,
                newStatus = request.NewStatus.ToString(),
                reason = request.Reason,
                trialEndsAtUtc = request.TrialEndsAtUtc
            });

            await auditService.RecordEventAsync(new RecordAuditEventRequest(
                ActorId: actorId,
                ActorEmail: actorEmailClaim,
                ActorRole: actorRoleClaim,
                ActorRealm: actorRealm,
                Action: PlatformActionConstants.TenantStatusChanged,
                TargetType: PlatformTargetTypeConstants.Tenant,
                TargetId: id.ToString(),
                TenantId: id,
                IpAddress: ipAddress,
                UserAgent: userAgent,
                DetailsJson: detailsJson,
                Outcome: "Success"), ct);

            return Results.Ok(result.Value);
        }).RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformSupport);

        // Impersonation Engine
        platformGroup.MapPost("/tenants/{id:guid}/impersonate", async (
            Guid id,
            StartImpersonationRequest request,
            TenantImpersonationApplicationService impersonationService,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken ct) =>
        {
            var actorIdClaim = user.FindFirst("sub")?.Value;
            var actorEmailClaim = user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value ?? "support@lavaway.com";
            var actorRoleClaim = user.FindFirst("role")?.Value ?? user.FindFirst(ClaimTypes.Role)?.Value ?? "PlatformSupport";
            var actorRealm = user.FindFirst("user_realm")?.Value ?? "Platform";
            var actorId = Guid.TryParse(actorIdClaim, out var parsedId) ? parsedId : Guid.NewGuid();

            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var userAgent = context.Request.Headers.UserAgent.ToString();

            var result = await impersonationService.StartImpersonationAsync(
                id,
                request,
                actorId,
                actorEmailClaim,
                actorRoleClaim,
                actorRealm,
                ipAddress,
                userAgent,
                ct);

            if (result.IsSuccess)
            {
                return Results.Ok(result.Value);
            }

            return result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Unauthorized => Results.Json(new { result.Error.Code, result.Error.Description }, statusCode: StatusCodes.Status403Forbidden),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformSupport);

        platformGroup.MapPost("/tenants/{id:guid}/end-impersonation", async (
            Guid id,
            EndImpersonationRequest request,
            TenantImpersonationApplicationService impersonationService,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken ct) =>
        {
            var actorIdClaim = user.FindFirst("sub")?.Value;
            var actorEmailClaim = user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value ?? "support@lavaway.com";
            var actorRoleClaim = user.FindFirst("role")?.Value ?? user.FindFirst(ClaimTypes.Role)?.Value ?? "PlatformSupport";
            var actorRealm = user.FindFirst("user_realm")?.Value ?? "Platform";
            var actorId = Guid.TryParse(actorIdClaim, out var parsedId) ? parsedId : Guid.NewGuid();

            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var userAgent = context.Request.Headers.UserAgent.ToString();

            var result = await impersonationService.EndImpersonationAsync(
                id,
                request,
                actorId,
                actorEmailClaim,
                actorRoleClaim,
                actorRealm,
                ipAddress,
                userAgent,
                ct);

            if (result.IsSuccess)
            {
                return Results.Ok(new { message = "Sessão de diagnóstico encerrada com sucesso." });
            }

            return Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        }).RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformSupport);

        return app;
    }
}
