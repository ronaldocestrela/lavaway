namespace CarWashSaaS.Shared.Contracts;

public static class PaymentMethodConstants
{
    public const string Pix = "Pix";
    public const string Cash = "Cash";
    public const string CreditCard = "CreditCard";
    public const string DebitCard = "DebitCard";
    public const string SubscriptionCredit = "SubscriptionCredit";

    public static readonly IReadOnlyList<string> All = [Pix, Cash, CreditCard, DebitCard, SubscriptionCredit];

    public static string ToDisplayName(string method) => method switch
    {
        Pix => "Pix",
        Cash => "Dinheiro",
        CreditCard => "Cartão de Crédito",
        DebitCard => "Cartão de Débito",
        SubscriptionCredit => "Crédito de Assinatura",
        _ => method
    };

    public static bool IsValid(string? method) =>
        !string.IsNullOrWhiteSpace(method) &&
        All.Contains(method.Trim(), StringComparer.OrdinalIgnoreCase);
}

