namespace CarWashSaaS.Shared.Contracts;

public static class PaymentGatewayProviderConstants
{
    public const string PagarMe = "PagarMe";
    public const string MercadoPago = "MercadoPago";
    public const string Simulated = "Simulated";

    public static readonly IReadOnlyList<string> All = [PagarMe, MercadoPago, Simulated];

    public static bool IsValid(string? provider) =>
        !string.IsNullOrWhiteSpace(provider) &&
        All.Contains(provider.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string ToDisplayName(string provider) => provider switch
    {
        PagarMe => "Pagar.me (Stone Co.)",
        MercadoPago => "Mercado Pago",
        Simulated => "Ambiente Simulado (Testes)",
        _ => provider
    };
}

public sealed record TenantPaymentGatewayConfigDto(
    Guid TenantId,
    string Provider,
    string? PagarMePublicKey,
    string? PagarMeSecretKeyMasked,
    bool HasSecretKey,
    string? PagarMeWebhookSecretMasked,
    bool HasWebhookSecret,
    string? MercadoPagoPublicKey,
    string? MercadoPagoAccessTokenMasked,
    bool HasMercadoPagoAccessToken,
    string? MercadoPagoWebhookSecretMasked,
    bool HasMercadoPagoWebhookSecret,
    bool IsActive,
    string WebhookUrl,
    string PagarMeWebhookUrl,
    string MercadoPagoWebhookUrl,
    DateTimeOffset? LastTestedAtUtc,
    bool? LastTestSuccess,
    string? LastTestMessage);

public sealed record SaveTenantPaymentGatewayConfigRequest(
    string Provider,
    string? PagarMePublicKey = null,
    string? PagarMeSecretKey = null,
    string? PagarMeWebhookSecret = null,
    string? MercadoPagoPublicKey = null,
    string? MercadoPagoAccessToken = null,
    string? MercadoPagoWebhookSecret = null,
    bool IsActive = true);

public sealed record TestTenantGatewayConnectionRequest(
    string Provider,
    string? PagarMeSecretKey = null,
    string? MercadoPagoAccessToken = null);

public sealed record TestTenantGatewayConnectionResultDto(
    bool Success,
    string Message,
    DateTimeOffset TestedAtUtc);
