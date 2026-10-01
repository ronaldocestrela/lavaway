namespace CarWashSaaS.YardOperations.Application;

public sealed record CreateServiceCommand(
    string Name,
    string Category,
    IReadOnlyCollection<ServicePriceInput> Prices);
