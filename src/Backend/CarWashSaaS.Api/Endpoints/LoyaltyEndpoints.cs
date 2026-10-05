using System.Security.Claims;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;

namespace CarWashSaaS.Api.Endpoints;

public static class LoyaltyEndpoints
{
    public static IEndpointRouteBuilder MapLoyaltyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/loyalty");

        group.MapGet("/program", async (
            ICurrentTenantAccessor currentTenantAccessor,
            LoyaltyApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetLoyaltyProgramAsync(tenantId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        group.MapPut("/program", async (
            UpdateLoyaltyProgramRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            LoyaltyApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.UpdateLoyaltyProgramAsync(tenantId, request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        group.MapGet("/customers", async (
            ICurrentTenantAccessor currentTenantAccessor,
            LoyaltyApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.ListCustomerLoyaltyAccountsAsync(tenantId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        group.MapGet("/customers/{customerId:guid}", async (
            Guid customerId,
            ICurrentTenantAccessor currentTenantAccessor,
            LoyaltyApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetLoyaltySummaryByCustomerIdAsync(tenantId, customerId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        group.MapGet("/customers/by-phone", async (
            string phone,
            ICurrentTenantAccessor currentTenantAccessor,
            LoyaltyApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetLoyaltySummaryByPhoneAsync(tenantId, phone, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        group.MapGet("/customers/{customerId:guid}/transactions", async (
            Guid customerId,
            ICurrentTenantAccessor currentTenantAccessor,
            LoyaltyApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetCustomerLoyaltyTransactionsAsync(tenantId, customerId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        group.MapPost("/customers/redeem", async (
            RedeemLoyaltyRewardRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            LoyaltyApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.RedeemRewardAsync(tenantId, request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapPost("/customers/adjust", async (
            ManualLoyaltyAdjustmentRequest request,
            ClaimsPrincipal user,
            ICurrentTenantAccessor currentTenantAccessor,
            LoyaltyApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var operatorName = user.Identity?.Name ?? "Administrador";
            var result = await service.AdjustBalanceAsync(tenantId, request, operatorName, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        return app;
    }

    private static IResult MapErrorToResult(Error error) =>
        error.Type switch
        {
            ErrorType.Validation => Results.BadRequest(new { error.Code, error.Description }),
            ErrorType.NotFound => Results.NotFound(new { error.Code, error.Description }),
            ErrorType.Conflict => Results.Conflict(new { error.Code, error.Description }),
            ErrorType.Unauthorized => Results.Forbid(),
            _ => Results.Problem(error.Description, statusCode: StatusCodes.Status500InternalServerError)
        };
}
