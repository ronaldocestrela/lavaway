using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Application;

public sealed record PixGatewayChargeRequest(
    Guid TenantId,
    Guid WorkOrderId,
    decimal Amount,
    string Description,
    string CustomerName,
    string CustomerPhone,
    TimeSpan Expiration);

public sealed record PixGatewayChargeResponse(
    string TxId,
    string QrCodeBase64,
    string CopyPasteKey,
    DateTimeOffset ExpiresAtUtc);

public interface IPixGatewayProvider
{
    string ProviderName { get; }

    Task<Result<PixGatewayChargeResponse>> CreateImmediateChargeAsync(
        PixGatewayChargeRequest request,
        CancellationToken ct = default);
}
