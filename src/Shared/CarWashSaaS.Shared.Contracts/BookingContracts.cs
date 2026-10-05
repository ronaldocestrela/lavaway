namespace CarWashSaaS.Shared.Contracts;

public static class BookingStatusConstants
{
    public const string Scheduled = "Scheduled";
    public const string Confirmed = "Confirmed";
    public const string Arrived = "Arrived";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string NoShow = "NoShow";

    public static readonly IReadOnlyList<string> All =
    [
        Scheduled,
        Confirmed,
        Arrived,
        Completed,
        Cancelled,
        NoShow
    ];

    public static string GetDisplayName(string? status) => status?.Trim() switch
    {
        Scheduled => "Agendado",
        Confirmed => "Confirmado",
        Arrived => "Em Atendimento",
        Completed => "Concluído",
        Cancelled => "Cancelado",
        NoShow => "Não Compareceu",
        _ => status ?? string.Empty
    };
}

public static class BookingOriginConstants
{
    public const string WhatsAppBot = "WhatsAppBot";
    public const string WebReception = "WebReception";
    public const string ManualPhone = "ManualPhone";
}

public sealed record AvailableTimeSlotDto(
    TimeOnly StartTime,
    TimeOnly EndTime,
    int AvailableBoxes,
    int TotalBoxes);

public sealed record BookingSummaryDto(
    Guid Id,
    Guid TenantId,
    string Protocol,
    Guid? CustomerId,
    string CustomerName,
    string CustomerPhone,
    string VehiclePlate,
    string? VehicleModel,
    string VehicleSize,
    Guid ServiceId,
    string ServiceName,
    decimal EstimatedPrice,
    DateOnly ScheduledDate,
    TimeOnly ScheduledTime,
    int EstimatedDurationMinutes,
    string Status,
    string Origin,
    Guid? WorkOrderId,
    string? Notes,
    DateTimeOffset CreatedAt);

public sealed record CreateBookingFromChatbotRequest(
    string CustomerPhone,
    string? CustomerName,
    string VehiclePlate,
    string? VehicleModel,
    string VehicleSize,
    Guid ServiceId,
    DateOnly ScheduledDate,
    TimeOnly ScheduledTime);

public sealed record BookingConfirmationDto(
    Guid BookingId,
    string Protocol,
    string CustomerName,
    string ServiceName,
    DateOnly ScheduledDate,
    TimeOnly ScheduledTime,
    decimal EstimatedPrice);

public sealed record CreateManualBookingRequest(
    string CustomerName,
    string CustomerPhone,
    string VehiclePlate,
    string? VehicleModel,
    string VehicleSize,
    Guid ServiceId,
    DateOnly ScheduledDate,
    TimeOnly ScheduledTime,
    string? Notes);

public sealed record InboundWhatsAppReceivedEvent(
    Guid TenantId,
    string SenderPhone,
    string? PushName,
    string MessageText,
    string MessageId,
    DateTimeOffset ReceivedAtUtc);
