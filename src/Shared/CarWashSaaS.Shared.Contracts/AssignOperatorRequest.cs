namespace CarWashSaaS.Shared.Contracts;

public sealed record AssignOperatorRequest(Guid? OperatorId, Guid? SecondaryOperatorId = null);

