using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.Api.Endpoints;

public static class YardOperationsEndpoints
{
    public static IEndpointRouteBuilder MapYardOperationsEndpoints(this IEndpointRouteBuilder app)
    {
        MapServicesEndpoints(app);
        MapCustomerEndpoints(app);
        MapYardSetupEndpoints(app);

        return app;
    }

    private static void MapServicesEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/services", async (ICurrentTenantAccessor currentTenantAccessor, ServiceCatalogApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.ListAsync(tenantId);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization();

        app.MapGet("/services/{id:guid}", async (Guid id, ICurrentTenantAccessor currentTenantAccessor, ServiceCatalogApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetAsync(tenantId, id);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization();

        app.MapPost("/services", async (CreateServiceCommand command, ICurrentTenantAccessor currentTenantAccessor, ServiceCatalogApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.CreateAsync(tenantId, command);
            return result.IsSuccess ? Results.Created($"/services/{result.Value!.Id}", result.Value) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        app.MapPut("/services/{id:guid}", async (Guid id, UpdateServiceCommand command, ICurrentTenantAccessor currentTenantAccessor, ServiceCatalogApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.UpdateAsync(tenantId, id, command);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);
    }

    private static void MapCustomerEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/customers/search", async (
            string? plate,
            string? phone,
            int? limit,
            ICurrentTenantAccessor currentTenantAccessor,
            CustomerVehicleApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.SearchAsync(tenantId, new SearchCustomerVehiclesQuery(plate, phone, limit ?? 20));
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        app.MapGet("/customers/{customerId:guid}", async (
            Guid customerId,
            ICurrentTenantAccessor currentTenantAccessor,
            CustomerVehicleApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetAsync(tenantId, customerId);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        app.MapPost("/customers", async (
            CreateCustomerWithVehicleRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            CustomerVehicleApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            if (!Enum.TryParse<VehicleSize>(request.Size, true, out var size) || !Enum.IsDefined(size))
            {
                return Results.BadRequest(new { Code = "vehicle.size.invalid", Description = "Vehicle size is invalid." });
            }

            var command = new CreateCustomerWithVehicleCommand(request.Name, request.Phone, request.Plate, size);
            var result = await service.CreateAsync(tenantId, command);
            return result.IsSuccess ? Results.Created($"/customers/{result.Value!.CustomerId}", result.Value) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.CreateWorkOrders);

        app.MapPost("/customers/{customerId:guid}/vehicles", async (
            Guid customerId,
            AddVehicleToCustomerRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            CustomerVehicleApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            if (!Enum.TryParse<VehicleSize>(request.Size, true, out var size) || !Enum.IsDefined(size))
            {
                return Results.BadRequest(new { Code = "vehicle.size.invalid", Description = "Vehicle size is invalid." });
            }

            var result = await service.AddVehicleAsync(tenantId, customerId, new AddVehicleToCustomerCommand(request.Plate, size));
            return result.IsSuccess ? Results.Created($"/customers/{customerId}", result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.CreateWorkOrders);
    }

    private static void MapYardSetupEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/yard/capacity", async (ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetCapacityAsync(tenantId);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization();

        app.MapPost("/yard/capacity", async (CreateYardCapacityCommand command, ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.CreateCapacityAsync(tenantId, command);
            return result.IsSuccess ? Results.Created("/yard/capacity", result.Value) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        app.MapGet("/team-members", async (ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.ListTeamMembersAsync(tenantId);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization();

        app.MapPost("/team-members", async (CreateTeamMemberCommand command, ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.CreateTeamMemberAsync(tenantId, command);
            return result.IsSuccess ? Results.Created($"/team-members/{result.Value!.Id}", result.Value) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        app.MapGet("/commission-rules", async (ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.ListCommissionRulesAsync(tenantId);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization();

        app.MapPost("/commission-rules", async (CreateCommissionRuleCommand command, ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.CreateCommissionRuleAsync(tenantId, command);
            return result.IsSuccess ? Results.Created($"/commission-rules/{result.Value!.Id}", result.Value) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);
    }
}
