using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Booking?> GetByProtocolAsync(string protocol, CancellationToken ct = default);
    Task<IReadOnlyList<Booking>> GetByDateAsync(Guid tenantId, DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<Booking>> GetActiveBookingsByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default);
    Task<int> CountActiveBookingsInSlotAsync(Guid tenantId, DateOnly date, TimeOnly startTime, TimeOnly endTime, CancellationToken ct = default);
    Task AddAsync(Booking booking, CancellationToken ct = default);
    void Update(Booking booking);
    Task<IReadOnlyList<Booking>> GetBookingsPending24hReminderAsync(Guid tenantId, DateTimeOffset referenceTime, CancellationToken ct = default);
    Task<IReadOnlyList<Booking>> GetBookingsPending2hReminderAsync(Guid tenantId, DateTimeOffset referenceTime, CancellationToken ct = default);
    Task<Booking?> GetUpcomingActiveBookingByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default);
}
