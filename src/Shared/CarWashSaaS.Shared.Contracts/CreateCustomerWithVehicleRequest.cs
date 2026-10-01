namespace CarWashSaaS.Shared.Contracts;

/// <summary>Customer and first vehicle data submitted by reception.</summary>
public sealed record CreateCustomerWithVehicleRequest(string Name, string Phone, string Plate, string Size);