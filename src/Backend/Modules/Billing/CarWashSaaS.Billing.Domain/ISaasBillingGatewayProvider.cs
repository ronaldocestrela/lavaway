using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Domain;

public sealed record GatewaySubscriptionResult(
    string GatewayCustomerId,
    string GatewaySubscriptionId,
    string InitialInvoiceId,
    string? PaymentUrl,
    string? PixQrCode,
    string? PixCopiaECola);

public sealed record GatewayInvoiceResult(
    string GatewayInvoiceId,
    decimal Amount,
    DateTimeOffset DueDateUtc,
    string? PaymentUrl,
    string? PixQrCode,
    string? PixCopiaECola);

public interface ISaasBillingGatewayProvider
{
    Task<Result<GatewaySubscriptionResult>> CreateSubscriptionAsync(
        Guid tenantId,
        string customerName,
        string customerEmail,
        SaasPlan plan,
        CancellationToken ct = default);

    Task<Result<bool>> ChangeSubscriptionPlanAsync(
        string gatewaySubscriptionId,
        SaasPlan newPlan,
        CancellationToken ct = default);

    Task<Result<GatewayInvoiceResult>> GenerateInvoiceAsync(
        Guid tenantId,
        string gatewayCustomerId,
        decimal amount,
        DateTimeOffset dueDateUtc,
        CancellationToken ct = default);

    bool VerifyWebhookSignature(string payload, string signatureHeader, string secret);
}
