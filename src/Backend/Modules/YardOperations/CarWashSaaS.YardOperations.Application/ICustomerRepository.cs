using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default);
    Task AddAsync(Customer customer, CancellationToken ct = default);
}
