using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Domain;

namespace CarWashSaaS.Tenants.Application;

public sealed class GlobalTenantApplicationService(ITenantRepository repository) : IGlobalTenantLookup, ITenantNotificationContactLookup
{
    public async Task<Result<PagedResult<GlobalTenantSummaryDto>>> GetTenantsAsync(
        GetGlobalTenantsRequest request,
        CancellationToken ct = default)
    {
        var safeRequest = request with
        {
            Page = request.Page < 1 ? 1 : request.Page,
            PageSize = request.PageSize < 1 ? 20 : (request.PageSize > 100 ? 100 : request.PageSize)
        };

        var paged = await repository.SearchGlobalTenantsAsync(safeRequest, ct);
        return Result<PagedResult<GlobalTenantSummaryDto>>.Success(paged);
    }

    public async Task<Result<GlobalTenantSummaryDto>> GetTenantByIdAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<GlobalTenantSummaryDto>.Failure(new Error(
                "tenant.id.required",
                "Tenant ID cannot be empty.",
                ErrorType.Validation));
        }

        var summary = await repository.GetSummaryByIdAsync(tenantId, ct);
        if (summary is null)
        {
            return Result<GlobalTenantSummaryDto>.Failure(new Error(
                "tenant.not_found",
                $"Tenant with ID '{tenantId}' was not found.",
                ErrorType.NotFound));
        }

        return Result<GlobalTenantSummaryDto>.Success(summary);
    }

    public async Task<Result<GlobalTenantSummaryDto>> UpdateTenantStatusAsync(
        Guid tenantId,
        UpdateTenantStatusRequest request,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<GlobalTenantSummaryDto>.Failure(new Error(
                "tenant.id.required",
                "Tenant ID cannot be empty.",
                ErrorType.Validation));
        }

        var tenant = await repository.GetByIdAsync(tenantId, ct);
        if (tenant is null)
        {
            return Result<GlobalTenantSummaryDto>.Failure(new Error(
                "tenant.not_found",
                $"Tenant with ID '{tenantId}' was not found.",
                ErrorType.NotFound));
        }

        var changeResult = tenant.ChangeStatus(request.NewStatus, request.Reason, request.TrialEndsAtUtc);
        if (!changeResult.IsSuccess)
        {
            return Result<GlobalTenantSummaryDto>.Failure(changeResult.Error!);
        }

        await repository.UpdateAsync(tenant, ct);

        var updatedSummary = await repository.GetSummaryByIdAsync(tenantId, ct);
        return Result<GlobalTenantSummaryDto>.Success(updatedSummary!);
    }

    public async Task<Result<bool>> ExistsAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<bool>.Success(false);
        }

        var tenant = await repository.GetByIdAsync(tenantId, ct);
        return Result<bool>.Success(tenant is not null);
    }

    public async Task<Result<GlobalTenantSummaryDto>> GetSummaryAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<GlobalTenantSummaryDto>.Failure(new Error(
                "tenant.id.required",
                "Tenant ID cannot be empty.",
                ErrorType.Validation));
        }

        var summary = await repository.GetSummaryByIdAsync(tenantId, ct);
        if (summary is null)
        {
            return Result<GlobalTenantSummaryDto>.Failure(new Error(
                "tenant.not_found",
                $"Tenant with ID '{tenantId}' was not found.",
                ErrorType.NotFound));
        }

        return Result<GlobalTenantSummaryDto>.Success(summary);
    }

    public async Task<Result<TenantNotificationContactDto>> GetContactAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TenantNotificationContactDto>.Failure(new Error(
                "tenant.id.required",
                "Tenant ID cannot be empty.",
                ErrorType.Validation));
        }

        var summary = await repository.GetSummaryByIdAsync(tenantId, ct);
        if (summary is null)
        {
            return Result<TenantNotificationContactDto>.Failure(new Error(
                "tenant.not_found",
                $"Tenant with ID '{tenantId}' was not found.",
                ErrorType.NotFound));
        }

        var sanitizedName = !string.IsNullOrWhiteSpace(summary.TradeName) ? summary.TradeName : summary.Name;
        var email = $"{sanitizedName.ToLowerInvariant().Replace(" ", "").Replace("-", "")}@lavaway.com";

        return Result<TenantNotificationContactDto>.Success(new TenantNotificationContactDto(
            tenantId,
            sanitizedName,
            email,
            summary.Phone));
    }
}
