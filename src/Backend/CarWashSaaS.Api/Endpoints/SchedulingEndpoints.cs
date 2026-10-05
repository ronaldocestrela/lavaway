using CarWashSaaS.Api.Middleware;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;

namespace CarWashSaaS.Api.Endpoints;

public static class SchedulingEndpoints
{
    public static IEndpointRouteBuilder MapSchedulingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/scheduling");

        group.MapGet("/bookings", async (
            ICurrentTenantAccessor currentTenantAccessor,
            BookingApplicationService service,
            string? date,
            string? status,
            string? search,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            DateOnly? parsedDate = null;
            if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var d))
            {
                parsedDate = d;
            }

            var result = await service.GetBookingsAsync(tenantId, parsedDate, status, search, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapGet("/bookings/{id:guid}", async (
            Guid id,
            ICurrentTenantAccessor currentTenantAccessor,
            BookingApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetBookingByIdAsync(tenantId, id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                _ => Results.BadRequest(new { result.Error.Code, result.Error.Description })
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapPost("/bookings", async (
            CreateManualBookingRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            BookingApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.CreateManualBookingAsync(tenantId, request, ct);
            return result.IsSuccess
                ? Results.Created($"/scheduling/bookings/{result.Value!.BookingId}", result.Value)
                : result.Error!.Type switch
                {
                    ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                    ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                    _ => Results.BadRequest(new { result.Error.Code, result.Error.Description })
                };
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapPost("/bookings/{id:guid}/cancel", async (
            Guid id,
            CancelBookingRequest? request,
            ICurrentTenantAccessor currentTenantAccessor,
            BookingApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.CancelBookingAsync(tenantId, id, request?.Reason, ct);
            return result.IsSuccess
                ? Results.NoContent()
                : result.Error!.Type switch
                {
                    ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                    _ => Results.BadRequest(new { result.Error.Code, result.Error.Description })
                };
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        group.MapGet("/slots", async (
            ICurrentTenantAccessor currentTenantAccessor,
            BookingApplicationService service,
            string date,
            Guid serviceId,
            string? vehicleSize,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            if (!DateOnly.TryParse(date, out var parsedDate))
            {
                return Results.BadRequest(new { error = "Valid date (YYYY-MM-DD) is required." });
            }

            var size = string.IsNullOrWhiteSpace(vehicleSize) ? VehicleSizeConstants.HatchSedan : vehicleSize;
            var result = await service.GetAvailableTimeSlotsAsync(tenantId, parsedDate, serviceId, size, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.Error!.Type switch
                {
                    ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                    _ => Results.BadRequest(new { result.Error.Code, result.Error.Description })
                };
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        return app;
    }
}

public sealed record CancelBookingRequest(string? Reason);
