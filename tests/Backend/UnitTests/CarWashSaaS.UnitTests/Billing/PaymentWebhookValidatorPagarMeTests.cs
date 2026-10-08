using System.Security.Cryptography;
using System.Text;
using CarWashSaaS.Billing.Infrastructure.Webhooks;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class PaymentWebhookValidatorPagarMeTests
{
    private readonly PaymentWebhookValidator _validator = new();

    [Fact]
    public void ValidatePagarMeWebhook_WithValidHmacSha256_ShouldSucceed()
    {
        var secret = "minha_chave_secreta_webhook_123";
        var payload = "{\"id\":\"hook_123\",\"type\":\"charge.paid\"}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        var header = $"sha256={hash}";

        var result = _validator.ValidatePagarMeWebhook(header, payload, secret);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidatePagarMeWebhook_WithInvalidHmacSha256_ShouldFail()
    {
        var secret = "minha_chave_secreta_webhook_123";
        var payload = "{\"id\":\"hook_123\",\"type\":\"charge.paid\"}";
        var header = "sha256=invalidhash1234567890abcdef";

        var result = _validator.ValidatePagarMeWebhook(header, payload, secret);

        Assert.False(result.IsSuccess);
        Assert.Equal("webhook.signature_invalid", result.Error?.Code);
    }

    [Fact]
    public void ValidatePagarMeWebhook_WithMatchingSecretToken_ShouldSucceed()
    {
        var secret = "token_secreto_personalizado";
        var payload = "{\"id\":\"hook_456\",\"type\":\"order.paid\"}";

        var result = _validator.ValidatePagarMeWebhook(secret, payload, secret);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidatePagarMeWebhook_WithMismatchedSecretToken_ShouldFail()
    {
        var secret = "token_correto";
        var payload = "{\"id\":\"hook_456\"}";

        var result = _validator.ValidatePagarMeWebhook("token_errado", payload, secret);

        Assert.False(result.IsSuccess);
        Assert.Equal("webhook.secret_invalid", result.Error?.Code);
    }

    [Fact]
    public void ValidatePagarMeWebhook_WhenHeaderMissing_ShouldFail()
    {
        var result = _validator.ValidatePagarMeWebhook(null, "{}", "secret");

        Assert.False(result.IsSuccess);
        Assert.Equal("webhook.signature_missing", result.Error?.Code);
    }
}
