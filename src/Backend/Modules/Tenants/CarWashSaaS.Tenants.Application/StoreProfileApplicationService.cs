using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Domain;

namespace CarWashSaaS.Tenants.Application;

public sealed class StoreProfileApplicationService(IStoreProfileRepository repository)
{
    public async Task<Result<StoreProfile>> GetAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<StoreProfile>.Failure(new Error("store_profile.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var profile = await repository.GetByTenantIdAsync(tenantId, ct);
        if (profile is null)
        {
            return Result<StoreProfile>.Failure(new Error("store_profile.not_found", "Store profile was not found.", ErrorType.NotFound));
        }

        return Result<StoreProfile>.Success(profile);
    }

    public async Task<Result<StoreProfile>> CreateAsync(Guid tenantId, CreateStoreProfileCommand command, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<StoreProfile>.Failure(new Error("store_profile.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var existing = await repository.GetByTenantIdAsync(tenantId, ct);
        if (existing is not null)
        {
            return Result<StoreProfile>.Failure(new Error("store_profile.duplicate", "The store profile for this tenant already exists.", ErrorType.Conflict));
        }

        var result = StoreProfile.Create(
            tenantId,
            command.LegalName,
            command.TradeName,
            command.Cnpj,
            command.Phone,
            command.Street,
            command.City,
            command.State,
            command.PostalCode,
            command.LogoUrl,
            command.BrandPrimaryColor,
            command.BrandSecondaryColor);

        if (!result.IsSuccess)
        {
            return result;
        }

        await repository.AddAsync(result.Value!, ct);
        return result;
    }

    public async Task<Result<StoreProfile>> UpdateAsync(Guid tenantId, UpdateStoreProfileCommand command, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<StoreProfile>.Failure(new Error("store_profile.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var existing = await repository.GetByTenantIdAsync(tenantId, ct);
        if (existing is null)
        {
            return Result<StoreProfile>.Failure(new Error("store_profile.not_found", "Store profile was not found.", ErrorType.NotFound));
        }

        var result = existing.Update(
            command.LegalName,
            command.TradeName,
            command.Cnpj,
            command.Phone,
            command.Street,
            command.City,
            command.State,
            command.PostalCode,
            command.LogoUrl,
            command.BrandPrimaryColor,
            command.BrandSecondaryColor);

        if (!result.IsSuccess)
        {
            return result;
        }

        await repository.UpdateAsync(existing, ct);
        return result;
    }
}
