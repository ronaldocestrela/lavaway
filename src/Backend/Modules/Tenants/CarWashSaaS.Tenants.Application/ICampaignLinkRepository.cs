using CarWashSaaS.Tenants.Domain;

namespace CarWashSaaS.Tenants.Application;

public interface ICampaignLinkRepository
{
    Task<CampaignLink?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CampaignLink?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<CampaignLink>> ListAsync(string? searchTerm = null, bool? isActive = null, CancellationToken ct = default);
    Task AddAsync(CampaignLink campaign, CancellationToken ct = default);
    void Update(CampaignLink campaign);
    Task<bool> IncrementClickCountAsync(Guid id, DateTimeOffset utcNow, CancellationToken ct = default);
}
