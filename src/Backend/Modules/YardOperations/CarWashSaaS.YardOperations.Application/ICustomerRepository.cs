using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default);
    Task<Customer?> GetByNormalizedPhoneAsync(Guid tenantId, string normalizedPhone, CancellationToken ct = default) =>
        Task.FromResult<Customer?>(null);
    Task AddAsync(Customer customer, CancellationToken ct = default);
}
