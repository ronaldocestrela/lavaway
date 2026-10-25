namespace CarWashSaaS.Shared.Contracts;

public sealed record AnonymizeCustomerRequest(string Reason);

public sealed record CustomerAnonymizedDto(
    Guid CustomerId,
    string AnonymizedName,
    DateTimeOffset AnonymizedAtUtc,
    int VehiclesDisassociatedCount);
