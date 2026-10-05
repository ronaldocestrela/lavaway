using CarWashSaaS.Billing.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Api.Endpoints;

public static class SubscriptionEndpoints
{
    public static IEndpointRouteBuilder MapSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/subscriptions");

        // Planos de Assinatura
        group.MapGet("/plans", async (
            bool? activeOnly,
            ICurrentTenantAccessor currentTenantAccessor,
            SubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.ListPlansAsync(tenantId, activeOnly ?? false, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapGet("/plans/{planId:guid}", async (
            Guid planId,
            ICurrentTenantAccessor currentTenantAccessor,
            SubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetPlanByIdAsync(tenantId, planId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapPost("/plans", async (
            CreateSubscriptionPlanRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            SubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.CreatePlanAsync(tenantId, request, ct);
            return result.IsSuccess
                ? Results.Created($"/subscriptions/plans/{result.Value!.Id}", result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        group.MapPut("/plans/{planId:guid}", async (
            Guid planId,
            UpdateSubscriptionPlanRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            SubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.UpdatePlanAsync(tenantId, planId, request, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        // Assinaturas de Clientes
        group.MapGet("/", async (
            ICurrentTenantAccessor currentTenantAccessor,
            SubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.ListSubscriptionsAsync(tenantId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapGet("/{subscriptionId:guid}", async (
            Guid subscriptionId,
            ICurrentTenantAccessor currentTenantAccessor,
            SubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetSubscriptionByIdAsync(tenantId, subscriptionId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapPost("/", async (
            SubscribeCustomerRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            SubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.SubscribeCustomerAsync(tenantId, request, ct);
            return result.IsSuccess
                ? Results.Created($"/subscriptions/{result.Value!.Id}", result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapPost("/{subscriptionId:guid}/plates", async (
            Guid subscriptionId,
            AddSubscriptionPlateRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            SubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.AddPlateToSubscriptionAsync(tenantId, subscriptionId, request.Plate, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapDelete("/{subscriptionId:guid}/plates/{plate}", async (
            Guid subscriptionId,
            string plate,
            ICurrentTenantAccessor currentTenantAccessor,
            SubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.RemovePlateFromSubscriptionAsync(tenantId, subscriptionId, plate, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapPost("/{subscriptionId:guid}/cancel", async (
            Guid subscriptionId,
            string? reason,
            ICurrentTenantAccessor currentTenantAccessor,
            SubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.CancelSubscriptionAsync(tenantId, subscriptionId, reason ?? "Cancelamento solicitado", ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapGet("/{subscriptionId:guid}/usages", async (
            Guid subscriptionId,
            ICurrentTenantAccessor currentTenantAccessor,
            SubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.ListUsagesBySubscriptionAsync(tenantId, subscriptionId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapGet("/dashboard", async (
            ICurrentTenantAccessor currentTenantAccessor,
            SubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetDashboardSummaryAsync(tenantId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapGet("/lookup/by-plate", async (
            string plate,
            ICurrentTenantAccessor currentTenantAccessor,
            SubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetActiveSubscriptionByPlateAsync(tenantId, plate, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapPost("/consume", async (
            ConsumeSubscriptionCreditRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            SubscriptionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.ConsumeCreditForWorkOrderAsync(tenantId, request, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        return app;
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
