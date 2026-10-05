using System.Security.Claims;
using CarWashSaaS.Billing.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Api.Endpoints;

public static class CashierEndpoints
{
    public static IEndpointRouteBuilder MapCashierEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/billing/cashier");

        group.MapPost("/payments/work-order", async (
            RegisterWorkOrderPaymentRequest request,
            ClaimsPrincipal user,
            ICurrentTenantAccessor currentTenantAccessor,
            CashRegisterApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var userId = TryGetUserId(user);
            var userName = user.Identity?.Name ?? "Operador";

            var result = await service.RegisterWorkOrderPaymentAsync(tenantId, request, userId, userName, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapPost("/movements", async (
            CreateCashMovementRequest request,
            ClaimsPrincipal user,
            ICurrentTenantAccessor currentTenantAccessor,
            CashRegisterApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var userId = TryGetUserId(user);
            var userName = user.Identity?.Name ?? "Operador";

            var result = await service.RecordCashMovementAsync(tenantId, request, userId, userName, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapGet("/summary", async (
            DateOnly? date,
            ICurrentTenantAccessor currentTenantAccessor,
            CashRegisterApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var result = await service.GetDailySummaryAsync(tenantId, targetDate, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapPost("/close", async (
            CloseDailyCashRequest request,
            ClaimsPrincipal user,
            ICurrentTenantAccessor currentTenantAccessor,
            CashRegisterApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var userId = TryGetUserId(user) ?? Guid.Empty;
            var userName = user.Identity?.Name ?? "Gestor";

            var result = await service.CloseDailyCashAsync(tenantId, request, userId, userName, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapGet("/closings", async (
            int? count,
            ICurrentTenantAccessor currentTenantAccessor,
            CashRegisterApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.ListRecentClosingsAsync(tenantId, count ?? 30, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        return app;
    }

    private static Guid? TryGetUserId(ClaimsPrincipal user)
    {
        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var id) ? id : null;
    }

    private static IResult MapErrorToResult(Error error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Description }),
        ErrorType.Validation => Results.BadRequest(new { error.Code, error.Description }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Description }),
        ErrorType.Unauthorized => Results.Problem(error.Description, statusCode: StatusCodes.Status403Forbidden),
        _ => Results.Problem(error.Description, statusCode: StatusCodes.Status400BadRequest)
    };
}
