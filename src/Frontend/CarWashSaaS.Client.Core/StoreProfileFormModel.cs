using System.Text.RegularExpressions;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class StoreProfileFormModel
{
    public string LegalName { get; set; } = string.Empty;
    public string TradeName { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? BrandPrimaryColor { get; set; }
    public string? BrandSecondaryColor { get; set; }

    public static StoreProfileFormModel FromDto(StoreProfileDto dto) => new()
    {
        LegalName = dto.LegalName,
        TradeName = dto.TradeName,
        Cnpj = FormatCnpj(dto.Cnpj),
        Phone = dto.Phone,
        PostalCode = FormatCep(dto.PostalCode),
        Street = dto.Street,
        City = dto.City,
        State = dto.State,
        LogoUrl = dto.LogoUrl,
        BrandPrimaryColor = dto.BrandPrimaryColor,
        BrandSecondaryColor = dto.BrandSecondaryColor
    };

    public CreateStoreProfileRequest ToCreateRequest() => new(
        LegalName.Trim(),
        TradeName.Trim(),
        DigitsOnly(Cnpj),
        Phone.Trim(),
        Street.Trim(),
        City.Trim(),
        State.Trim().ToUpperInvariant(),
        DigitsOnly(PostalCode),
        LogoUrl,
        BrandPrimaryColor,
        BrandSecondaryColor);

    public UpdateStoreProfileRequest ToUpdateRequest() => new(
        LegalName.Trim(),
        TradeName.Trim(),
        DigitsOnly(Cnpj),
        Phone.Trim(),
        Street.Trim(),
        City.Trim(),
        State.Trim().ToUpperInvariant(),
        DigitsOnly(PostalCode),
        LogoUrl,
        BrandPrimaryColor,
        BrandSecondaryColor);

    public static string DigitsOnly(string value) =>
        new(value.Where(char.IsDigit).ToArray());

    public static string FormatCnpj(string value)
    {
        var digits = DigitsOnly(value);
        if (digits.Length > 14) digits = digits[..14];
        if (digits.Length <= 2) return digits;
        if (digits.Length <= 5) return $"{digits[..2]}.{digits[2..]}";
        if (digits.Length <= 8) return $"{digits[..2]}.{digits[2..5]}.{digits[5..]}";
        if (digits.Length <= 12) return $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..]}";
        return $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..12]}-{digits[12..]}";
    }

    public static string FormatCep(string value)
    {
        var digits = DigitsOnly(value);
        if (digits.Length > 8) digits = digits[..8];
        if (digits.Length <= 5) return digits;
        return $"{digits[..5]}-{digits[5..]}";
    }

    public static bool IsValidCnpj(string value)
    {
        var digits = DigitsOnly(value);
        if (digits.Length != 14 || digits.All(c => c == digits[0]))
        {
            return false;
        }

        var weights1 = new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var sum1 = 0;
        for (var i = 0; i < 12; i++)
        {
            sum1 += (digits[i] - '0') * weights1[i];
        }

        var rest1 = sum1 % 11;
        var digit1 = rest1 < 2 ? 0 : 11 - rest1;
        if (digits[12] - '0' != digit1)
        {
            return false;
        }

        var weights2 = new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var sum2 = 0;
        for (var i = 0; i < 13; i++)
        {
            sum2 += (digits[i] - '0') * weights2[i];
        }

        var rest2 = sum2 % 11;
        var digit2 = rest2 < 2 ? 0 : 11 - rest2;
        return digits[13] - '0' == digit2;
    }

    public (bool IsValid, string? ErrorMessage) ValidateStep1()
    {
        if (string.IsNullOrWhiteSpace(LegalName))
            return (false, "Razão Social é obrigatória.");
        if (LegalName.Trim().Length > 200)
            return (false, "Razão Social deve ter no máximo 200 caracteres.");
        if (string.IsNullOrWhiteSpace(TradeName))
            return (false, "Nome Fantasia é obrigatório.");
        if (TradeName.Trim().Length > 200)
            return (false, "Nome Fantasia deve ter no máximo 200 caracteres.");
        if (string.IsNullOrWhiteSpace(Cnpj) || !IsValidCnpj(Cnpj))
            return (false, "CNPJ informado é inválido.");

        return (true, null);
    }

    public (bool IsValid, string? ErrorMessage) ValidateStep2()
    {
        if (string.IsNullOrWhiteSpace(Phone))
            return (false, "Telefone de contato é obrigatório.");
        if (Phone.Trim().Length > 32)
            return (false, "Telefone deve ter no máximo 32 caracteres.");
        if (string.IsNullOrWhiteSpace(PostalCode))
            return (false, "CEP é obrigatório.");
        if (string.IsNullOrWhiteSpace(Street))
            return (false, "Logradouro/Endereço é obrigatório.");
        if (Street.Trim().Length > 200)
            return (false, "Logradouro deve ter no máximo 200 caracteres.");
        if (string.IsNullOrWhiteSpace(City))
            return (false, "Cidade é obrigatória.");
        if (City.Trim().Length > 150)
            return (false, "Cidade deve ter no máximo 150 caracteres.");
        if (string.IsNullOrWhiteSpace(State) || State.Trim().Length != 2)
            return (false, "UF deve ser uma sigla válida de 2 letras (ex: SP).");

        return (true, null);
    }

    public (bool IsValid, string? ErrorMessage) ValidateStep3Branding()
    {
        if (!string.IsNullOrWhiteSpace(BrandPrimaryColor))
        {
            if (!Regex.IsMatch(BrandPrimaryColor.Trim(), "^#[0-9A-Fa-f]{6}$"))
                return (false, "Cor primária inválida. Use o formato hexadecimal #RRGGBB.");
        }

        if (!string.IsNullOrWhiteSpace(BrandSecondaryColor))
        {
            if (!Regex.IsMatch(BrandSecondaryColor.Trim(), "^#[0-9A-Fa-f]{6}$"))
                return (false, "Cor secundária inválida. Use o formato hexadecimal #RRGGBB.");
        }

        return (true, null);
    }
}
