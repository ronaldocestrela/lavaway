namespace CarWashSaaS.Shared.Contracts;

public sealed record ServicePriceDto(string VehicleSize, decimal Amount, int EstimatedDurationMinutes);
