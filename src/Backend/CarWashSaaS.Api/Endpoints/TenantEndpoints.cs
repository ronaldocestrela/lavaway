using CarWashSaaS.Api.Services;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Application;

namespace CarWashSaaS.Api.Endpoints;

public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tenants");

        group.MapGet("/profile", async (ICurrentTenantAccessor currentTenantAccessor, StoreProfileApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.GetAsync(tenantId);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization();

        group.MapPost("/profile", async (CreateStoreProfileCommand command, ICurrentTenantAccessor currentTenantAccessor, StoreProfileApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.CreateAsync(tenantId, command);
            return result.IsSuccess ? Results.Created("/tenants/profile", result.Value) : result.Error!.Type switch
            {
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        group.MapPut("/profile", async (UpdateStoreProfileCommand command, ICurrentTenantAccessor currentTenantAccessor, StoreProfileApplicationService service) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await service.UpdateAsync(tenantId, command);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
            {
                ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
                ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
                _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        group.MapPost("/profile/logo", async (
            IFormFile? logoFile,
            string? brandPrimaryColor,
            string? brandSecondaryColor,
            ICurrentTenantAccessor currentTenantAccessor,
            StoreProfileApplicationService service,
            TenantBrandingStorageService storageService,
            IBackgroundQueue backgroundQueue,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            if (logoFile is null || logoFile.Length == 0)
            {
                return Results.BadRequest(new { code = "store_profile.logo.required", description = "A logo file is required." });
            }

            var profileResult = await service.GetAsync(tenantId);
            if (!profileResult.IsSuccess)
            {
                return profileResult.Error!.Type switch
                {
                    ErrorType.NotFound => Results.NotFound(new { profileResult.Error.Code, profileResult.Error.Description }),
                    ErrorType.Validation => Results.BadRequest(new { profileResult.Error.Code, profileResult.Error.Description }),
                    _ => Results.Problem(profileResult.Error.Description, statusCode: StatusCodes.Status400BadRequest)
                };
            }

            var existingProfile = profileResult.Value!;
            await using var logoStream = logoFile.OpenReadStream();
            var savedLogoResult = await storageService.SaveLogoAsync(tenantId, logoFile.FileName, logoFile.Length, logoStream, ct);
            if (!savedLogoResult.IsSuccess)
            {
                return Results.BadRequest(new { savedLogoResult.Error!.Code, savedLogoResult.Error.Description });
            }

            var updatedCommand = new UpdateStoreProfileCommand(
                existingProfile.LegalName,
                existingProfile.TradeName,
                existingProfile.Cnpj,
                existingProfile.Phone,
                existingProfile.Street,
                existingProfile.City,
                existingProfile.State,
                existingProfile.PostalCode,
                savedLogoResult.Value,
                string.IsNullOrWhiteSpace(brandPrimaryColor) ? existingProfile.BrandPrimaryColor : brandPrimaryColor,
                string.IsNullOrWhiteSpace(brandSecondaryColor) ? existingProfile.BrandSecondaryColor : brandSecondaryColor);

            var updateResult = await service.UpdateAsync(tenantId, updatedCommand);
            if (!updateResult.IsSuccess)
            {
                return updateResult.Error!.Type switch
                {
                    ErrorType.NotFound => Results.NotFound(new { updateResult.Error.Code, updateResult.Error.Description }),
                    ErrorType.Validation => Results.BadRequest(new { updateResult.Error.Code, updateResult.Error.Description }),
                    _ => Results.Problem(updateResult.Error.Description, statusCode: StatusCodes.Status400BadRequest)
                };
            }

            await backgroundQueue.EnqueueAsync(
                new TenantQueueMessage(tenantId, TenantBrandingAuditQueueHandler.EventName, savedLogoResult.Value!), ct);

            return Results.Ok(updateResult.Value);
        }).RequireAuthorization(AuthorizationPolicyNames.Administrator);

        group.MapGet("/profile/logo/{fileName}", async (
            string fileName,
            ICurrentTenantAccessor currentTenantAccessor,
            StoreProfileApplicationService service,
            TenantBrandingStorageService storageService,
            CancellationToken ct) =>
        {
            if (currentTenantAccessor.TenantId is not Guid tenantId)
            {
                return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var profileResult = await service.GetAsync(tenantId, ct);
            if (!profileResult.IsSuccess || !string.Equals(profileResult.Value!.LogoUrl, $"/tenants/profile/logo/{fileName}", StringComparison.Ordinal))
            {
                return Results.NotFound();
            }

            var logo = await storageService.GetLogoAsync(tenantId, fileName, ct);
            return logo is null
                ? Results.NotFound()
                : Results.File(logo.Content, logo.ContentType);
        }).RequireAuthorization();

        return app;
    }
}
