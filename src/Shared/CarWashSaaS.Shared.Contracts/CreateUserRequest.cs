namespace CarWashSaaS.Shared.Contracts;

public sealed record CreateUserRequest(string Email, string Password, string Role);
