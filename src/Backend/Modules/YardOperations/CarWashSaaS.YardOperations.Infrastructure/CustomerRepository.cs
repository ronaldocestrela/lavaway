using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class CustomerRepository(YardOperationsDbContext dbContext) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default) =>
        dbContext.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(customer => customer.TenantId == tenantId && customer.Id == customerId, ct);

    public Task<Customer?> GetByNormalizedPhoneAsync(Guid tenantId, string normalizedPhone, CancellationToken ct = default)
    {
        var candidates = Customer.GetCandidateNormalizedPhones(normalizedPhone);
        return dbContext.Customers
            .FirstOrDefaultAsync(customer => customer.TenantId == tenantId &&
                (candidates.Contains(customer.NormalizedPhone) || candidates.Contains(customer.Phone)), ct);
    }

    public async Task AddAsync(Customer customer, CancellationToken ct = default) =>
        await dbContext.Customers.AddAsync(customer, ct);
}
