using System.Security.Claims;
using CarWashSaaS.Api.Services;
using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Api.Endpoints;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var authGroup = app.MapGroup("/auth");

        authGroup.MapPost("/login", async (
            LoginRequest request,
            IdentityApplicationService identityService,
            CancellationToken ct) =>
        {
            var result = await identityService.AuthenticateAsync(request, ct);
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

        authGroup.MapPost("/register-tenant", async (
            RegisterTenantRequest request,
            TenantOnboardingService onboardingService,
            CancellationToken ct) =>
        {
            var result = await onboardingService.RegisterAsync(request, ct);
            if (result.IsSuccess)
            {
                return Results.Created("/auth/me", result.Value);
            }

            return result.Error!.Type switch
            {
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).AllowAnonymous();

        authGroup.MapPost("/seed", async (IServiceProvider serviceProvider) =>
        {
            await CarWashSaaS.Api.Services.DevDatabaseSeeder.SeedAsync(serviceProvider);
            return Results.Ok(new { message = "Database seeded successfully for development." });
        }).AllowAnonymous();

        authGroup.MapPost("/refresh", async (
            RefreshTokenRequest request,
            IdentityApplicationService identityService,
            CancellationToken ct) =>
        {
            var result = await identityService.RefreshTokenAsync(request, ct);
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

        authGroup.MapPost("/revoke", async (
            RevokeTokenRequest request,
            IdentityApplicationService identityService,
            CancellationToken ct) =>
        {
            var result = await identityService.RevokeTokenAsync(request, ct);
            return result.IsSuccess
                ? Results.Ok(new { message = "Token revoked successfully." })
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        }).AllowAnonymous();

        authGroup.MapGet("/me", (
            ClaimsPrincipal user,
            ICurrentTenantAccessor currentTenantAccessor) =>
        {
            var userId = user.FindFirst("sub")?.Value;
            var email = user.FindFirst("email")?.Value;
            var role = user.FindFirst("role")?.Value;
            var permissions = user.FindAll("permission").Select(c => c.Value).ToArray();

            return Results.Ok(new
            {
                userId,
                email,
                tenantId = currentTenantAccessor.TenantId,
                role,
                permissions
            });
        }).RequireAuthorization();

        var identityGroup = app.MapGroup("/identity");

        identityGroup.MapPost("/users", async (
            CreateUserRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            IdentityApplicationService identityService,
            CancellationToken ct) =>
        {
            var tenantId = currentTenantAccessor.TenantId;
            if (!tenantId.HasValue)
            {
                return Results.Forbid();
            }

            var result = await identityService.CreateUserAsync(tenantId.Value, request, ct);
            if (result.IsSuccess)
            {
                return Results.Created($"/identity/users/{result.Value!.Id}", result.Value);
            }

            return result.Error!.Type switch
            {
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        identityGroup.MapGet("/users", async (
            ICurrentTenantAccessor currentTenantAccessor,
            IdentityApplicationService identityService,
            CancellationToken ct) =>
        {
            var tenantId = currentTenantAccessor.TenantId;
            if (!tenantId.HasValue)
            {
                return Results.Forbid();
            }

            var result = await identityService.ListUsersAsync(tenantId.Value, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        identityGroup.MapGet("/permissions", (CustomRoleApplicationService roleService) =>
        {
            var result = roleService.ListAvailablePermissions();
            return Results.Ok(result.Value);
        }).RequireAuthorization();

        identityGroup.MapGet("/roles", async (
            ICurrentTenantAccessor currentTenantAccessor,
            CustomRoleApplicationService roleService,
            CancellationToken ct) =>
        {
            var tenantId = currentTenantAccessor.TenantId;
            if (!tenantId.HasValue)
            {
                return Results.Forbid();
            }

            await roleService.EnsureDefaultRolesAsync(tenantId.Value, ct);
            var result = await roleService.ListRolesAsync(tenantId.Value, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        }).RequireAuthorization();

        identityGroup.MapGet("/roles/{id:guid}", async (
            Guid id,
            ICurrentTenantAccessor currentTenantAccessor,
            CustomRoleApplicationService roleService,
            CancellationToken ct) =>
        {
            var tenantId = currentTenantAccessor.TenantId;
            if (!tenantId.HasValue)
            {
                return Results.Forbid();
            }

            var result = await roleService.GetRoleByIdAsync(tenantId.Value, id, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.Error!.Type switch
                {
                    ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                    _ => Results.BadRequest(new { result.Error.Code, result.Error.Description })
                };
        }).RequireAuthorization();

        identityGroup.MapPost("/roles", async (
            CreateCustomRoleRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            CustomRoleApplicationService roleService,
            CancellationToken ct) =>
        {
            var tenantId = currentTenantAccessor.TenantId;
            if (!tenantId.HasValue)
            {
                return Results.Forbid();
            }

            var result = await roleService.CreateRoleAsync(tenantId.Value, request, ct);
            if (result.IsSuccess)
            {
                return Results.Created($"/identity/roles/{result.Value!.Id}", result.Value);
            }

            return result.Error!.Type switch
            {
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.ManageRoles);

        identityGroup.MapPut("/roles/{id:guid}", async (
            Guid id,
            UpdateCustomRoleRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            CustomRoleApplicationService roleService,
            CancellationToken ct) =>
        {
            var tenantId = currentTenantAccessor.TenantId;
            if (!tenantId.HasValue)
            {
                return Results.Forbid();
            }

            var result = await roleService.UpdateRoleAsync(tenantId.Value, id, request, ct);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Value);
            }

            return result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.ManageRoles);

        identityGroup.MapDelete("/roles/{id:guid}", async (
            Guid id,
            ICurrentTenantAccessor currentTenantAccessor,
            CustomRoleApplicationService roleService,
            CancellationToken ct) =>
        {
            var tenantId = currentTenantAccessor.TenantId;
            if (!tenantId.HasValue)
            {
                return Results.Forbid();
            }

            var result = await roleService.DeleteRoleAsync(tenantId.Value, id, ct);
            if (result.IsSuccess)
            {
                return Results.Ok(new { message = "Role deleted successfully." });
            }

            return result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.ManageRoles);

        return app;
    }
}
