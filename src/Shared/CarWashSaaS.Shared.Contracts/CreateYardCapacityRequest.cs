namespace CarWashSaaS.Shared.Contracts;

public sealed record CreateYardCapacityRequest(
    int TotalBoxes,
    string? Description);
