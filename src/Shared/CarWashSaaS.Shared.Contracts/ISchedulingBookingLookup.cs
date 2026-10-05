namespace CarWashSaaS.Shared.Contracts;

public interface ISchedulingBookingLookup
{
    Task<Result<IReadOnlyList<ServiceDto>>> GetAvailableServicesCatalogAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<AvailableTimeSlotDto>>> GetAvailableTimeSlotsAsync(
        Guid tenantId,
        DateOnly date,
        Guid serviceId,
        string vehicleSize,
        CancellationToken ct = default);

    Task<Result<BookingConfirmationDto>> CreateBookingFromChatbotAsync(
        Guid tenantId,
        CreateBookingFromChatbotRequest request,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<BookingSummaryDto>>> GetCustomerActiveBookingsAsync(
        Guid tenantId,
        string customerPhone,
        CancellationToken ct = default);

    Task<Result<BookingSummaryDto>> ConfirmBookingAsync(
        Guid tenantId,
        Guid bookingId,
        CancellationToken ct = default);

    Task<Result<BookingSummaryDto>> CancelBookingAsync(
        Guid tenantId,
        Guid bookingId,
        string? reason,
        CancellationToken ct = default);

    Task<Result<BookingSummaryDto>> RescheduleBookingAsync(
        Guid tenantId,
        Guid bookingId,
        DateOnly newDate,
        TimeOnly newTime,
        CancellationToken ct = default);

    Task<Result<BookingSummaryDto?>> GetUpcomingBookingForCustomerAsync(
        Guid tenantId,
        string customerPhone,
        CancellationToken ct = default);
}
