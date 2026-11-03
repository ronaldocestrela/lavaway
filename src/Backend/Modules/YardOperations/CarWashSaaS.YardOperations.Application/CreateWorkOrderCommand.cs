namespace CarWashSaaS.YardOperations.Application;

public sealed record CreateWorkOrderCommand(
    Guid CustomerId,
    Guid VehicleId,
    IReadOnlyCollection<CreateWorkOrderItemInput> Items,
    string? Notes = null,
    Guid? BookingId = null);

public sealed record CreateWorkOrderItemInput(
    Guid ServiceId,
    int Quantity = 1);
