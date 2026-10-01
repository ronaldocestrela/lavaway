namespace CarWashSaaS.YardOperations.Application;

public sealed record CreateYardCapacityCommand(int TotalBoxes, string? Description = null);
