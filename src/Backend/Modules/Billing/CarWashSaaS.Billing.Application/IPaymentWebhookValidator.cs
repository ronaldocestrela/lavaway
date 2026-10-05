using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Application;

public interface IPaymentWebhookValidator
{
    Result ValidateMercadoPagoSignature(
        string? xSignatureHeader,
        string? xRequestIdHeader,
        string? dataId,
        string webhookSecret);

    Result ValidateSimulatedSecret(
        string? providedSecret,
        string expectedSecret);
}
