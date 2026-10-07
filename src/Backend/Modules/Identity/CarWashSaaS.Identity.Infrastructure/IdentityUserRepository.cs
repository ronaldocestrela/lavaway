using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Identity.Infrastructure;

public sealed class IdentityUserRepository(
    UserManager<ApplicationUser> userManager,
    IdentityModuleDbContext context,
    ICurrentTenantAccessor? currentTenantAccessor = null) : IIdentityUserRepository
{
    public async Task<IdentityUserSnapshot?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.ToUpperInvariant();
        var user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

        if (user is null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        var role = ParseRole(roles);

        return new IdentityUserSnapshot(user.Id, user.TenantId, user.Email!, role);
    }

    public async Task<IdentityUserSnapshot?> FindByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        var role = ParseRole(roles);

        return new IdentityUserSnapshot(user.Id, user.TenantId, user.Email!, role);
    }

    public async Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken ct = default)
    {
        var user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            return false;
        }

        return await userManager.CheckPasswordAsync(user, password);
    }

    public async Task<Result<IdentityUserSnapshot>> CreateUserAsync(
        Guid tenantId,
        string email,
        string password,
        ShopRole role,
        CancellationToken ct = default)
    {
        var normalizedEmail = email.ToUpperInvariant();
        var exists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail, ct);

        if (exists)
        {
            return Result<IdentityUserSnapshot>.Failure(new Error(
                "user.email.duplicate",
                "A user with this email already exists.",
                ErrorType.Conflict));
        }

        if (currentTenantAccessor?.TenantId is null && currentTenantAccessor is CurrentTenantAccessor mutableAccessor)
        {
            mutableAccessor.SetTenant(tenantId);
        }

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            UserName = email,
            NormalizedUserName = normalizedEmail,
            Email = email,
            NormalizedEmail = normalizedEmail,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var firstError = result.Errors.FirstOrDefault();
            return Result<IdentityUserSnapshot>.Failure(new Error(
                firstError?.Code ?? "user.creation.failed",
                firstError?.Description ?? "Failed to create user.",
                ErrorType.Validation));
        }

        var roleResult = await userManager.AddToRoleAsync(user, role.ToString());
        if (!roleResult.Succeeded)
        {
            var firstError = roleResult.Errors.FirstOrDefault();
            return Result<IdentityUserSnapshot>.Failure(new Error(
                firstError?.Code ?? "user.role.failed",
                firstError?.Description ?? "Failed to assign role to user.",
                ErrorType.Validation));
        }

        return Result<IdentityUserSnapshot>.Success(new IdentityUserSnapshot(user.Id, user.TenantId, user.Email, role));
    }

    public async Task<IReadOnlyCollection<IdentityUserSnapshot>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var users = await context.Users
            .Where(u => u.TenantId == tenantId)
            .ToListAsync(ct);

        var list = new List<IdentityUserSnapshot>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var role = ParseRole(roles);
            list.Add(new IdentityUserSnapshot(user.Id, user.TenantId, user.Email!, role));
        }

        return list;
    }

    private static ShopRole ParseRole(IList<string> roles)
    {
        if (roles.Count > 0 && Enum.TryParse<ShopRole>(roles[0], true, out var parsedRole))
        {
            return parsedRole;
        }

        return ShopRole.Operator;
    }
}
