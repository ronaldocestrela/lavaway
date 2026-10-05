using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Domain;

public sealed class CashTransaction : IMustHaveTenant
{
    private CashTransaction()
    {
    }

    private CashTransaction(
        Guid id,
        Guid tenantId,
        Guid? workOrderId,
        CashTransactionType type,
        string paymentMethod,
        decimal amount,
        string description,
        DateTimeOffset occurredAtUtc,
        Guid? registeredByUserId,
        string? registeredByUserName,
        string? externalReference)
    {
        Id = id;
        TenantId = tenantId;
        WorkOrderId = workOrderId;
        Type = type;
        PaymentMethod = paymentMethod;
        Amount = amount;
        Description = description;
        OccurredAtUtc = occurredAtUtc;
        RegisteredByUserId = registeredByUserId;
        RegisteredByUserName = registeredByUserName;
        ExternalReference = externalReference;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public Guid? WorkOrderId { get; private set; }
    public CashTransactionType Type { get; private set; }
    public string PaymentMethod { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public Guid? RegisteredByUserId { get; private set; }
    public string? RegisteredByUserName { get; private set; }
    public string? ExternalReference { get; private set; }

    public static Result<CashTransaction> CreateIncome(
        Guid tenantId,
        decimal amount,
        string paymentMethod,
        string description,
        DateTimeOffset? occurredAtUtc = null,
        Guid? workOrderId = null,
        Guid? registeredByUserId = null,
        string? registeredByUserName = null,
        string? externalReference = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CashTransaction>.Failure(new Error("cash_transaction.tenant.required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (amount <= 0)
        {
            return Result<CashTransaction>.Failure(new Error("cash_transaction.amount.invalid", "O valor da receita deve ser maior que zero.", ErrorType.Validation));
        }

        if (!PaymentMethodConstants.IsValid(paymentMethod))
        {
            return Result<CashTransaction>.Failure(new Error("cash_transaction.payment_method.invalid", $"Forma de pagamento '{paymentMethod}' inválida.", ErrorType.Validation));
        }

        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? "Receita avulsa" : description.Trim();
        if (normalizedDescription.Length > 300)
        {
            return Result<CashTransaction>.Failure(new Error("cash_transaction.description.too_long", "A descrição não pode exceder 300 caracteres.", ErrorType.Validation));
        }

        return Result<CashTransaction>.Success(new CashTransaction(
            Guid.CreateVersion7(),
            tenantId,
            workOrderId,
            CashTransactionType.Income,
            paymentMethod.Trim(),
            amount,
            normalizedDescription,
            occurredAtUtc ?? DateTimeOffset.UtcNow,
            registeredByUserId,
            string.IsNullOrWhiteSpace(registeredByUserName) ? null : registeredByUserName.Trim(),
            string.IsNullOrWhiteSpace(externalReference) ? null : externalReference.Trim()));
    }

    public static Result<CashTransaction> CreateBleed(
        Guid tenantId,
        decimal amount,
        string description,
        DateTimeOffset? occurredAtUtc = null,
        Guid? registeredByUserId = null,
        string? registeredByUserName = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CashTransaction>.Failure(new Error("cash_transaction.tenant.required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (amount <= 0)
        {
            return Result<CashTransaction>.Failure(new Error("cash_transaction.amount.invalid", "O valor da sangria deve ser maior que zero.", ErrorType.Validation));
        }

        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDescription))
        {
            return Result<CashTransaction>.Failure(new Error("cash_transaction.description.required", "A justificativa/descrição da sangria é obrigatória.", ErrorType.Validation));
        }

        if (normalizedDescription.Length > 300)
        {
            return Result<CashTransaction>.Failure(new Error("cash_transaction.description.too_long", "A descrição não pode exceder 300 caracteres.", ErrorType.Validation));
        }

        return Result<CashTransaction>.Success(new CashTransaction(
            Guid.CreateVersion7(),
            tenantId,
            null,
            CashTransactionType.Bleed,
            PaymentMethodConstants.Cash,
            amount,
            normalizedDescription,
            occurredAtUtc ?? DateTimeOffset.UtcNow,
            registeredByUserId,
            string.IsNullOrWhiteSpace(registeredByUserName) ? null : registeredByUserName.Trim(),
            null));
    }

    public static Result<CashTransaction> CreateSupply(
        Guid tenantId,
        decimal amount,
        string description,
        DateTimeOffset? occurredAtUtc = null,
        Guid? registeredByUserId = null,
        string? registeredByUserName = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CashTransaction>.Failure(new Error("cash_transaction.tenant.required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (amount <= 0)
        {
            return Result<CashTransaction>.Failure(new Error("cash_transaction.amount.invalid", "O valor do aporte deve ser maior que zero.", ErrorType.Validation));
        }

        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? "Aporte inicial / reforço de troco" : description.Trim();
        if (normalizedDescription.Length > 300)
        {
            return Result<CashTransaction>.Failure(new Error("cash_transaction.description.too_long", "A descrição não pode exceder 300 caracteres.", ErrorType.Validation));
        }

        return Result<CashTransaction>.Success(new CashTransaction(
            Guid.CreateVersion7(),
            tenantId,
            null,
            CashTransactionType.Supply,
            PaymentMethodConstants.Cash,
            amount,
            normalizedDescription,
            occurredAtUtc ?? DateTimeOffset.UtcNow,
            registeredByUserId,
            string.IsNullOrWhiteSpace(registeredByUserName) ? null : registeredByUserName.Trim(),
            null));
    }
}
