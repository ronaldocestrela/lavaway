namespace CarWashSaaS.Shared.Contracts;

public sealed record ServiceDto(
    Guid Id,
    string Name,
    string Category,
    IReadOnlyCollection<ServicePriceDto> Prices);
