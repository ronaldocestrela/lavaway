using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class CustomerLoyaltyRepository(YardOperationsDbContext dbContext) : ICustomerLoyaltyRepository
{
    public Task<CustomerLoyaltyAccount?> GetByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default) =>
        dbContext.CustomerLoyaltyAccounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.CustomerId == customerId, ct);

    public Task<CustomerLoyaltyAccount?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        dbContext.CustomerLoyaltyAccounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);

    public Task<bool> HasAccrualForWorkOrderAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default) =>
        dbContext.LoyaltyTransactions
            .AnyAsync(t => t.TenantId == tenantId && t.WorkOrderId == workOrderId && t.Type == LoyaltyTransactionType.Accrual, ct);

    public async Task<IReadOnlyList<CustomerLoyaltyAccount>> ListAccountsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var accounts = await dbContext.CustomerLoyaltyAccounts
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.Balance)
            .ToListAsync(ct);

        return accounts.AsReadOnly();
    }

    public async Task<IReadOnlyList<LoyaltyTransaction>> ListTransactionsAsync(Guid tenantId, Guid accountId, CancellationToken ct = default)
    {
        var transactions = await dbContext.LoyaltyTransactions
            .Where(t => t.TenantId == tenantId && t.CustomerLoyaltyAccountId == accountId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);

        return transactions.AsReadOnly();
    }

    public async Task AddAccountAsync(CustomerLoyaltyAccount account, CancellationToken ct = default) =>
        await dbContext.CustomerLoyaltyAccounts.AddAsync(account, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        dbContext.SaveChangesAsync(ct);
}
