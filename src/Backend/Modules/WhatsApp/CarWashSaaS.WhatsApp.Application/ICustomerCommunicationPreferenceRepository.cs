using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.WhatsApp.Application;

public interface ICustomerCommunicationPreferenceRepository
{
    Task<CustomerCommunicationPreference?> GetByPhoneAsync(Guid tenantId, string normalizedPhone, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerCommunicationPreference>> ListPreferencesAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(CustomerCommunicationPreference preference, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
