using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Application;

namespace CarWashSaaS.Api.Endpoints;

public static class PlatformWhatsAppEndpoints
{
    public static IEndpointRouteBuilder MapPlatformWhatsAppEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/platform/whatsapp")
            .RequireAuthorization(PlatformAuthorizationPolicyNames.PlatformSupport);

        group.MapGet("/instances", async (
            string? searchTerm,
            string? status,
            int? page,
            int? pageSize,
            PlatformWhatsAppHealthApplicationService service,
            CancellationToken ct) =>
        {
            var request = new GetPlatformWhatsAppInstancesRequest(
                SearchTerm: searchTerm,
                Status: status,
                Page: page ?? 1,
                PageSize: pageSize ?? 20);

            var result = await service.GetOverviewAsync(request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        });

        group.MapGet("/instances/{tenantId:guid}/health", async (
            Guid tenantId,
            WhatsAppConnectionApplicationService service,
            CancellationToken ct) =>
        {
            var result = await service.GetHealthDetailsAsync(tenantId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        });

        group.MapPost("/instances/{tenantId:guid}/probe", async (
            Guid tenantId,
            PlatformWhatsAppHealthApplicationService service,
            CancellationToken ct) =>
        {
            var result = await service.RunProbeAsync(tenantId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        });

        group.MapPost("/instances/{tenantId:guid}/alert", async (
            Guid tenantId,
            TriggerWhatsAppAlertRequest? request,
            PlatformWhatsAppHealthApplicationService service,
            CancellationToken ct) =>
        {
            var result = await service.TriggerManualAlertAsync(tenantId, request ?? new TriggerWhatsAppAlertRequest(), ct);
            return result.IsSuccess
                ? Results.Ok(new { message = "Alerta de reconexão disparado com sucesso." })
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        });

        group.MapGet("/incidents", async (
            int? count,
            PlatformWhatsAppHealthApplicationService service,
            CancellationToken ct) =>
        {
            var result = await service.ListRecentIncidentsAsync(count ?? 50, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { result.Error!.Code, result.Error.Description });
        });

        return app;
    }
}
