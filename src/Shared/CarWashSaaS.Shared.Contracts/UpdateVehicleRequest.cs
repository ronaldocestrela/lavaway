namespace CarWashSaaS.Shared.Contracts;

/// <summary>Request payload to update vehicle details (plate and size).</summary>
public sealed record UpdateVehicleRequest(string Plate, string Size);
