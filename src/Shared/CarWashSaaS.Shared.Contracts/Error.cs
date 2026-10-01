namespace CarWashSaaS.Shared.Contracts;

public sealed record Error(string Code, string Description, ErrorType Type);