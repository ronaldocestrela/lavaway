using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Identity.Infrastructure;

public sealed class PlatformUserRepository(
    IdentityModuleDbContext dbContext) : IPlatformUserRepository
{
    private static readonly PasswordHasher<PlatformUser> Hasher = new();

    public async Task<PlatformUser?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await dbContext.PlatformUsers
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
    }

    public async Task<PlatformUser?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.PlatformUsers
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<IReadOnlyCollection<PlatformUser>> ListAllAsync(CancellationToken ct = default)
    {
        return await dbContext.PlatformUsers
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);
    }

    public async Task AddAsync(PlatformUser user, CancellationToken ct = default)
    {
        await dbContext.PlatformUsers.AddAsync(user, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public void Update(PlatformUser user)
    {
        dbContext.PlatformUsers.Update(user);
        dbContext.SaveChanges();
    }

    public bool VerifyPassword(PlatformUser user, string password)
    {
        var result = Hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }

    public string HashPassword(string password)
    {
        // Fake instance for hashing
        var dummy = PlatformUser.Create("dummy@domain.com", "Dummy", PlatformRole.PlatformSupport, "dummy").Value!;
        return Hasher.HashPassword(dummy, password);
    }
}
