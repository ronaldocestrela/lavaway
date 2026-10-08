using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarWashSaaS.Billing.Application;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.Billing.Infrastructure.Gateways;

public sealed class PagarMePixGatewayProvider(
    HttpClient httpClient,
    string secretKey,
    string? publicKey = null,
    ILogger<PagarMePixGatewayProvider>? logger = null) : IPixGatewayProvider
{
    private const string BaseApiUrl = "https://api.pagar.me/core/v5";

    public string ProviderName => "PagarMe";
    public string? PublicKey => publicKey;

    public async Task<Result<PixGatewayChargeResponse>> CreateImmediateChargeAsync(
        PixGatewayChargeRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return Result<PixGatewayChargeResponse>.Failure(
                new Error("pagarme.secret_key_missing", "Chave secreta da Pagar.me não configurada para este estabelecimento.", ErrorType.Validation));
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseApiUrl}/orders");
            var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{secretKey.Trim()}:"));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            httpRequest.Headers.Add("X-Idempotency-Key", $"lavaway-{request.TenantId}-{request.WorkOrderId}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");

            var amountInCents = (int)Math.Round(request.Amount * 100m, MidpointRounding.AwayFromZero);
            var expiresInSeconds = (int)Math.Max(60, request.Expiration.TotalSeconds);

            var phoneDigits = new string((request.CustomerPhone ?? string.Empty).Where(char.IsDigit).ToArray());
            var areaCode = phoneDigits.Length >= 10 ? phoneDigits[..2] : "11";
            var phoneNumber = phoneDigits.Length >= 10 ? phoneDigits[2..] : (phoneDigits.Length > 0 ? phoneDigits : "999999999");

            var payload = new PagarMeOrderRequest(
                Customer: new PagarMeCustomer(
                    Name: string.IsNullOrWhiteSpace(request.CustomerName) ? "Cliente Lavaway" : request.CustomerName.Trim(),
                    Email: string.IsNullOrWhiteSpace(phoneDigits) ? "cliente@lavaway.com.br" : $"{phoneDigits}@lavaway.com.br",
                    Phones: new PagarMePhones(
                        MobilePhone: new PagarMePhone(
                            CountryCode: "55",
                            AreaCode: areaCode,
                            Number: phoneNumber))),
                Items:
                [
                    new PagarMeOrderItem(
                        Amount: amountInCents,
                        Description: string.IsNullOrWhiteSpace(request.Description) ? "Serviço Lava-Jato" : request.Description.Trim(),
                        Quantity: 1,
                        Code: $"OS-{request.WorkOrderId.ToString()[..8].ToUpperInvariant()}")
                ],
                Payments:
                [
                    new PagarMePayment(
                        PaymentMethod: "pix",
                        Pix: new PagarMePixPaymentData(
                            ExpiresIn: expiresInSeconds,
                            AdditionalInformation:
                            [
                                new PagarMePixAdditionalInfo("Ordem de Serviço", request.WorkOrderId.ToString())
                            ]))
                ]);

            httpRequest.Content = JsonContent.Create(payload);

            var httpResponse = await httpClient.SendAsync(httpRequest, ct);
            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorBody = await httpResponse.Content.ReadAsStringAsync(ct);
                logger?.LogWarning("Pagar.me API error ({StatusCode}): {Body}", httpResponse.StatusCode, errorBody);
                return Result<PixGatewayChargeResponse>.Failure(
                    new Error("pagarme.api_error", $"Falha ao gerar cobrança na Pagar.me: {httpResponse.StatusCode}", ErrorType.Validation));
            }

            var responseDto = await httpResponse.Content.ReadFromJsonAsync<PagarMeOrderResponse>(cancellationToken: ct);
            var firstCharge = responseDto?.Charges?.FirstOrDefault();
            var lastTransaction = firstCharge?.LastTransaction;

            if (lastTransaction is null || string.IsNullOrWhiteSpace(lastTransaction.QrCode))
            {
                return Result<PixGatewayChargeResponse>.Failure(
                    new Error("pagarme.invalid_response", "A resposta da Pagar.me não contém os dados do QR Code Pix.", ErrorType.Validation));
            }

            var txId = !string.IsNullOrWhiteSpace(firstCharge?.Id) ? firstCharge.Id : lastTransaction.Id ?? Guid.NewGuid().ToString();
            var copyPasteKey = lastTransaction.QrCode;
            var qrCodeSvg = GenerateQrCodeSvg(copyPasteKey);
            var qrCodeBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(qrCodeSvg));

            var expiresAtUtc = DateTimeOffset.TryParse(lastTransaction.ExpiresAt, out var exp)
                ? exp
                : DateTimeOffset.UtcNow.Add(request.Expiration);

            return Result<PixGatewayChargeResponse>.Success(new PixGatewayChargeResponse(
                txId,
                qrCodeBase64,
                copyPasteKey,
                expiresAtUtc));
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unexpected error calling Pagar.me API");
            return Result<PixGatewayChargeResponse>.Failure(new Error("pagarme.exception", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<PixGatewayPaymentDetails>> GetPaymentDetailsAsync(
        Guid tenantId,
        string paymentIdOrTxId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return Result<PixGatewayPaymentDetails>.Failure(
                new Error("pagarme.secret_key_missing", "Chave secreta da Pagar.me não configurada.", ErrorType.Validation));
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{BaseApiUrl}/charges/{paymentIdOrTxId}");
            var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{secretKey.Trim()}:"));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);

            var httpResponse = await httpClient.SendAsync(httpRequest, ct);
            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorBody = await httpResponse.Content.ReadAsStringAsync(ct);
                logger?.LogWarning("Pagar.me GET charge error ({StatusCode}): {Body}", httpResponse.StatusCode, errorBody);
                return Result<PixGatewayPaymentDetails>.Failure(
                    new Error("pagarme.query_error", $"Falha ao consultar cobrança na Pagar.me: {httpResponse.StatusCode}", ErrorType.Validation));
            }

            var responseDto = await httpResponse.Content.ReadFromJsonAsync<PagarMeChargeResponse>(cancellationToken: ct);
            if (responseDto is null)
            {
                return Result<PixGatewayPaymentDetails>.Failure(
                    new Error("pagarme.invalid_response", "Resposta inválida ao consultar cobrança na Pagar.me.", ErrorType.Validation));
            }

            var isPaid = string.Equals(responseDto.Status, "paid", StringComparison.OrdinalIgnoreCase);
            DateTimeOffset? paidAt = null;
            if (!string.IsNullOrWhiteSpace(responseDto.PaidAt) && DateTimeOffset.TryParse(responseDto.PaidAt, out var parsed))
            {
                paidAt = parsed;
            }

            var amount = (responseDto.PaidAmount ?? responseDto.Amount) / 100m;

            return Result<PixGatewayPaymentDetails>.Success(new PixGatewayPaymentDetails(
                responseDto.Id ?? paymentIdOrTxId,
                isPaid ? "approved" : (responseDto.Status ?? "unknown"),
                amount,
                paidAt));
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unexpected error querying Pagar.me charge {ChargeId}", paymentIdOrTxId);
            return Result<PixGatewayPaymentDetails>.Failure(new Error("pagarme.exception", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<bool>> TestCredentialsAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return Result<bool>.Failure(new Error("pagarme.secret_required", "A chave secreta da Pagar.me é obrigatória.", ErrorType.Validation));
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{BaseApiUrl}/customers?page=1&size=1");
            var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{secretKey.Trim()}:"));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);

            var httpResponse = await httpClient.SendAsync(httpRequest, ct);
            if (httpResponse.IsSuccessStatusCode)
            {
                return Result<bool>.Success(true);
            }

            if (httpResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return Result<bool>.Failure(new Error("pagarme.unauthorized", "Chave secreta inválida ou sem permissão na Pagar.me (401 Unauthorized).", ErrorType.Unauthorized));
            }

            return Result<bool>.Failure(new Error("pagarme.test_failed", $"A API da Pagar.me respondeu com status {httpResponse.StatusCode}.", ErrorType.Validation));
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to test Pagar.me credentials");
            return Result<bool>.Failure(new Error("pagarme.network_error", $"Erro de conexão com a Pagar.me: {ex.Message}", ErrorType.Validation));
        }
    }

    private static string GenerateQrCodeSvg(string payload)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        var size = 25;
        var cellSize = 10;
        var totalSize = size * cellSize;

        var svg = new StringBuilder();
        svg.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {totalSize} {totalSize}\" width=\"100%\" height=\"100%\">");
        svg.Append($"<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>");

        void DrawFinderPattern(int startX, int startY)
        {
            svg.Append($"<rect x=\"{startX * cellSize}\" y=\"{startY * cellSize}\" width=\"{7 * cellSize}\" height=\"{7 * cellSize}\" fill=\"#000000\"/>");
            svg.Append($"<rect x=\"{(startX + 1) * cellSize}\" y=\"{(startY + 1) * cellSize}\" width=\"{5 * cellSize}\" height=\"{5 * cellSize}\" fill=\"#ffffff\"/>");
            svg.Append($"<rect x=\"{(startX + 2) * cellSize}\" y=\"{(startY + 2) * cellSize}\" width=\"{3 * cellSize}\" height=\"{3 * cellSize}\" fill=\"#000000\"/>");
        }

        DrawFinderPattern(0, 0);
        DrawFinderPattern(size - 7, 0);
        DrawFinderPattern(0, size - 7);

        for (var r = 0; r < size; r++)
        {
            for (var c = 0; c < size; c++)
            {
                if ((r < 8 && c < 8) || (r < 8 && c >= size - 8) || (r >= size - 8 && c < 8))
                {
                    continue;
                }

                var byteIndex = (r * size + c) % hash.Length;
                var bitIndex = (r + c) % 8;
                var isDark = ((hash[byteIndex] >> bitIndex) & 1) == 1;

                if (isDark)
                {
                    svg.Append($"<rect x=\"{c * cellSize}\" y=\"{r * cellSize}\" width=\"{cellSize}\" height=\"{cellSize}\" fill=\"#111827\"/>");
                }
            }
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    private sealed record PagarMeOrderRequest(
        [property: JsonPropertyName("customer")] PagarMeCustomer Customer,
        [property: JsonPropertyName("items")] IReadOnlyList<PagarMeOrderItem> Items,
        [property: JsonPropertyName("payments")] IReadOnlyList<PagarMePayment> Payments);

    private sealed record PagarMeCustomer(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("phones")] PagarMePhones Phones);

    private sealed record PagarMePhones(
        [property: JsonPropertyName("mobile_phone")] PagarMePhone MobilePhone);

    private sealed record PagarMePhone(
        [property: JsonPropertyName("country_code")] string CountryCode,
        [property: JsonPropertyName("area_code")] string AreaCode,
        [property: JsonPropertyName("number")] string Number);

    private sealed record PagarMeOrderItem(
        [property: JsonPropertyName("amount")] int Amount,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("quantity")] int Quantity,
        [property: JsonPropertyName("code")] string Code);

    private sealed record PagarMePayment(
        [property: JsonPropertyName("payment_method")] string PaymentMethod,
        [property: JsonPropertyName("pix")] PagarMePixPaymentData Pix);

    private sealed record PagarMePixPaymentData(
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("additional_information")] IReadOnlyList<PagarMePixAdditionalInfo> AdditionalInformation);

    private sealed record PagarMePixAdditionalInfo(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("value")] string Value);

    private sealed record PagarMeOrderResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("charges")] IReadOnlyList<PagarMeChargeResponse>? Charges);

    private sealed record PagarMeChargeResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("paid_amount")] decimal? PaidAmount,
        [property: JsonPropertyName("paid_at")] string? PaidAt,
        [property: JsonPropertyName("last_transaction")] PagarMeTransactionResponse? LastTransaction);

    private sealed record PagarMeTransactionResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("transaction_type")] string? TransactionType,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("qr_code")] string? QrCode,
        [property: JsonPropertyName("qr_code_url")] string? QrCodeUrl,
        [property: JsonPropertyName("expires_at")] string? ExpiresAt);
}
