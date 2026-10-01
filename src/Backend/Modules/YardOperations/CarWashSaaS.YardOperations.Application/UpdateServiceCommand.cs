namespace CarWashSaaS.YardOperations.Application;

public sealed record UpdateServiceCommand(
    string Name,
    string Category,
    IReadOnlyCollection<ServicePriceInput> Prices);
