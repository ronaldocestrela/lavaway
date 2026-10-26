using System.Text.RegularExpressions;

namespace CarWashSaaS.Shared.Contracts;

public static partial class PiiRegexMasker
{
    [GeneratedRegex(@"\b\d{3}\.\d{3}\.\d{3}-\d{2}\b", RegexOptions.Compiled)]
    private static partial Regex FormattedCpfRegex();

    [GeneratedRegex(@"\b\d{11}\b", RegexOptions.Compiled)]
    private static partial Regex UnformattedCpfRegex();

    [GeneratedRegex(@"(?:\+?55\s?)?(?:\(?[1-9]{2}\)?[\s-]?)?(?:9\d{4}|\d{4})[-\s]?\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b(sk_live_|sk_test_|ev_|APP_USR-)[A-Za-z0-9_\-]{8,}\b", RegexOptions.Compiled)]
    private static partial Regex SecretKeyRegex();

    public static string MaskCpf(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.Length != 11)
        {
            return input;
        }

        var lastTwoDigitsOfBase = digits.Substring(6, 3);
        return $"***.***.{lastTwoDigitsOfBase}-**";
    }

    public static string MaskPhone(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var clean = new string(input.Where(char.IsDigit).ToArray());
        var hasCountryCode = clean.StartsWith("55") && clean.Length >= 12;

        if (hasCountryCode)
        {
            clean = clean.Substring(2);
        }

        if (clean.Length == 11) // (DD) 9XXXX-XXXX
        {
            var ddd = clean.Substring(0, 2);
            var lastFour = clean.Substring(7, 4);
            var prefix = hasCountryCode ? "+55 " : "";
            return $"{prefix}({ddd}) *****-{lastFour}";
        }

        if (clean.Length == 10) // (DD) XXXX-XXXX
        {
            var ddd = clean.Substring(0, 2);
            var lastFour = clean.Substring(6, 4);
            var prefix = hasCountryCode ? "+55 " : "";
            return $"{prefix}({ddd}) ****-{lastFour}";
        }

        if (clean.Length >= 4)
        {
            return $"*****{clean.Substring(clean.Length - 4)}";
        }

        return "******";
    }

    public static string MaskEmail(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var atIndex = input.IndexOf('@');
        if (atIndex <= 0 || atIndex >= input.Length - 1)
        {
            return input;
        }

        var username = input.Substring(0, atIndex);
        var domain = input.Substring(atIndex);

        var firstChar = username[0];
        return $"{firstChar}***{domain}";
    }

    public static string MaskSecretKey(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var trimmed = input.Trim();
        if (trimmed.Length <= 8)
        {
            return "••••••••";
        }

        if (trimmed.StartsWith("sk_live_", StringComparison.OrdinalIgnoreCase))
        {
            return $"sk_live_••••••••{trimmed[^4..]}";
        }

        if (trimmed.StartsWith("sk_test_", StringComparison.OrdinalIgnoreCase))
        {
            return $"sk_test_••••••••{trimmed[^4..]}";
        }

        if (trimmed.StartsWith("ev_", StringComparison.OrdinalIgnoreCase))
        {
            return $"ev_••••••••{trimmed[^4..]}";
        }

        if (trimmed.StartsWith("APP_USR-", StringComparison.OrdinalIgnoreCase))
        {
            return $"APP_USR-••••••••{trimmed[^4..]}";
        }

        return $"••••••••{trimmed[^4..]}";
    }

    public static string MaskAllPiiInText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text ?? string.Empty;
        }

        // 1. Secret keys
        text = SecretKeyRegex().Replace(text, m => MaskSecretKey(m.Value));

        // 2. Formatted CPFs (com pontos e traço)
        text = FormattedCpfRegex().Replace(text, m => MaskCpf(m.Value));

        // 3. E-mails
        text = EmailRegex().Replace(text, m => MaskEmail(m.Value));

        // 4. Phones (formatados ou celulares 11 dígitos ou fixos 10 dígitos)
        text = PhoneRegex().Replace(text, m => MaskPhone(m.Value));

        // 5. Unformatted CPFs restantes (11 dígitos que não são celulares)
        text = UnformattedCpfRegex().Replace(text, m => MaskCpf(m.Value));

        return text;
    }
}
