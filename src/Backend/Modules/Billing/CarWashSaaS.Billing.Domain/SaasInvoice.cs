using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Domain;

public sealed class SaasInvoice : IMustHaveTenant
{
    private SaasInvoice()
    {
    }

    private SaasInvoice(
        Guid id,
        Guid tenantId,
        string gatewayInvoiceId,
        decimal amount,
        DateTimeOffset dueDateUtc,
        DateTimeOffset? paidAtUtc,
        string status,
        string? paymentUrl,
        string? pixQrCode,
        string? pixCopiaECola)
    {
        Id = id;
        TenantId = tenantId;
        GatewayInvoiceId = gatewayInvoiceId;
        Amount = amount;
        DueDateUtc = dueDateUtc;
        PaidAtUtc = paidAtUtc;
        Status = status;
        PaymentUrl = paymentUrl;
        PixQrCode = pixQrCode;
        PixCopiaECola = pixCopiaECola;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    public string GatewayInvoiceId { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateTimeOffset DueDateUtc { get; private set; }
    public DateTimeOffset? PaidAtUtc { get; private set; }
    public string Status { get; private set; } = "Pending";
    public string? PaymentUrl { get; private set; }
    public string? PixQrCode { get; private set; }
    public string? PixCopiaECola { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<SaasInvoice> CreatePending(
        Guid tenantId,
        string gatewayInvoiceId,
        decimal amount,
        DateTimeOffset dueDateUtc,
        string? paymentUrl = null,
        string? pixQrCode = null,
        string? pixCopiaECola = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<SaasInvoice>.Failure(new Error("saas_invoice.tenant.required", "TenantId é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(gatewayInvoiceId))
        {
            return Result<SaasInvoice>.Failure(new Error("saas_invoice.gateway_id.required", "GatewayInvoiceId é obrigatório.", ErrorType.Validation));
        }

        if (amount < 0)
        {
            return Result<SaasInvoice>.Failure(new Error("saas_invoice.amount.invalid", "Valor da fatura não pode ser negativo.", ErrorType.Validation));
        }

        return Result<SaasInvoice>.Success(new SaasInvoice(
            Guid.CreateVersion7(),
            tenantId,
            gatewayInvoiceId.Trim(),
            amount,
            dueDateUtc,
            paidAtUtc: null,
            status: "Pending",
            paymentUrl,
            pixQrCode,
            pixCopiaECola));
    }

    public Result MarkPaid(DateTimeOffset? paidAt = null)
    {
        if (Status == "Paid")
        {
            return Result.Success();
        }

        Status = "Paid";
        PaidAtUtc = paidAt ?? DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result MarkOverdue()
    {
        if (Status == "Paid")
        {
            return Result.Failure(new Error("saas_invoice.already_paid", "Fatura já liquidada não pode ser marcada como em atraso.", ErrorType.Conflict));
        }

        Status = "Overdue";
        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status == "Paid")
        {
            return Result.Failure(new Error("saas_invoice.already_paid", "Fatura já liquidada não pode ser cancelada.", ErrorType.Conflict));
        }

        Status = "Canceled";
        return Result.Success();
    }
}
