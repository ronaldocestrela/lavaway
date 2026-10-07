using System.Security.Cryptography;
using System.Text;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Infrastructure;

public sealed class SimulatedSaasBillingGatewayProvider : ISaasBillingGatewayProvider
{
    public Task<Result<GatewaySubscriptionResult>> CreateSubscriptionAsync(
        Guid tenantId,
        string customerName,
        string customerEmail,
        SaasPlan plan,
        CancellationToken ct = default)
    {
        var customerId = $"cus_saas_{tenantId:N}[..12]";
        var subscriptionId = $"sub_saas_{Guid.NewGuid():N}[..12]";
        var invoiceId = $"inv_saas_{Guid.NewGuid():N}[..12]";

        var pixCopiaECola = $"00020126580014br.gov.bcb.pix0136{tenantId}520400005303986540{plan.MonthlyPrice:F2}5802BR5915LAVAWAY SAAS6009SAO PAULO62070503***6304ABCD";
        var pixQrCode = $"https://api.qrserver.com/v1/create-qr-code/?size=250x250&data={Uri.EscapeDataString(pixCopiaECola)}";
        var paymentUrl = $"https://billing.lavaway.com/pay/{invoiceId}";

        return Task.FromResult(Result<GatewaySubscriptionResult>.Success(new GatewaySubscriptionResult(
            GatewayCustomerId: customerId,
            GatewaySubscriptionId: subscriptionId,
            InitialInvoiceId: invoiceId,
            PaymentUrl: paymentUrl,
            PixQrCode: pixQrCode,
            PixCopiaECola: pixCopiaECola)));
    }

    public Task<Result<bool>> ChangeSubscriptionPlanAsync(
        string gatewaySubscriptionId,
        SaasPlan newPlan,
        CancellationToken ct = default)
    {
        return Task.FromResult(Result<bool>.Success(true));
    }

    public Task<Result<GatewayInvoiceResult>> GenerateInvoiceAsync(
        Guid tenantId,
        string gatewayCustomerId,
        decimal amount,
        DateTimeOffset dueDateUtc,
        CancellationToken ct = default)
    {
        var invoiceId = $"inv_saas_{Guid.NewGuid():N}[..12]";
        var pixCopiaECola = $"00020126580014br.gov.bcb.pix0136{tenantId}520400005303986540{amount:F2}5802BR5915LAVAWAY SAAS6009SAO PAULO62070503***6304EF12";
        var pixQrCode = $"https://api.qrserver.com/v1/create-qr-code/?size=250x250&data={Uri.EscapeDataString(pixCopiaECola)}";
        var paymentUrl = $"https://billing.lavaway.com/pay/{invoiceId}";

        return Task.FromResult(Result<GatewayInvoiceResult>.Success(new GatewayInvoiceResult(
            GatewayInvoiceId: invoiceId,
            Amount: amount,
            DueDateUtc: dueDateUtc,
            PaymentUrl: paymentUrl,
            PixQrCode: pixQrCode,
            PixCopiaECola: pixCopiaECola)));
    }

    public bool VerifyWebhookSignature(string payload, string signatureHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computedSignature = Convert.ToHexString(hash).ToLowerInvariant();

        return string.Equals(signatureHeader.Trim().ToLowerInvariant(), computedSignature, StringComparison.OrdinalIgnoreCase);
    }
}
