namespace CarWashSaaS.Shared.Contracts;

public static class PixChargeStatusConstants
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Expired = "Expired";
    public const string Cancelled = "Cancelled";
}

public sealed record WorkOrderPixChargeDto(
    Guid Id,
    Guid TenantId,
    Guid WorkOrderId,
    decimal Amount,
    string Status,
    string TxId,
    string QrCodeBase64,
    string CopyPasteKey,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? WhatsAppSentAtUtc = null,
    DateTimeOffset? PaidAtUtc = null);

public sealed record GenerateWorkOrderPixChargeRequest(
    int? ExpirationMinutes = null);

public sealed record SendWorkOrderPixWhatsAppRequest(
    string? CustomMessage = null);

public sealed record WorkOrderPaymentSummaryDto(
    Guid WorkOrderId,
    Guid TenantId,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    string Plate,
    string VehicleSize,
    string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAtUtc);

public interface IWorkOrderPaymentLookup
{
    Task<Result<WorkOrderPaymentSummaryDto>> GetPaymentSummaryAsync(
        Guid tenantId,
        Guid workOrderId,
        CancellationToken ct = default);

    Task<Result<WorkOrderPaymentSummaryDto?>> GetActiveWorkOrderForCustomerPhoneAsync(
        Guid tenantId,
        string customerPhone,
        CancellationToken ct = default);
}

public interface IPixBillingLookup
{
    Task<Result<WorkOrderPixChargeDto>> GetOrCreateWorkOrderPixChargeAsync(
        Guid tenantId,
        Guid workOrderId,
        CancellationToken ct = default);

    Task<Result<WorkOrderPixChargeDto>> SendPixChargeToWhatsAppAsync(
        Guid tenantId,
        Guid workOrderId,
        CancellationToken ct = default);
}
