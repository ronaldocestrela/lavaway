using System.Security.Claims;
using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;
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

        return app;
    }
}
