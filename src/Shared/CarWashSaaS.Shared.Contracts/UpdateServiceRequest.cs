namespace CarWashSaaS.Shared.Contracts;

public sealed record UpdateServiceRequest(
    string Name,
    string Category,
    IReadOnlyCollection<ServicePriceDto> Prices);
