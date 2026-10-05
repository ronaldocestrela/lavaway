using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Domain;

public sealed class PixCharge : IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public Guid WorkOrderId { get; private set; }
    public decimal Amount { get; private set; }
    public string Status { get; private set; } = PixChargeStatusConstants.Pending;
    public string TxId { get; private set; } = string.Empty;
    public string QrCodeBase64 { get; private set; } = string.Empty;
    public string CopyPasteKey { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? WhatsAppSentAtUtc { get; private set; }
    public DateTimeOffset? PaidAtUtc { get; private set; }

    private PixCharge()
    {
    }

    public static Result<PixCharge> Create(
        Guid tenantId,
        Guid workOrderId,
        decimal amount,
        string txId,
        string qrCodeBase64,
        string copyPasteKey,
        DateTimeOffset expiresAtUtc)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<PixCharge>.Failure(new Error("billing.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (workOrderId == Guid.Empty)
        {
            return Result<PixCharge>.Failure(new Error("billing.work_order_required", "Ordem de serviço é obrigatória.", ErrorType.Validation));
        }

        if (amount <= 0)
        {
            return Result<PixCharge>.Failure(new Error("billing.amount_invalid", "O valor da cobrança deve ser maior que zero.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(txId))
        {
            return Result<PixCharge>.Failure(new Error("billing.txid_required", "Identificador de transação (TxId) é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(copyPasteKey))
        {
            return Result<PixCharge>.Failure(new Error("billing.copypaste_required", "Código Copia e Cola é obrigatório.", ErrorType.Validation));
        }

        if (expiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return Result<PixCharge>.Failure(new Error("billing.expiration_invalid", "A expiração da cobrança deve estar no futuro.", ErrorType.Validation));
        }

        var charge = new PixCharge
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            WorkOrderId = workOrderId,
            Amount = amount,
            Status = PixChargeStatusConstants.Pending,
            TxId = txId.Trim(),
            QrCodeBase64 = qrCodeBase64.Trim(),
            CopyPasteKey = copyPasteKey.Trim(),
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        return Result<PixCharge>.Success(charge);
    }

    public Result MarkAsSentToWhatsApp(DateTimeOffset sentAtUtc)
    {
        WhatsAppSentAtUtc = sentAtUtc;
        return Result.Success();
    }

    public Result Expire()
    {
        if (Status == PixChargeStatusConstants.Paid)
        {
            return Result.Failure(new Error("billing.already_paid", "Cobrança já se encontra paga.", ErrorType.Conflict));
        }

        Status = PixChargeStatusConstants.Expired;
        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status == PixChargeStatusConstants.Paid)
        {
            return Result.Failure(new Error("billing.already_paid", "Cobrança já se encontra paga.", ErrorType.Conflict));
        }

        Status = PixChargeStatusConstants.Cancelled;
        return Result.Success();
    }

    public Result MarkAsPaid(DateTimeOffset paidAtUtc)
    {
        if (Status == PixChargeStatusConstants.Paid)
        {
            return Result.Success(); // Idempotente
        }

        Status = PixChargeStatusConstants.Paid;
        PaidAtUtc = paidAtUtc;
        return Result.Success();
    }

    public bool IsActiveAndPending() =>
        Status == PixChargeStatusConstants.Pending && ExpiresAtUtc > DateTimeOffset.UtcNow;
}
