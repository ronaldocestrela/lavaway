namespace CarWashSaaS.Shared.Contracts;

public interface ICustomerCommunicationPreferenceLookup
{
    Task<Result<CustomerCommunicationPreferenceDto?>> GetPreferenceAsync(
        Guid tenantId,
        string phone,
        CancellationToken ct = default);

    Task<Result<CustomerCommunicationPreferenceDto>> UpdatePreferenceAsync(
        Guid tenantId,
        string phone,
        bool isOptedIn,
        string? reason = null,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<CustomerCommunicationPreferenceDto>>> ListPreferencesAsync(
        Guid tenantId,
        CancellationToken ct = default);
}
