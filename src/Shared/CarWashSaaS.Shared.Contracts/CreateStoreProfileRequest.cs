namespace CarWashSaaS.Shared.Contracts;

/// <summary>Request payload to create an initial store profile for a tenant.</summary>
public sealed record CreateStoreProfileRequest(
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
