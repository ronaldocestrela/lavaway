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
        MapWorkOrderEndpoints(app);
        MapVehicleInspectionEndpoints(app);
        MapPostServicePhotosEndpoints(app);

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
            return result.IsSuccess ? Results.Ok(result.Value!.Select(ToDto).ToList()) : result.Error!.Type switch
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
            return result.IsSuccess ? Results.Ok(ToDto(result.Value!)) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization();

        app.MapPost("/services", async (CreateServiceRequest request, ICurrentTenantAccessor currentTenantAccessor, ServiceCatalogApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var parsedPrices = ParsePrices(request.Prices);
            if (!parsedPrices.IsSuccess)
            {
                return Results.BadRequest(new { parsedPrices.Error!.Code, parsedPrices.Error.Description });
            }

            var command = new CreateServiceCommand(request.Name, request.Category, parsedPrices.Value!);
            var result = await service.CreateAsync(tenantId, command);
            return result.IsSuccess ? Results.Created($"/services/{result.Value!.Id}", ToDto(result.Value!)) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        app.MapPut("/services/{id:guid}", async (Guid id, UpdateServiceRequest request, ICurrentTenantAccessor currentTenantAccessor, ServiceCatalogApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var parsedPrices = ParsePrices(request.Prices);
            if (!parsedPrices.IsSuccess)
            {
                return Results.BadRequest(new { parsedPrices.Error!.Code, parsedPrices.Error.Description });
            }

            var command = new UpdateServiceCommand(request.Name, request.Category, parsedPrices.Value!);
            var result = await service.UpdateAsync(tenantId, id, command);
            return result.IsSuccess ? Results.Ok(ToDto(result.Value!)) : result.Error!.Type switch
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
            return result.IsSuccess ? Results.Ok(ToDto(result.Value!)) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization();

        app.MapPost("/yard/capacity", async (CreateYardCapacityRequest request, ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var command = new CreateYardCapacityCommand(request.TotalBoxes, request.Description);
            var result = await service.CreateCapacityAsync(tenantId, command);
            return result.IsSuccess ? Results.Created("/yard/capacity", ToDto(result.Value!)) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        app.MapPut("/yard/capacity", async (UpdateYardCapacityRequest request, ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var command = new UpdateYardCapacityCommand(request.TotalBoxes, request.Description);
            var result = await service.UpdateCapacityAsync(tenantId, command);
            return result.IsSuccess ? Results.Ok(ToDto(result.Value!)) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
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
            return result.IsSuccess ? Results.Ok(result.Value!.Select(ToDto).ToList()) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization();

        app.MapGet("/team-members/{id:guid}", async (Guid id, ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetTeamMemberAsync(tenantId, id);
            return result.IsSuccess ? Results.Ok(ToDto(result.Value!)) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization();

        app.MapPost("/team-members", async (CreateTeamMemberRequest request, ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var command = new CreateTeamMemberCommand(request.FullName, request.Role, request.Email ?? string.Empty);
            var result = await service.CreateTeamMemberAsync(tenantId, command);
            return result.IsSuccess ? Results.Created($"/team-members/{result.Value!.Id}", ToDto(result.Value!)) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        app.MapPut("/team-members/{id:guid}", async (Guid id, UpdateTeamMemberRequest request, ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var command = new UpdateTeamMemberCommand(request.FullName, request.Role, request.Email);
            var result = await service.UpdateTeamMemberAsync(tenantId, id, command);
            return result.IsSuccess ? Results.Ok(ToDto(result.Value!)) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        app.MapPatch("/team-members/{id:guid}/toggle-status", async (Guid id, ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.ToggleTeamMemberStatusAsync(tenantId, id);
            return result.IsSuccess ? Results.Ok(ToDto(result.Value!)) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
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
            return result.IsSuccess ? Results.Ok(result.Value!.Select(ToDto).ToList()) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization();

        app.MapPost("/commission-rules", async (CreateCommissionRuleRequest request, ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var command = new CreateCommissionRuleCommand(request.ServiceName, request.RoleName, request.Percentage);
            var result = await service.CreateCommissionRuleAsync(tenantId, command);
            return result.IsSuccess ? Results.Created($"/commission-rules/{result.Value!.Id}", ToDto(result.Value!)) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        app.MapPut("/commission-rules/{id:guid}", async (Guid id, UpdateCommissionRuleRequest request, ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var command = new UpdateCommissionRuleCommand(request.Percentage);
            var result = await service.UpdateCommissionRuleAsync(tenantId, id, command);
            return result.IsSuccess ? Results.Ok(ToDto(result.Value!)) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        app.MapDelete("/commission-rules/{id:guid}", async (Guid id, ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.DeleteCommissionRuleAsync(tenantId, id);
            return result.IsSuccess ? Results.NoContent() : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);
    }

    private static void MapWorkOrderEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/work-orders", async (
            CreateWorkOrderRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            WorkOrderApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            if (request.Items is null || request.Items.Count == 0)
            {
                return Results.BadRequest(new { Code = "work_order.items.required", Description = "Pelo menos um serviço é obrigatório." });
            }

            var command = new CreateWorkOrderCommand(
                request.CustomerId,
                request.VehicleId,
                request.Items.Select(i => new CreateWorkOrderItemInput(i.ServiceId, i.Quantity)).ToList(),
                request.Notes);

            var result = await service.CreateAsync(tenantId, command);
            return result.IsSuccess ? Results.Created($"/work-orders/{result.Value!.Id}", result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.CreateWorkOrders);

        app.MapGet("/work-orders/{id:guid}", async (
            Guid id,
            ICurrentTenantAccessor currentTenantAccessor,
            WorkOrderApplicationService service) =>
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
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        app.MapGet("/work-orders", async (
            int? limit,
            ICurrentTenantAccessor currentTenantAccessor,
            WorkOrderApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.ListRecentAsync(tenantId, limit ?? 20);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        app.MapGet("/yard/kanban", async (
            ICurrentTenantAccessor currentTenantAccessor,
            WorkOrderApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetKanbanBoardAsync(tenantId);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        app.MapPatch("/work-orders/{id:guid}/status", async (
            Guid id,
            ChangeWorkOrderStatusRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            WorkOrderApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.ChangeStatusAsync(tenantId, id, request);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.UpdateWorkOrderStatus);

        app.MapPatch("/work-orders/{id:guid}/operator", async (
            Guid id,
            AssignOperatorRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            WorkOrderApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.AssignOperatorAsync(tenantId, id, request.OperatorId);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.UpdateWorkOrderStatus);

        app.MapGet("/work-orders/{id:guid}/history", async (
            Guid id,
            ICurrentTenantAccessor currentTenantAccessor,
            WorkOrderApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetStatusHistoryAsync(tenantId, id);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);
    }

    private static YardCapacityDto ToDto(YardCapacity capacity) => new(
        capacity.Id,
        capacity.TotalBoxes,
        capacity.Description);

    private static TeamMemberDto ToDto(TeamMember member) => new(
        member.Id,
        member.FullName,
        member.Role,
        member.Email,
        member.IsActive);

    private static CommissionRuleDto ToDto(CommissionRule rule) => new(
        rule.Id,
        rule.ServiceName,
        rule.RoleName,
        rule.Percentage);

    private static ServiceDto ToDto(Service service) => new(
        service.Id,
        service.Name,
        service.Category,
        service.Prices.Select(p => new ServicePriceDto(p.VehicleSize.ToString(), p.Amount, p.EstimatedDurationMinutes)).ToList());

    private static Result<List<ServicePriceInput>> ParsePrices(IReadOnlyCollection<ServicePriceDto>? prices)
    {
        if (prices is null || prices.Count == 0)
        {
            return Result<List<ServicePriceInput>>.Failure(new Error("service.prices.required", "Pelo menos um preço por porte de veículo é obrigatório.", ErrorType.Validation));
        }

        var list = new List<ServicePriceInput>();
        foreach (var price in prices)
        {
            if (!Enum.TryParse<VehicleSize>(price.VehicleSize, true, out var size) || !Enum.IsDefined(size))
            {
                return Result<List<ServicePriceInput>>.Failure(new Error("service_price.size.invalid", $"Porte de veículo '{price.VehicleSize}' é inválido.", ErrorType.Validation));
            }

            list.Add(new ServicePriceInput(size, price.Amount, price.EstimatedDurationMinutes));
        }

        return Result<List<ServicePriceInput>>.Success(list);
    }

    private static void MapVehicleInspectionEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/work-orders/{workOrderId:guid}/inspection", async (
            Guid workOrderId,
            ICurrentTenantAccessor currentTenantAccessor,
            VehicleInspectionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetByWorkOrderIdAsync(tenantId, workOrderId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        app.MapPost("/work-orders/{workOrderId:guid}/inspection", async (
            Guid workOrderId,
            CreateInspectionRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            VehicleInspectionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.CreateOrGetInspectionAsync(tenantId, workOrderId, request, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        app.MapPut("/work-orders/{workOrderId:guid}/inspection/checklist", async (
            Guid workOrderId,
            UpdateInspectionChecklistRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            VehicleInspectionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.UpdateChecklistAsync(tenantId, workOrderId, request, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        app.MapPost("/work-orders/{workOrderId:guid}/inspection/damages", async (
            Guid workOrderId,
            AddInspectionDamageRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            VehicleInspectionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.AddDamageAsync(tenantId, workOrderId, request, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        app.MapDelete("/work-orders/{workOrderId:guid}/inspection/damages/{damageId:guid}", async (
            Guid workOrderId,
            Guid damageId,
            ICurrentTenantAccessor currentTenantAccessor,
            VehicleInspectionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.RemoveDamageAsync(tenantId, workOrderId, damageId, ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        app.MapPost("/work-orders/{workOrderId:guid}/inspection/photos", async (
            Guid workOrderId,
            IFormFile file,
            string category,
            Guid? damageId,
            ICurrentTenantAccessor currentTenantAccessor,
            VehicleInspectionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            if (!Enum.TryParse<InspectionPhotoCategory>(category, true, out var photoCategory))
            {
                return Results.BadRequest(new { Code = "photo.category.invalid", Description = "Categoria de foto inválida." });
            }

            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { Code = "photo.file.empty", Description = "Arquivo não enviado." });
            }

            await using var stream = file.OpenReadStream();
            var result = await service.UploadPhotoAsync(
                tenantId,
                workOrderId,
                photoCategory,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                damageId,
                ct);

            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers)
          .DisableAntiforgery();

        app.MapGet("/work-orders/{workOrderId:guid}/inspection/photos/{photoId:guid}", async (
            Guid workOrderId,
            Guid photoId,
            ICurrentTenantAccessor currentTenantAccessor,
            VehicleInspectionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetPhotoStreamAsync(tenantId, workOrderId, photoId, ct);
            return result.IsSuccess
                ? Results.File(result.Value!.Content, result.Value.ContentType)
                : result.Error!.Type switch
                {
                    ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                    _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
                };
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        app.MapPost("/work-orders/{workOrderId:guid}/inspection/complete", async (
            Guid workOrderId,
            ICurrentTenantAccessor currentTenantAccessor,
            VehicleInspectionApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.CompleteInspectionAsync(tenantId, workOrderId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);
    }

    private static void MapPostServicePhotosEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/work-orders/{workOrderId:guid}/comparison-gallery", async (
            Guid workOrderId,
            ICurrentTenantAccessor currentTenantAccessor,
            WorkOrderApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetComparisonGalleryAsync(tenantId, workOrderId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        app.MapPost("/work-orders/{workOrderId:guid}/post-service-photos", async (
            Guid workOrderId,
            IFormFile file,
            string? category,
            string? title,
            Guid? workOrderItemId,
            Guid? beforeInspectionPhotoId,
            string? notes,
            ICurrentTenantAccessor currentTenantAccessor,
            WorkOrderApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var photoCategory = InspectionPhotoCategory.Other;
            if (!string.IsNullOrWhiteSpace(category) && !Enum.TryParse(category, true, out photoCategory))
            {
                return Results.BadRequest(new { Code = "photo.category.invalid", Description = "Categoria de foto inválida." });
            }

            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { Code = "photo.file.empty", Description = "Arquivo não enviado." });
            }

            var resolvedTitle = string.IsNullOrWhiteSpace(title) ? "Foto Pós-Serviço" : title.Trim();

            await using var stream = file.OpenReadStream();
            var result = await service.UploadPostServicePhotoAsync(
                tenantId,
                workOrderId,
                workOrderItemId,
                beforeInspectionPhotoId,
                photoCategory,
                resolvedTitle,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                notes,
                ct);

            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.UpdateWorkOrderStatus)
          .DisableAntiforgery();

        app.MapGet("/work-orders/{workOrderId:guid}/post-service-photos/{photoId:guid}", async (
            Guid workOrderId,
            Guid photoId,
            ICurrentTenantAccessor currentTenantAccessor,
            WorkOrderApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetPostServicePhotoStreamAsync(tenantId, workOrderId, photoId, ct);
            return result.IsSuccess
                ? Results.File(result.Value!.Content, result.Value.ContentType)
                : result.Error!.Type switch
                {
                    ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                    _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
                };
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        app.MapDelete("/work-orders/{workOrderId:guid}/post-service-photos/{photoId:guid}", async (
            Guid workOrderId,
            Guid photoId,
            ICurrentTenantAccessor currentTenantAccessor,
            WorkOrderApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.RemovePostServicePhotoAsync(tenantId, workOrderId, photoId, ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.UpdateWorkOrderStatus);

        app.MapGet("/work-orders/{id:guid}/receipt-pdf", async (
            Guid id,
            ICurrentTenantAccessor currentTenantAccessor,
            WorkOrderNotificationApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetReceiptPdfStreamAsync(tenantId, id, ct);
            if (!result.IsSuccess)
            {
                return result.Error!.Type switch
                {
                    ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                    _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
                };
            }

            var fileName = $"comprovante-OS-{id.ToString("D")[..8].ToUpperInvariant()}.pdf";
            return Results.File(result.Value!.Content, "application/pdf", fileName);
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);

        app.MapPost("/work-orders/{id:guid}/notifications/receipt", async (
            Guid id,
            SendWorkOrderNotificationRequest? request,
            ICurrentTenantAccessor currentTenantAccessor,
            WorkOrderNotificationApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.SendReceiptNotificationAsync(tenantId, id, request?.CustomMessage, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.UpdateWorkOrderStatus);

        app.MapPost("/work-orders/{id:guid}/notifications/ready", async (
            Guid id,
            SendWorkOrderNotificationRequest? request,
            ICurrentTenantAccessor currentTenantAccessor,
            WorkOrderNotificationApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.SendReadyForPickupNotificationAsync(tenantId, id, request?.CustomMessage, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.UpdateWorkOrderStatus);

        app.MapPost("/work-orders/{id:guid}/notifications/comparison-photos", async (
            Guid id,
            SendComparisonPhotosRequest? request,
            ICurrentTenantAccessor currentTenantAccessor,
            WorkOrderNotificationApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.SendComparisonPhotosNotificationAsync(tenantId, id, request?.SelectedPhotoIds, request?.CustomMessage, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.UpdateWorkOrderStatus);

        app.MapGet("/work-orders/{id:guid}/notifications", async (
            Guid id,
            ICurrentTenantAccessor currentTenantAccessor,
            WorkOrderNotificationApplicationService service,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetNotificationSummaryAsync(tenantId, id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(new { result.Error!.Code, result.Error.Description });
        }).RequireAuthorization(AuthorizationPolicyNames.ViewCustomers);
    }
}

