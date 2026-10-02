using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Identity.Application;

public interface IIdentityUserRepository
{
    Task<IdentityUserSnapshot?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<IdentityUserSnapshot?> FindByIdAsync(Guid userId, CancellationToken ct = default);
    Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken ct = default);
    Task<Result<IdentityUserSnapshot>> CreateUserAsync(Guid tenantId, string email, string password, ShopRole role, CancellationToken ct = default);
    Task<IReadOnlyCollection<IdentityUserSnapshot>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
}
