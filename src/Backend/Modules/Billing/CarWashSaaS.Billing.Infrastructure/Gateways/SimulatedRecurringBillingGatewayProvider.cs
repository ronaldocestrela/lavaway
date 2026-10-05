using CarWashSaaS.Billing.Application;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Infrastructure.Gateways;

public sealed class SimulatedRecurringBillingGatewayProvider : IRecurringBillingGatewayProvider
{
    public Task<Result<RecurringGatewaySubscriptionData>> CreateSubscriptionAsync(
        Guid tenantId,
        Guid customerId,
        string customerName,
        decimal monthlyAmount,
        string? cardNumber,
        string? cardHolderName,
        string? cardExpiration,
        string? cardCvv,
        CancellationToken ct = default)
    {
        var cleanCard = new string((cardNumber ?? string.Empty).Where(char.IsDigit).ToArray());

        // Permite simular rejeição de cartão com final 0000 em testes
        if (cleanCard.EndsWith("0000", StringComparison.Ordinal))
        {
            return Task.FromResult(Result<RecurringGatewaySubscriptionData>.Failure(
                new Error("gateway.card_declined", "Cartão de crédito recusado pela operadora (simulação).", ErrorType.Validation)));
        }

        var lastFour = cleanCard.Length >= 4
            ? cleanCard[^4..]
            : "4242";

        var brand = DetectCardBrand(cleanCard);
        var gatewayId = $"SUB-{Guid.NewGuid():N}"[..16].ToUpperInvariant();

        return Task.FromResult(Result<RecurringGatewaySubscriptionData>.Success(
            new RecurringGatewaySubscriptionData(
                gatewayId,
                lastFour,
                brand,
                true)));
    }

    public Task<Result> CancelSubscriptionAsync(
        Guid tenantId,
        string gatewaySubscriptionId,
        CancellationToken ct = default)
    {
        return Task.FromResult(Result.Success());
    }

    private static string DetectCardBrand(string digits)
    {
        if (digits.StartsWith('4')) return "Visa";
        if (digits.StartsWith("51") || digits.StartsWith("52") || digits.StartsWith("53") || digits.StartsWith("54") || digits.StartsWith("55")) return "Mastercard";
        if (digits.StartsWith("34") || digits.StartsWith("37")) return "Amex";
        if (digits.StartsWith("60") || digits.StartsWith("65")) return "Elo";
        return "Visa";
    }
}
