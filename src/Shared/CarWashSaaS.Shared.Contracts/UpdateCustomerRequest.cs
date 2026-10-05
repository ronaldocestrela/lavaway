namespace CarWashSaaS.Shared.Contracts;

/// <summary>Request payload to update customer details (name and phone).</summary>
public sealed record UpdateCustomerRequest(string Name, string Phone);
