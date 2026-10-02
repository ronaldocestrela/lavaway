namespace CarWashSaaS.YardOperations.Application;

public sealed record SearchCustomerVehiclesQuery(string? Plate, string? Phone, int Limit = 20);
