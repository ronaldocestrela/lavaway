namespace CarWashSaaS.Shared.Contracts;

/// <summary>Store profile details returned by tenant management endpoints.</summary>
public sealed record StoreProfileDto(
    Guid Id,
    Guid TenantId,
    string LegalName,
    string TradeName,
    string Cnpj,
    string Phone,
    string Street,
    string City,
    string State,
    string PostalCode,
    string? LogoUrl,
    string? BrandPrimaryColor,
    string? BrandSecondaryColor);
