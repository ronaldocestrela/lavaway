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
        var isActive = user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow;

        return new IdentityUserSnapshot(user.Id, user.TenantId, user.Email!, role, isActive, user.CustomRoleId);
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
        var isActive = user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow;

        return new IdentityUserSnapshot(user.Id, user.TenantId, user.Email!, role, isActive, user.CustomRoleId);
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

    public Task<Result<IdentityUserSnapshot>> CreateUserAsync(
        Guid tenantId,
        string email,
        string password,
        ShopRole role,
        CancellationToken ct = default)
    {
        return CreateUserAsync(tenantId, email, password, role, null, ct);
    }

    public async Task<Result<IdentityUserSnapshot>> CreateUserAsync(
        Guid tenantId,
        string email,
        string password,
        ShopRole role,
        Guid? customRoleId,
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
            EmailConfirmed = true,
            LockoutEnabled = true,
            CustomRoleId = customRoleId
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

        return Result<IdentityUserSnapshot>.Success(new IdentityUserSnapshot(user.Id, user.TenantId, user.Email, role, true, user.CustomRoleId));
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
            var isActive = user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow;
            list.Add(new IdentityUserSnapshot(user.Id, user.TenantId, user.Email!, role, isActive, user.CustomRoleId));
        }

        return list;
    }

    public async Task<Result> SetActiveStatusAsync(Guid userId, bool isActive, CancellationToken ct = default)
    {
        var user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            return Result.Failure(new Error("user.not_found", "User was not found.", ErrorType.NotFound));
        }

        user.LockoutEnabled = true;
        user.LockoutEnd = isActive ? null : DateTimeOffset.MaxValue;
        await userManager.UpdateAsync(user);

        return Result.Success();
    }

    public Task<Result> UpdateUserAsync(
        Guid userId,
        string email,
        string? newPassword,
        ShopRole? role,
        bool isActive,
        CancellationToken ct = default)
    {
        return UpdateUserAsync(userId, email, newPassword, role, isActive, null, ct);
    }

    public async Task<Result> UpdateUserAsync(
        Guid userId,
        string email,
        string? newPassword,
        ShopRole? role,
        bool isActive,
        Guid? customRoleId,
        CancellationToken ct = default)
    {
        var user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            return Result.Failure(new Error("user.not_found", "User was not found.", ErrorType.NotFound));
        }

        var normalizedEmail = email.ToUpperInvariant();
        if (user.NormalizedEmail != normalizedEmail)
        {
            var emailExists = await context.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.NormalizedEmail == normalizedEmail && u.Id != userId, ct);

            if (emailExists)
            {
                return Result.Failure(new Error("user.email.duplicate", "A user with this email already exists.", ErrorType.Conflict));
            }

            user.Email = email;
            user.NormalizedEmail = normalizedEmail;
            user.UserName = email;
            user.NormalizedUserName = normalizedEmail;
        }

        user.LockoutEnabled = true;
        user.LockoutEnd = isActive ? null : DateTimeOffset.MaxValue;
        if (customRoleId.HasValue)
        {
            user.CustomRoleId = customRoleId.Value == Guid.Empty ? null : customRoleId;
        }

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var first = updateResult.Errors.FirstOrDefault();
            return Result.Failure(new Error(first?.Code ?? "user.update.failed", first?.Description ?? "Failed to update user.", ErrorType.Validation));
        }

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await userManager.ResetPasswordAsync(user, token, newPassword);
            if (!resetResult.Succeeded)
            {
                var first = resetResult.Errors.FirstOrDefault();
                return Result.Failure(new Error(first?.Code ?? "user.password.failed", first?.Description ?? "Failed to update password.", ErrorType.Validation));
            }
        }

        if (role.HasValue)
        {
            var currentRoles = await userManager.GetRolesAsync(user);
            await userManager.RemoveFromRolesAsync(user, currentRoles);
            await userManager.AddToRoleAsync(user, role.Value.ToString());
        }

        return Result.Success();
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
