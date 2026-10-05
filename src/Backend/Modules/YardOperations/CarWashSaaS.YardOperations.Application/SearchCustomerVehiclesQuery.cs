namespace CarWashSaaS.YardOperations.Application;

public sealed record SearchCustomerVehiclesQuery(string? Plate = null, string? Phone = null, int Limit = 20, string? Query = null);
