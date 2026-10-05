using System.Security.Cryptography;
using System.Text;
using CarWashSaaS.Billing.Application;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Infrastructure.Webhooks;

public sealed class PaymentWebhookValidator : IPaymentWebhookValidator
{
    private static readonly TimeSpan MaxTimestampDrift = TimeSpan.FromMinutes(5);

    public Result ValidateMercadoPagoSignature(
        string? xSignatureHeader,
        string? xRequestIdHeader,
        string? dataId,
        string webhookSecret)
    {
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return Result.Failure(new Error("webhook.secret_not_configured", "Segredo do webhook não configurado.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(xSignatureHeader))
        {
            return Result.Failure(new Error("webhook.signature_missing", "Cabeçalho x-signature ausente.", ErrorType.Unauthorized));
        }

        // Formato esperado de x-signature do Mercado Pago: ts=1700000000,v1=5d6...
        string? ts = null;
        string? v1Hash = null;

        var parts = xSignatureHeader.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (keyValue.Length == 2)
            {
                if (keyValue[0].Equals("ts", StringComparison.OrdinalIgnoreCase))
                {
                    ts = keyValue[1];
                }
                else if (keyValue[0].Equals("v1", StringComparison.OrdinalIgnoreCase))
                {
                    v1Hash = keyValue[1];
                }
            }
        }

        if (string.IsNullOrWhiteSpace(ts) || string.IsNullOrWhiteSpace(v1Hash))
        {
            return Result.Failure(new Error("webhook.signature_malformed", "Formato inválido do cabeçalho x-signature.", ErrorType.Unauthorized));
        }

        // Validação anti-replay attack via timestamp
        if (!long.TryParse(ts, out var timestampSeconds))
        {
            return Result.Failure(new Error("webhook.timestamp_invalid", "Timestamp da assinatura inválido.", ErrorType.Unauthorized));
        }

        var eventTime = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds);
        var now = DateTimeOffset.UtcNow;
        if (now - eventTime > MaxTimestampDrift || eventTime - now > MaxTimestampDrift)
        {
            return Result.Failure(new Error("webhook.timestamp_expired", "Timestamp do webhook fora da janela permitida (replay attack).", ErrorType.Unauthorized));
        }

        // Manifest do Mercado Pago: id:[data.id_or_request_id];request-id:[x-request-id];ts:[ts];
        var manifestBuilder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(dataId))
        {
            manifestBuilder.Append($"id:{dataId};");
        }

        if (!string.IsNullOrWhiteSpace(xRequestIdHeader))
        {
            manifestBuilder.Append($"request-id:{xRequestIdHeader};");
        }

        manifestBuilder.Append($"ts:{ts};");

        var manifest = manifestBuilder.ToString();
        var secretBytes = Encoding.UTF8.GetBytes(webhookSecret);
        var manifestBytes = Encoding.UTF8.GetBytes(manifest);

        using var hmac = new HMACSHA256(secretBytes);
        var computedHashBytes = hmac.ComputeHash(manifestBytes);
        var computedHex = Convert.ToHexStringLower(computedHashBytes);

        var expectedBytes = Encoding.UTF8.GetBytes(computedHex);
        var providedBytes = Encoding.UTF8.GetBytes(v1Hash.ToLowerInvariant());

        if (expectedBytes.Length != providedBytes.Length || !CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
        {
            return Result.Failure(new Error("webhook.signature_invalid", "Assinatura do webhook inválida.", ErrorType.Unauthorized));
        }

        return Result.Success();
    }

    public Result ValidateSimulatedSecret(
        string? providedSecret,
        string expectedSecret)
    {
        if (string.IsNullOrWhiteSpace(expectedSecret))
        {
            return Result.Failure(new Error("webhook.secret_not_configured", "Segredo esperado não configurado.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(providedSecret))
        {
            return Result.Failure(new Error("webhook.secret_missing", "Segredo do webhook ausente.", ErrorType.Unauthorized));
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedSecret);
        var providedBytes = Encoding.UTF8.GetBytes(providedSecret);

        if (expectedBytes.Length != providedBytes.Length || !CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
        {
            return Result.Failure(new Error("webhook.secret_mismatch", "Segredo do webhook incorreto.", ErrorType.Unauthorized));
        }

        return Result.Success();
    }
}
