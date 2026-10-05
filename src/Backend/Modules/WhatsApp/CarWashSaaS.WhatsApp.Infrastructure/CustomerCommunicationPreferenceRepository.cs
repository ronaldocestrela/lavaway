using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.WhatsApp.Infrastructure;

public sealed class CustomerCommunicationPreferenceRepository(WhatsAppDbContext dbContext) : ICustomerCommunicationPreferenceRepository
{
    public async Task<CustomerCommunicationPreference?> GetByPhoneAsync(Guid tenantId, string normalizedPhone, CancellationToken ct = default)
    {
        return await dbContext.CustomerCommunicationPreferences
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.NormalizedPhone == normalizedPhone, ct);
    }

    public async Task<IReadOnlyList<CustomerCommunicationPreference>> ListPreferencesAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await dbContext.CustomerCommunicationPreferences
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(CustomerCommunicationPreference preference, CancellationToken ct = default)
    {
        await dbContext.CustomerCommunicationPreferences.AddAsync(preference, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return dbContext.SaveChangesAsync(ct);
    }
}
