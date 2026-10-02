namespace CarWashSaaS.Shared.Contracts;

public sealed record CreateServiceRequest(
    string Name,
    string Category,
    IReadOnlyCollection<ServicePriceDto> Prices);
