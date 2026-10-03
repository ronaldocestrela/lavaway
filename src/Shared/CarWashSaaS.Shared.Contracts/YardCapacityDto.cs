namespace CarWashSaaS.Shared.Contracts;

public sealed record YardCapacityDto(
    Guid Id,
    int TotalBoxes,
    string Description);
