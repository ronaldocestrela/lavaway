namespace CarWashSaaS.Tenants.Application;

public sealed record UpdateStoreProfileCommand(
    string LegalName,
    string TradeName,
    string Cnpj,
    string Phone,
    string Street,
    string City,
    string State,
    string PostalCode);
