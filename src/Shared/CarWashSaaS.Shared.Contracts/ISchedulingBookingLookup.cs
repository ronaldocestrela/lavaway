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
}
