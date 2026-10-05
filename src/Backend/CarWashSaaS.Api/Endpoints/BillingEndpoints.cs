using CarWashSaaS.Api.Middleware;
using CarWashSaaS.Billing.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Api.Endpoints;

public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/billing");

        group.MapPost("/work-orders/{workOrderId:guid}/pix", async (
            Guid workOrderId,
            GenerateWorkOrderPixChargeRequest? request,
            ICurrentTenantAccessor currentTenantAccessor,
            PixBillingApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetOrCreatePixChargeForWorkOrderAsync(
                tenantId,
                workOrderId,
                request?.ExpirationMinutes,
                ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapGet("/work-orders/{workOrderId:guid}/pix", async (
            Guid workOrderId,
            ICurrentTenantAccessor currentTenantAccessor,
            PixBillingApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetPixChargeByWorkOrderIdAsync(tenantId, workOrderId, ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapPost("/work-orders/{workOrderId:guid}/pix/send-whatsapp", async (
            Guid workOrderId,
            SendWorkOrderPixWhatsAppRequest? request,
            ICurrentTenantAccessor currentTenantAccessor,
            PixBillingApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.SendPixChargeToCustomerWhatsAppAsync(
                tenantId,
                workOrderId,
                request?.CustomMessage,
                ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapErrorToResult(result.Error!);
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        return app;
    }

    private static IResult MapErrorToResult(Error error) =>
        error.Type switch
        {
            ErrorType.NotFound => Results.NotFound(new { error.Code, error.Description }),
            ErrorType.Conflict => Results.Conflict(new { error.Code, error.Description }),
            ErrorType.Unauthorized => Results.Forbid(),
            _ => Results.BadRequest(new { error.Code, error.Description })
        };
}
