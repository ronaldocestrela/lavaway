namespace CarWashSaaS.Shared.Contracts;

public sealed record UpdateYardCapacityRequest(
    int TotalBoxes,
    string? Description);
