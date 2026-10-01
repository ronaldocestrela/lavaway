namespace CarWashSaaS.Tenants.Application;

public sealed record CreateStoreProfileCommand(
    string LegalName,
    string TradeName,
    string Cnpj,
    string Phone,
    string Street,
    string City,
    string State,
    string PostalCode,
    string? LogoUrl = null,
    string? BrandPrimaryColor = null,
    string? BrandSecondaryColor = null);
