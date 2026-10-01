using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed record ServicePriceInput(VehicleSize VehicleSize, decimal Amount, int EstimatedDurationMinutes);
