using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class LoyaltyTransaction : IMustHaveTenant
{
    private LoyaltyTransaction()
    {
    }

    internal LoyaltyTransaction(
        Guid id,
        Guid tenantId,
        Guid customerLoyaltyAccountId,
        LoyaltyTransactionType type,
        int amount,
        int balanceAfter,
        Guid? workOrderId,
        string? workOrderNumber,
        string description,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        CustomerLoyaltyAccountId = customerLoyaltyAccountId;
        Type = type;
        Amount = amount;
        BalanceAfter = balanceAfter;
        WorkOrderId = workOrderId;
        WorkOrderNumber = string.IsNullOrWhiteSpace(workOrderNumber) ? null : workOrderNumber.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public Guid CustomerLoyaltyAccountId { get; private set; }
    public LoyaltyTransactionType Type { get; private set; }
    public int Amount { get; private set; }
    public int BalanceAfter { get; private set; }
    public Guid? WorkOrderId { get; private set; }
    public string? WorkOrderNumber { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<LoyaltyTransaction> Create(
        Guid tenantId,
        Guid customerLoyaltyAccountId,
        LoyaltyTransactionType type,
        int amount,
        int balanceAfter,
        Guid? workOrderId = null,
        string? workOrderNumber = null,
        string? description = null,
        DateTimeOffset? createdAtUtc = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<LoyaltyTransaction>.Failure(new Error("loyalty_tx.tenant.required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (customerLoyaltyAccountId == Guid.Empty)
        {
            return Result<LoyaltyTransaction>.Failure(new Error("loyalty_tx.account.required", "Conta de fidelidade é obrigatória.", ErrorType.Validation));
        }

        if (amount == 0)
        {
            return Result<LoyaltyTransaction>.Failure(new Error("loyalty_tx.amount.invalid", "Quantidade de selos da transação não pode ser zero.", ErrorType.Validation));
        }

        var tx = new LoyaltyTransaction(
            Guid.CreateVersion7(),
            tenantId,
            customerLoyaltyAccountId,
            type,
            amount,
            balanceAfter,
            workOrderId,
            workOrderNumber,
            description ?? string.Empty,
            createdAtUtc ?? DateTimeOffset.UtcNow);

        return Result<LoyaltyTransaction>.Success(tx);
    }
}
