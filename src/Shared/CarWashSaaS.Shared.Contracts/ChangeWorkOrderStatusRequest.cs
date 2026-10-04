namespace CarWashSaaS.Shared.Contracts;

public sealed record ChangeWorkOrderStatusRequest(
    string TargetStatus,
    Guid? OperatorId = null,
    string? Notes = null);
