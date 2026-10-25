namespace CarWashSaaS.YardOperations.Application;

public sealed record AnonymizeCustomerCommand(
    Guid CustomerId,
    Guid OperatorId,
    string OperatorEmail,
    string Reason,
    string? IpAddress = null,
    string? UserAgent = null);
