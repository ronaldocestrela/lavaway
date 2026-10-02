namespace CarWashSaaS.Shared.Contracts;

/// <summary>Vehicle data submitted for an existing customer.</summary>
public sealed record AddVehicleToCustomerRequest(string Plate, string Size);
