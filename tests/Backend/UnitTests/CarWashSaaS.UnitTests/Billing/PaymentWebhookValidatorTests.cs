using System.Security.Cryptography;
using System.Text;
using CarWashSaaS.Billing.Infrastructure.Webhooks;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class PaymentWebhookValidatorTests
{
    private readonly PaymentWebhookValidator _validator = new();

    [Fact]
    public void ValidateMercadoPagoSignature_WhenSignatureIsValid_ShouldReturnSuccess()
    {
        var secret = "secret_key_12345";
        var dataId = "123456789";
        var requestId = "req-abc-987";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var manifest = $"id:{dataId};request-id:{requestId};ts:{ts};";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest)));

        var xSignature = $"ts={ts},v1={hash}";

        var result = _validator.ValidateMercadoPagoSignature(xSignature, requestId, dataId, secret);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateMercadoPagoSignature_WhenSignatureHashMismatch_ShouldReturnUnauthorized()
    {
        var secret = "secret_key_12345";
        var dataId = "123456789";
        var requestId = "req-abc-987";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var xSignature = $"ts={ts},v1=wrong_hash_value_here";

        var result = _validator.ValidateMercadoPagoSignature(xSignature, requestId, dataId, secret);

        Assert.False(result.IsSuccess);
        Assert.Equal("webhook.signature_invalid", result.Error?.Code);
        Assert.Equal(ErrorType.Unauthorized, result.Error?.Type);
    }

    [Fact]
    public void ValidateMercadoPagoSignature_WhenTimestampExpired_ShouldReturnReplayAttackError()
    {
        var secret = "secret_key_12345";
        var dataId = "123456789";
        var requestId = "req-abc-987";
        // 10 minutos atrás (fora da janela de 5 minutos)
        var ts = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();

        var manifest = $"id:{dataId};request-id:{requestId};ts:{ts};";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest)));

        var xSignature = $"ts={ts},v1={hash}";

        var result = _validator.ValidateMercadoPagoSignature(xSignature, requestId, dataId, secret);

        Assert.False(result.IsSuccess);
        Assert.Equal("webhook.timestamp_expired", result.Error?.Code);
    }

    [Fact]
    public void ValidateSimulatedSecret_WhenMatch_ShouldReturnSuccess()
    {
        var result = _validator.ValidateSimulatedSecret("MySecret123!", "MySecret123!");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateSimulatedSecret_WhenMismatch_ShouldReturnUnauthorized()
    {
        var result = _validator.ValidateSimulatedSecret("WrongSecret", "MySecret123!");
        Assert.False(result.IsSuccess);
        Assert.Equal("webhook.secret_mismatch", result.Error?.Code);
    }

    [Fact]
    public void ValidateMercadoPagoSignature_WhenDataIdHasUppercase_ShouldNormalizeAndSucceed()
    {
        var secret = "secret_key_12345";
        var dataIdProvided = "AbC-123-XyZ";
        var requestId = "req-abc-987";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        // Mercado Pago spec requires lowercase dataId in manifest
        var manifest = $"id:{dataIdProvided.ToLowerInvariant()};request-id:{requestId};ts:{ts};";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest)));

        var xSignature = $"ts={ts},v1={hash}";

        var result = _validator.ValidateMercadoPagoSignature(xSignature, requestId, dataIdProvided, secret);

        Assert.True(result.IsSuccess);
    }
}

