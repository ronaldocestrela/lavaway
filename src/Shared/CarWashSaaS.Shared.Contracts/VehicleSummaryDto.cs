namespace CarWashSaaS.Shared.Contracts;

/// <summary>Vehicle details returned with a customer match.</summary>
public sealed record VehicleSummaryDto(Guid Id, string Plate, string Size);
