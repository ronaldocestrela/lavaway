using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Application;

public sealed record RecurringGatewaySubscriptionData(
    string GatewaySubscriptionId,
    string CardLastFourDigits,
    string CardBrand,
    bool IsSuccess,
    string? ErrorMessage = null);

public interface IRecurringBillingGatewayProvider
{
    Task<Result<RecurringGatewaySubscriptionData>> CreateSubscriptionAsync(
        Guid tenantId,
        Guid customerId,
        string customerName,
        decimal monthlyAmount,
        string? cardNumber,
        string? cardHolderName,
        string? cardExpiration,
        string? cardCvv,
        CancellationToken ct = default);

    Task<Result> CancelSubscriptionAsync(
        Guid tenantId,
        string gatewaySubscriptionId,
        CancellationToken ct = default);
}
