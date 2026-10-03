namespace CarWashSaaS.YardOperations.Application;

public sealed record UpdateYardCapacityCommand(int TotalBoxes, string? Description);
