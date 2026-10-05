using CarWashSaaS.Api.Middleware;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;

namespace CarWashSaaS.Api.Endpoints;

public static class AfterSalesEndpoints
{
    public static IEndpointRouteBuilder MapAfterSalesEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. Registro de Retirada na Ordem de Serviço
        app.MapPost("/yard/work-orders/{id:guid}/pickup", async (
            Guid id,
            RegisterWorkOrderPickupRequest? request,
            ICurrentTenantAccessor currentTenantAccessor,
            AfterSalesApplicationService afterSalesService,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("Tenant é obrigatório.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await afterSalesService.RegisterPickupAsync(tenantId, id, request?.PickedUpAtUtc, request?.Notes, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.BadRequest(new { result.Error.Code, result.Error.Description })
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        // 2. Disparo manual de pesquisa
        app.MapPost("/yard/work-orders/{id:guid}/survey/send", async (
            Guid id,
            ICurrentTenantAccessor currentTenantAccessor,
            AfterSalesApplicationService afterSalesService,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("Tenant é obrigatório.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await afterSalesService.DispatchSurveyManuallyAsync(tenantId, id, ct);
            return result.IsSuccess ? Results.Ok() : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.BadRequest(new { result.Error.Code, result.Error.Description })
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        var group = app.MapGroup("/after-sales");

        // 3. Métricas gerais de pós-venda
        group.MapGet("/metrics", async (
            ICurrentTenantAccessor currentTenantAccessor,
            AfterSalesApplicationService afterSalesService,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("Tenant é obrigatório.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await afterSalesService.GetMetricsAsync(tenantId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        // 4. Listagem de avaliações / pesquisas
        group.MapGet("/surveys", async (
            ICurrentTenantAccessor currentTenantAccessor,
            AfterSalesApplicationService afterSalesService,
            int? limit,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("Tenant é obrigatório.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await afterSalesService.ListSurveysAsync(tenantId, limit ?? 50, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        // 5. Réguas de reativação
        group.MapGet("/campaigns/rules", async (
            ICurrentTenantAccessor currentTenantAccessor,
            ReactivationCampaignApplicationService campaignService,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("Tenant é obrigatório.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await campaignService.GetRulesAsync(tenantId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        // 6. Atualizar régua de reativação
        group.MapPut("/campaigns/rules/{id:guid}", async (
            Guid id,
            UpdateReactivationCampaignRuleRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            ReactivationCampaignApplicationService campaignService,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("Tenant é obrigatório.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await campaignService.UpdateRuleAsync(tenantId, id, request, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                _ => Results.BadRequest(new { result.Error.Code, result.Error.Description })
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        // 7. Consultar audiência de uma régua
        group.MapGet("/campaigns/rules/{id:guid}/audience", async (
            Guid id,
            ICurrentTenantAccessor currentTenantAccessor,
            ReactivationCampaignApplicationService campaignService,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("Tenant é obrigatório.", statusCode: StatusCodes.Status403Forbidden);
            }

            var rules = await campaignService.GetRulesAsync(tenantId, ct);
            var rule = rules.Value?.FirstOrDefault(r => r.Id == id);
            if (rule is null)
            {
                return Results.NotFound(new { Code = "campaign.rule_not_found", Description = "Regra não encontrada." });
            }

            var result = await campaignService.GetInactiveCustomersForTierAsync(tenantId, rule.DaysInactive, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        // 8. Disparo manual de campanha por régua
        group.MapPost("/campaigns/rules/{id:guid}/dispatch", async (
            Guid id,
            ICurrentTenantAccessor currentTenantAccessor,
            ReactivationCampaignApplicationService campaignService,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("Tenant é obrigatório.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await campaignService.DispatchRuleManuallyAsync(tenantId, id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                _ => Results.BadRequest(new { result.Error.Code, result.Error.Description })
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        // 9. Listagem de preferências de comunicação (Opt-in / Opt-out)
        group.MapGet("/preferences", async (
            ICurrentTenantAccessor currentTenantAccessor,
            ICustomerCommunicationPreferenceLookup preferenceLookup,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("Tenant é obrigatório.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await preferenceLookup.ListPreferencesAsync(tenantId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        // 10. Atualização manual de preferência
        group.MapPost("/preferences/toggle", async (
            UpdateCustomerPreferenceRequest request,
            ICurrentTenantAccessor currentTenantAccessor,
            ICustomerCommunicationPreferenceLookup preferenceLookup,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("Tenant é obrigatório.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await preferenceLookup.UpdatePreferenceAsync(tenantId, request.Phone, request.IsOptedIn, request.Reason, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        }).RequireAuthorization(AuthorizationPolicyNames.Receptionist);

        return app;
    }
}
