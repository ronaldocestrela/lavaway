using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class CustomerLoyaltyAccount : IMustHaveTenant
{
    private readonly List<LoyaltyTransaction> _transactions = [];

    private CustomerLoyaltyAccount()
    {
    }

    private CustomerLoyaltyAccount(
        Guid id,
        Guid tenantId,
        Guid customerId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        CustomerId = customerId;
        Balance = 0;
        TotalEarned = 0;
        TotalRedeemed = 0;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public Guid CustomerId { get; private set; }
    public int Balance { get; private set; }
    public int TotalEarned { get; private set; }
    public int TotalRedeemed { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? LastAccrualAtUtc { get; private set; }
    public DateTimeOffset? LastRedemptionAtUtc { get; private set; }

    public IReadOnlyCollection<LoyaltyTransaction> Transactions => _transactions.AsReadOnly();

    public static Result<CustomerLoyaltyAccount> Create(Guid tenantId, Guid customerId)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CustomerLoyaltyAccount>.Failure(new Error("loyalty_account.tenant.required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (customerId == Guid.Empty)
        {
            return Result<CustomerLoyaltyAccount>.Failure(new Error("loyalty_account.customer.required", "Cliente é obrigatório.", ErrorType.Validation));
        }

        var account = new CustomerLoyaltyAccount(Guid.CreateVersion7(), tenantId, customerId, DateTimeOffset.UtcNow);
        return Result<CustomerLoyaltyAccount>.Success(account);
    }

    public Result<LoyaltyTransaction> CreditStamps(
        int stamps,
        Guid workOrderId,
        string? workOrderNumber = null,
        string? description = null,
        DateTimeOffset? timestamp = null)
    {
        if (stamps <= 0)
        {
            return Result<LoyaltyTransaction>.Failure(new Error("loyalty_account.stamps.invalid", "A quantidade de selos a creditar deve ser maior que zero.", ErrorType.Validation));
        }

        var now = timestamp ?? DateTimeOffset.UtcNow;
        Balance += stamps;
        TotalEarned += stamps;
        LastAccrualAtUtc = now;
        UpdatedAtUtc = now;

        var desc = string.IsNullOrWhiteSpace(description)
            ? (string.IsNullOrWhiteSpace(workOrderNumber) ? "Crédito de selos por ordem de serviço" : $"Crédito de selos por {workOrderNumber}")
            : description.Trim();

        var tx = new LoyaltyTransaction(
            Guid.CreateVersion7(),
            TenantId,
            Id,
            LoyaltyTransactionType.Accrual,
            stamps,
            Balance,
            workOrderId,
            workOrderNumber,
            desc,
            now);

        _transactions.Add(tx);
        return Result<LoyaltyTransaction>.Success(tx);
    }

    public Result<LoyaltyTransaction> RedeemReward(
        int stampsCost,
        string rewardTitle,
        string? notes = null,
        DateTimeOffset? timestamp = null)
    {
        if (stampsCost <= 0)
        {
            return Result<LoyaltyTransaction>.Failure(new Error("loyalty_account.cost.invalid", "O custo em selos da recompensa deve ser maior que zero.", ErrorType.Validation));
        }

        if (Balance < stampsCost)
        {
            return Result<LoyaltyTransaction>.Failure(new Error("loyalty.insufficient_balance", $"Saldo de selos insuficiente ({Balance}) para resgatar '{rewardTitle}' (custo: {stampsCost} selos).", ErrorType.Validation));
        }

        var now = timestamp ?? DateTimeOffset.UtcNow;
        Balance -= stampsCost;
        TotalRedeemed += stampsCost;
        LastRedemptionAtUtc = now;
        UpdatedAtUtc = now;

        var desc = string.IsNullOrWhiteSpace(notes)
            ? $"Resgate de recompensa: {rewardTitle}"
            : $"Resgate de recompensa: {rewardTitle} ({notes.Trim()})";

        var tx = new LoyaltyTransaction(
            Guid.CreateVersion7(),
            TenantId,
            Id,
            LoyaltyTransactionType.Redemption,
            -stampsCost,
            Balance,
            workOrderId: null,
            workOrderNumber: null,
            desc,
            now);

        _transactions.Add(tx);
        return Result<LoyaltyTransaction>.Success(tx);
    }

    public Result<LoyaltyTransaction> AdjustBalance(
        int delta,
        string reason,
        string operatorName,
        DateTimeOffset? timestamp = null)
    {
        if (delta == 0)
        {
            return Result<LoyaltyTransaction>.Failure(new Error("loyalty_account.delta.invalid", "O ajuste não pode ser zero.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result<LoyaltyTransaction>.Failure(new Error("loyalty_account.reason.required", "O motivo do ajuste é obrigatório.", ErrorType.Validation));
        }

        if (Balance + delta < 0)
        {
            return Result<LoyaltyTransaction>.Failure(new Error("loyalty.balance_cannot_be_negative", "O ajuste não pode deixar o saldo de selos negativo.", ErrorType.Validation));
        }

        var now = timestamp ?? DateTimeOffset.UtcNow;
        Balance += delta;
        UpdatedAtUtc = now;

        var desc = $"Ajuste manual de {delta:+0;-0} selos por {operatorName}: {reason.Trim()}";

        var tx = new LoyaltyTransaction(
            Guid.CreateVersion7(),
            TenantId,
            Id,
            LoyaltyTransactionType.Adjustment,
            delta,
            Balance,
            workOrderId: null,
            workOrderNumber: null,
            desc,
            now);

        _transactions.Add(tx);
        return Result<LoyaltyTransaction>.Success(tx);
    }

    public int CalculateRemaining(int targetStamps) => Math.Max(0, targetStamps - Balance);

    public bool IsEligibleForReward(int targetStamps) => targetStamps > 0 && Balance >= targetStamps;

    public bool IsNearRedemption(int targetStamps, int proximityThreshold = 1) =>
        targetStamps > 0 &&
        Balance > 0 &&
        Balance < targetStamps &&
        CalculateRemaining(targetStamps) <= proximityThreshold;
}
