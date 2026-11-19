using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Identity.Application;

public interface IIdentityUserRepository
{
    Task<IdentityUserSnapshot?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<IdentityUserSnapshot?> FindByIdAsync(Guid userId, CancellationToken ct = default);
    Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken ct = default);
    Task<Result<IdentityUserSnapshot>> CreateUserAsync(Guid tenantId, string email, string password, ShopRole role, CancellationToken ct = default);
    Task<Result<IdentityUserSnapshot>> CreateUserAsync(Guid tenantId, string email, string password, ShopRole role, Guid? customRoleId, CancellationToken ct = default)
        => CreateUserAsync(tenantId, email, password, role, ct);
    Task<IReadOnlyCollection<IdentityUserSnapshot>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<Result> SetActiveStatusAsync(Guid userId, bool isActive, CancellationToken ct = default);
    Task<Result> UpdateUserAsync(Guid userId, string email, string? newPassword, ShopRole? role, bool isActive, CancellationToken ct = default);
    Task<Result> UpdateUserAsync(Guid userId, string email, string? newPassword, ShopRole? role, bool isActive, Guid? customRoleId, CancellationToken ct = default)
        => UpdateUserAsync(userId, email, newPassword, role, isActive, ct);
}
