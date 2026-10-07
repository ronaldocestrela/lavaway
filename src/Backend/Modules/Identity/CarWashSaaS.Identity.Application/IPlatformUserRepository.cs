using CarWashSaaS.Identity.Domain;

namespace CarWashSaaS.Identity.Application;

public interface IPlatformUserRepository
{
    Task<PlatformUser?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<PlatformUser?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyCollection<PlatformUser>> ListAllAsync(CancellationToken ct = default);
    Task AddAsync(PlatformUser user, CancellationToken ct = default);
    void Update(PlatformUser user);
    bool VerifyPassword(PlatformUser user, string password);
    string HashPassword(string password);
}
