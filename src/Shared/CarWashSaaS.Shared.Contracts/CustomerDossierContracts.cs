namespace CarWashSaaS.Shared.Contracts;

public sealed record CustomerDossierDto(
    Guid TenantId,
    DateTimeOffset GeneratedAtUtc,
    CustomerProfileDossierDto Profile,
    IReadOnlyList<VehicleDossierDto> Vehicles,
    IReadOnlyList<WorkOrderDossierDto> WorkOrders,
    WhatsAppHistoryDossierDto CommunicationHistory,
    DossierLegalNoticeDto LegalNotice);

public sealed record CustomerProfileDossierDto(
    Guid CustomerId,
    string Name,
    string Phone,
    bool IsAnonymized,
    DateTimeOffset? AnonymizedAtUtc);

public sealed record VehicleDossierDto(
    Guid VehicleId,
    string Plate,
    string SizeCategory);

public sealed record WorkOrderDossierDto(
    Guid WorkOrderId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PickedUpAtUtc,
    string Status,
    decimal TotalAmount,
    bool IsPaid,
    string? PaymentMethod,
    IReadOnlyList<WorkOrderItemDossierDto> Services,
    WorkOrderSurveyDossierDto? Survey);

public sealed record WorkOrderItemDossierDto(
    string ServiceName,
    decimal UnitPrice,
    int Quantity,
    decimal TotalAmount);

public sealed record WorkOrderSurveyDossierDto(
    int? Rating,
    string? Feedback,
    DateTimeOffset? RespondedAtUtc);

public sealed record WhatsAppHistoryDossierDto(
    bool IsOptedIn,
    DateTimeOffset? ConsentUpdatedAtUtc,
    string? ConsentReason,
    IReadOnlyList<OutboundMessageDossierDto> OutboundMessages);

public sealed record OutboundMessageDossierDto(
    DateTimeOffset SentAtUtc,
    string MessageType,
    string Status);

public sealed record DossierLegalNoticeDto(
    string Title,
    string LegalBasis,
    string ControllerRole,
    string RetentionPolicyNotice,
    string DpoContactEmail);

public interface ICustomerWhatsAppHistoryLookup
{
    Task<Result<WhatsAppHistoryDossierDto>> GetCustomerWhatsAppHistoryAsync(
        Guid tenantId,
        string phone,
        CancellationToken ct = default);
}
