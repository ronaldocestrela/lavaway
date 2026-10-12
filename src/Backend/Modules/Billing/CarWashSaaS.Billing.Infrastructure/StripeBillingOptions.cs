namespace CarWashSaaS.Billing.Infrastructure;

public sealed class StripeBillingOptions
{
    public const string SectionName = "Stripe";

    public string? SecretKey { get; set; }
    public string? PublishableKey { get; set; }
    public string? WebhookSecret { get; set; }
    public string? DefaultSuccessUrl { get; set; }
    public string? DefaultCancelUrl { get; set; }
    public string Currency { get; set; } = "brl";
}
