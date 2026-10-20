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

        var pixCopiaECola = $"00020126580014br.gov.bcb.pix0136{tenantId}520400005303986540{plan.MonthlyPrice:F2}5802BR5915LAVAAE SAAS6009SAO PAULO62070503***6304ABCD";
        var pixQrCode = $"https://api.qrserver.com/v1/create-qr-code/?size=250x250&data={Uri.EscapeDataString(pixCopiaECola)}";
        var paymentUrl = $"https://billing.lavaae.com/pay/{invoiceId}";

        return Task.FromResult(Result<GatewaySubscriptionResult>.Success(new GatewaySubscriptionResult(
            GatewayCustomerId: customerId,
            GatewaySubscriptionId: subscriptionId,
            InitialInvoiceId: invoiceId,
            PaymentUrl: paymentUrl,
            PixQrCode: pixQrCode,
            PixCopiaECola: pixCopiaECola)));
    }

    public Task<Result<GatewayCheckoutSessionResult>> CreateSubscriptionCheckoutSessionAsync(
        Guid tenantId,
        string customerName,
        string customerEmail,
        SaasPlan plan,
        string successUrl,
        string cancelUrl,
        CancellationToken ct = default)
    {
        var sessionId = $"cs_simulated_{tenantId:N}[..8]_{Guid.NewGuid():N}[..8]";
        var checkoutUrl = $"https://checkout.stripe.com/pay/{sessionId}";
        return Task.FromResult(Result<GatewayCheckoutSessionResult>.Success(new GatewayCheckoutSessionResult(sessionId, checkoutUrl)));
    }

    public Task<Result<GatewayCheckoutSessionDetails>> GetCheckoutSessionAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        return Task.FromResult(Result<GatewayCheckoutSessionDetails>.Success(new GatewayCheckoutSessionDetails(
            SessionId: sessionId,
            TenantId: Guid.Empty,
            PlanTier: null,
            GatewayCustomerId: "cus_simulated",
            GatewaySubscriptionId: "sub_simulated",
            PaymentStatus: "paid",
            Status: "complete")));
    }

    public Task<Result<GatewayPortalSessionResult>> CreateCustomerPortalSessionAsync(
        string gatewayCustomerId,
        string returnUrl,
        CancellationToken ct = default)
    {
        var portalUrl = $"https://billing.stripe.com/p/session/simulated_{Guid.NewGuid():N}[..12]";
        return Task.FromResult(Result<GatewayPortalSessionResult>.Success(new GatewayPortalSessionResult(portalUrl)));
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
        var pixCopiaECola = $"00020126580014br.gov.bcb.pix0136{tenantId}520400005303986540{amount:F2}5802BR5915LAVAAE SAAS6009SAO PAULO62070503***6304EF12";
        var pixQrCode = $"https://api.qrserver.com/v1/create-qr-code/?size=250x250&data={Uri.EscapeDataString(pixCopiaECola)}";
        var paymentUrl = $"https://billing.lavaae.com/pay/{invoiceId}";

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
