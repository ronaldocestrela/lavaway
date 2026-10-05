using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarWashSaaS.Billing.Application;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.Billing.Infrastructure.Gateways;

public sealed class MercadoPagoPixGatewayProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<MercadoPagoPixGatewayProvider>? logger = null) : IPixGatewayProvider
{
    public string ProviderName => "MercadoPago";

    public async Task<Result<PixGatewayChargeResponse>> CreateImmediateChargeAsync(
        PixGatewayChargeRequest request,
        CancellationToken ct = default)
    {
        var accessToken = configuration["Billing:MercadoPago:AccessToken"];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            // Se não configurado credencial de produção, delega para fallback simulado
            var simulated = new SimulatedPixGatewayProvider();
            return await simulated.CreateImmediateChargeAsync(request, ct);
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.mercadopago.com/v1/payments");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.Add("X-Idempotency-Key", $"pix-{request.TenantId}-{request.WorkOrderId}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");

            var payload = new MercadoPagoPaymentRequest(
                TransactionAmount: request.Amount,
                Description: request.Description,
                PaymentMethodId: "pix",
                Payer: new MercadoPagoPayer(
                    Email: string.IsNullOrWhiteSpace(request.CustomerPhone) ? "cliente@lavaway.com.br" : $"{request.CustomerPhone}@lavaway.com.br",
                    FirstName: request.CustomerName),
                DateOfExpiration: DateTimeOffset.UtcNow.Add(request.Expiration).ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz"));

            httpRequest.Content = JsonContent.Create(payload);

            var httpResponse = await httpClient.SendAsync(httpRequest, ct);
            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorBody = await httpResponse.Content.ReadAsStringAsync(ct);
                logger?.LogWarning("Mercado Pago API error ({StatusCode}): {Body}", httpResponse.StatusCode, errorBody);
                return Result<PixGatewayChargeResponse>.Failure(new Error("mercadopago.error", $"Falha na comunicação com o Mercado Pago: {httpResponse.StatusCode}", ErrorType.Validation));
            }

            var responseDto = await httpResponse.Content.ReadFromJsonAsync<MercadoPagoPaymentResponse>(cancellationToken: ct);
            if (responseDto?.PointOfInteraction?.TransactionData is null)
            {
                return Result<PixGatewayChargeResponse>.Failure(new Error("mercadopago.invalid_response", "Resposta inválida do Mercado Pago ao gerar Pix.", ErrorType.Validation));
            }

            var txData = responseDto.PointOfInteraction.TransactionData;
            var txId = responseDto.Id.ToString();
            var expiresAt = DateTimeOffset.TryParse(responseDto.DateOfExpiration, out var exp)
                ? exp
                : DateTimeOffset.UtcNow.Add(request.Expiration);

            return Result<PixGatewayChargeResponse>.Success(new PixGatewayChargeResponse(
                txId,
                txData.QrCodeBase64 ?? string.Empty,
                txData.QrCode ?? string.Empty,
                expiresAt));
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unexpected error calling Mercado Pago Pix API");
            return Result<PixGatewayChargeResponse>.Failure(new Error("mercadopago.exception", ex.Message, ErrorType.Validation));
        }
    }

    private sealed record MercadoPagoPaymentRequest(
        [property: JsonPropertyName("transaction_amount")] decimal TransactionAmount,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("payment_method_id")] string PaymentMethodId,
        [property: JsonPropertyName("payer")] MercadoPagoPayer Payer,
        [property: JsonPropertyName("date_of_expiration")] string DateOfExpiration);

    private sealed record MercadoPagoPayer(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("first_name")] string FirstName);

    private sealed record MercadoPagoPaymentResponse(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("date_of_expiration")] string? DateOfExpiration,
        [property: JsonPropertyName("point_of_interaction")] MercadoPagoPointOfInteraction? PointOfInteraction);

    private sealed record MercadoPagoPointOfInteraction(
        [property: JsonPropertyName("transaction_data")] MercadoPagoTransactionData? TransactionData);

    private sealed record MercadoPagoTransactionData(
        [property: JsonPropertyName("qr_code")] string? QrCode,
        [property: JsonPropertyName("qr_code_base64")] string? QrCodeBase64);
}
