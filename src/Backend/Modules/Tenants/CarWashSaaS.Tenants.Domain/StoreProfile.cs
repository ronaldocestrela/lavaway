using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Tenants.Domain;

public sealed class StoreProfile : IMustHaveTenant
{
    private StoreProfile()
    {
    }

    private StoreProfile(
        Guid id,
        Guid tenantId,
        string legalName,
        string tradeName,
        string cnpj,
        string phone,
        string street,
        string city,
        string state,
        string postalCode,
        string? logoUrl,
        string? brandPrimaryColor,
        string? brandSecondaryColor)
    {
        Id = id;
        TenantId = tenantId;
        LegalName = legalName;
        TradeName = tradeName;
        Cnpj = cnpj;
        Phone = phone;
        Street = street;
        City = city;
        State = state;
        PostalCode = postalCode;
        LogoUrl = logoUrl;
        BrandPrimaryColor = brandPrimaryColor;
        BrandSecondaryColor = brandSecondaryColor;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public string LegalName { get; private set; } = string.Empty;
    public string TradeName { get; private set; } = string.Empty;
    public string Cnpj { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Street { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public string? LogoUrl { get; private set; }
    public string? BrandPrimaryColor { get; private set; }
    public string? BrandSecondaryColor { get; private set; }

    public static Result<StoreProfile> Create(
        Guid tenantId,
        string legalName,
        string tradeName,
        string cnpj,
        string phone,
        string street,
        string city,
        string state,
        string postalCode,
        string? logoUrl = null,
        string? brandPrimaryColor = null,
        string? brandSecondaryColor = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<StoreProfile>.Failure(new Error("store_profile.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var normalizedValues = ValidateAndNormalize(legalName, tradeName, cnpj, phone, street, city, state, postalCode, logoUrl, brandPrimaryColor, brandSecondaryColor);
        if (!normalizedValues.IsSuccess)
        {
            return Result<StoreProfile>.Failure(normalizedValues.Error!);
        }

        return Result<StoreProfile>.Success(new StoreProfile(
            Guid.CreateVersion7(),
            tenantId,
            normalizedValues.Value!.LegalName,
            normalizedValues.Value.TradeName,
            normalizedValues.Value.Cnpj,
            normalizedValues.Value.Phone,
            normalizedValues.Value.Street,
            normalizedValues.Value.City,
            normalizedValues.Value.State,
            normalizedValues.Value.PostalCode,
            normalizedValues.Value.LogoUrl,
            normalizedValues.Value.BrandPrimaryColor,
            normalizedValues.Value.BrandSecondaryColor));
    }

    public Result<StoreProfile> Update(
        string legalName,
        string tradeName,
        string cnpj,
        string phone,
        string street,
        string city,
        string state,
        string postalCode,
        string? logoUrl = null,
        string? brandPrimaryColor = null,
        string? brandSecondaryColor = null)
    {
        var normalizedValues = ValidateAndNormalize(legalName, tradeName, cnpj, phone, street, city, state, postalCode, logoUrl, brandPrimaryColor, brandSecondaryColor);
        if (!normalizedValues.IsSuccess)
        {
            return Result<StoreProfile>.Failure(normalizedValues.Error!);
        }

        LegalName = normalizedValues.Value!.LegalName;
        TradeName = normalizedValues.Value.TradeName;
        Cnpj = normalizedValues.Value.Cnpj;
        Phone = normalizedValues.Value.Phone;
        Street = normalizedValues.Value.Street;
        City = normalizedValues.Value.City;
        State = normalizedValues.Value.State;
        PostalCode = normalizedValues.Value.PostalCode;
        LogoUrl = normalizedValues.Value.LogoUrl;
        BrandPrimaryColor = normalizedValues.Value.BrandPrimaryColor;
        BrandSecondaryColor = normalizedValues.Value.BrandSecondaryColor;

        return Result<StoreProfile>.Success(this);
    }

    private static Result<(string LegalName, string TradeName, string Cnpj, string Phone, string Street, string City, string State, string PostalCode, string? LogoUrl, string? BrandPrimaryColor, string? BrandSecondaryColor)> ValidateAndNormalize(
        string legalName,
        string tradeName,
        string cnpj,
        string phone,
        string street,
        string city,
        string state,
        string postalCode,
        string? logoUrl = null,
        string? brandPrimaryColor = null,
        string? brandSecondaryColor = null)
    {
        if (string.IsNullOrWhiteSpace(legalName) || legalName.Trim().Length > 200)
        {
            return Result<(string LegalName, string TradeName, string Cnpj, string Phone, string Street, string City, string State, string PostalCode, string? LogoUrl, string? BrandPrimaryColor, string? BrandSecondaryColor)>.Failure(new Error("store_profile.legal_name.invalid", "A legal name up to 200 characters is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(tradeName) || tradeName.Trim().Length > 200)
        {
            return Result<(string LegalName, string TradeName, string Cnpj, string Phone, string Street, string City, string State, string PostalCode, string? LogoUrl, string? BrandPrimaryColor, string? BrandSecondaryColor)>.Failure(new Error("store_profile.trade_name.invalid", "A trade name up to 200 characters is required.", ErrorType.Validation));
        }

        var normalizedCnpj = NormalizeDigits(cnpj);
        if (string.IsNullOrWhiteSpace(cnpj) || !IsValidCnpj(normalizedCnpj))
        {
            return Result<(string LegalName, string TradeName, string Cnpj, string Phone, string Street, string City, string State, string PostalCode, string? LogoUrl, string? BrandPrimaryColor, string? BrandSecondaryColor)>.Failure(new Error("store_profile.cnpj.invalid", "A valid CNPJ is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(phone) || phone.Trim().Length > 32)
        {
            return Result<(string LegalName, string TradeName, string Cnpj, string Phone, string Street, string City, string State, string PostalCode, string? LogoUrl, string? BrandPrimaryColor, string? BrandSecondaryColor)>.Failure(new Error("store_profile.phone.invalid", "A phone number up to 32 characters is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(street) || street.Trim().Length > 200)
        {
            return Result<(string LegalName, string TradeName, string Cnpj, string Phone, string Street, string City, string State, string PostalCode, string? LogoUrl, string? BrandPrimaryColor, string? BrandSecondaryColor)>.Failure(new Error("store_profile.street.invalid", "A street address up to 200 characters is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(city) || city.Trim().Length > 150)
        {
            return Result<(string LegalName, string TradeName, string Cnpj, string Phone, string Street, string City, string State, string PostalCode, string? LogoUrl, string? BrandPrimaryColor, string? BrandSecondaryColor)>.Failure(new Error("store_profile.city.invalid", "A city up to 150 characters is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(state) || state.Trim().Length > 2)
        {
            return Result<(string LegalName, string TradeName, string Cnpj, string Phone, string Street, string City, string State, string PostalCode, string? LogoUrl, string? BrandPrimaryColor, string? BrandSecondaryColor)>.Failure(new Error("store_profile.state.invalid", "A state code with up to 2 characters is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(postalCode) || postalCode.Trim().Length > 20)
        {
            return Result<(string LegalName, string TradeName, string Cnpj, string Phone, string Street, string City, string State, string PostalCode, string? LogoUrl, string? BrandPrimaryColor, string? BrandSecondaryColor)>.Failure(new Error("store_profile.postal_code.invalid", "A postal code up to 20 characters is required.", ErrorType.Validation));
        }

        var normalizedLogoUrl = NormalizeLogoUrl(logoUrl);
        if (!normalizedLogoUrl.IsSuccess)
        {
            return Result<(string LegalName, string TradeName, string Cnpj, string Phone, string Street, string City, string State, string PostalCode, string? LogoUrl, string? BrandPrimaryColor, string? BrandSecondaryColor)>.Failure(normalizedLogoUrl.Error!);
        }

        var normalizedPrimaryColor = NormalizeHexColor(brandPrimaryColor, "brand_primary_color");
        if (!normalizedPrimaryColor.IsSuccess)
        {
            return Result<(string LegalName, string TradeName, string Cnpj, string Phone, string Street, string City, string State, string PostalCode, string? LogoUrl, string? BrandPrimaryColor, string? BrandSecondaryColor)>.Failure(normalizedPrimaryColor.Error!);
        }

        var normalizedSecondaryColor = NormalizeHexColor(brandSecondaryColor, "brand_secondary_color");
        if (!normalizedSecondaryColor.IsSuccess)
        {
            return Result<(string LegalName, string TradeName, string Cnpj, string Phone, string Street, string City, string State, string PostalCode, string? LogoUrl, string? BrandPrimaryColor, string? BrandSecondaryColor)>.Failure(normalizedSecondaryColor.Error!);
        }

        return Result<(string LegalName, string TradeName, string Cnpj, string Phone, string Street, string City, string State, string PostalCode, string? LogoUrl, string? BrandPrimaryColor, string? BrandSecondaryColor)>.Success((
            legalName.Trim(),
            tradeName.Trim(),
            normalizedCnpj,
            phone.Trim(),
            street.Trim(),
            city.Trim(),
            state.Trim().ToUpperInvariant(),
            postalCode.Trim(),
            normalizedLogoUrl.Value,
            normalizedPrimaryColor.Value,
            normalizedSecondaryColor.Value));
    }

    private static Result<string?> NormalizeLogoUrl(string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
        {
            return Result<string?>.Success(null);
        }

        var trimmed = logoUrl.Trim();
        if (trimmed.StartsWith("/", StringComparison.Ordinal) || trimmed.StartsWith("storage/", StringComparison.OrdinalIgnoreCase))
        {
            return Result<string?>.Success(trimmed.StartsWith("/", StringComparison.Ordinal) ? trimmed : $"/{trimmed}");
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return Result<string?>.Success(trimmed);
        }

        return Result<string?>.Failure(new Error("store_profile.logo_url.invalid", "A valid HTTP/HTTPS URL or internal storage path is required.", ErrorType.Validation));
    }

    private static Result<string?> NormalizeHexColor(string? value, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<string?>.Success(null);
        }

        var trimmed = value.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, "^#[0-9A-Fa-f]{6}$"))
        {
            return Result<string?>.Failure(new Error(errorCode, "A valid hex color is required, e.g. #FF6600.", ErrorType.Validation));
        }

        return Result<string?>.Success(trimmed.ToUpperInvariant());
    }

    private static string NormalizeDigits(string value)
    {
        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static bool IsValidCnpj(string cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj) || cnpj.Length != 14)
        {
            return false;
        }

        if (cnpj.All(digit => digit == cnpj[0]))
        {
            return false;
        }

        var sum = 0;
        var weights1 = new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        for (var i = 0; i < 12; i++)
        {
            sum += (cnpj[i] - '0') * weights1[i];
        }

        var firstDigit = sum % 11;
        firstDigit = firstDigit < 2 ? 0 : 11 - firstDigit;

        if (firstDigit != cnpj[12] - '0')
        {
            return false;
        }

        var sum2 = 0;
        var weights2 = new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        for (var i = 0; i < 13; i++)
        {
            sum2 += (cnpj[i] - '0') * weights2[i];
        }

        var secondDigit = sum2 % 11;
        secondDigit = secondDigit < 2 ? 0 : 11 - secondDigit;

        return secondDigit == cnpj[13] - '0';
    }
}
