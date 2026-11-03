namespace CarWashSaaS.Shared.Contracts;

public sealed record CreateWorkOrderRequest(
    Guid CustomerId,
    Guid VehicleId,
    IReadOnlyCollection<CreateWorkOrderItemRequest> Items,
    string? Notes = null,
    Guid? BookingId = null);

public sealed record CreateWorkOrderItemRequest(
    Guid ServiceId,
    int Quantity = 1);
