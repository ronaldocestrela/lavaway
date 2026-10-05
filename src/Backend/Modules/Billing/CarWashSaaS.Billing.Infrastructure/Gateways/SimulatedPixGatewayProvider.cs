using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CarWashSaaS.Billing.Application;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Infrastructure.Gateways;

public sealed class SimulatedPixGatewayProvider : IPixGatewayProvider
{
    private static readonly CultureInfo UsCulture = CultureInfo.InvariantCulture;

    public string ProviderName => "SimulatedPixGateway";

    public Task<Result<PixGatewayChargeResponse>> CreateImmediateChargeAsync(
        PixGatewayChargeRequest request,
        CancellationToken ct = default)
    {
        var txId = $"LAV{request.WorkOrderId.ToString()[..8].ToUpperInvariant()}{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(request.Expiration);

        var copyPasteKey = BuildEmvPixPayload(
            pixKey: "pix@lavaway.com.br",
            merchantName: "LAVAWAY SAAS",
            merchantCity: "SAO PAULO",
            amount: request.Amount,
            txId: txId);

        var qrCodeSvg = GenerateQrCodeSvg(copyPasteKey);
        var qrCodeBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(qrCodeSvg));

        var response = new PixGatewayChargeResponse(
            txId,
            qrCodeBase64,
            copyPasteKey,
            expiresAtUtc);

        return Task.FromResult(Result<PixGatewayChargeResponse>.Success(response));
    }

    private static string BuildEmvPixPayload(string pixKey, string merchantName, string merchantCity, decimal amount, string txId)
    {
        var sb = new StringBuilder();

        AppendTlv(sb, "00", "01"); // Format indicator
        AppendTlv(sb, "01", "12"); // Dynamic / Point of initiation

        // Merchant account information (GUI + Key)
        var mai = new StringBuilder();
        AppendTlv(mai, "00", "br.gov.bcb.pix");
        AppendTlv(mai, "01", pixKey);
        AppendTlv(sb, "26", mai.ToString());

        AppendTlv(sb, "52", "0000"); // Category code
        AppendTlv(sb, "53", "986");  // BRL currency
        AppendTlv(sb, "54", amount.ToString("0.00", UsCulture));
        AppendTlv(sb, "58", "BR");
        AppendTlv(sb, "59", SanitizeString(merchantName, 25));
        AppendTlv(sb, "60", SanitizeString(merchantCity, 15));

        // Additional data field (txId)
        var addData = new StringBuilder();
        AppendTlv(addData, "05", txId);
        AppendTlv(sb, "62", addData.ToString());

        // CRC16 placeholder
        sb.Append("6304");
        var payloadWithoutCrc = sb.ToString();
        var crc = CalculateCrc16(payloadWithoutCrc);

        return payloadWithoutCrc + crc;
    }

    private static void AppendTlv(StringBuilder sb, string tag, string value)
    {
        sb.Append(tag);
        sb.Append(value.Length.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append(value);
    }

    private static string SanitizeString(string value, int maxLength)
    {
        var clean = new string(value.Normalize(NormalizationForm.FormD)
            .Where(c => char.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return clean.Length > maxLength ? clean[..maxLength] : clean;
    }

    private static string CalculateCrc16(string input)
    {
        var bytes = Encoding.ASCII.GetBytes(input);
        ushort crc = 0xFFFF;
        const ushort polynomial = 0x1021;

        foreach (var b in bytes)
        {
            for (var i = 0; i < 8; i++)
            {
                var bit = ((b >> (7 - i)) & 1) == 1;
                var c15 = ((crc >> 15) & 1) == 1;
                crc <<= 1;
                if (c15 ^ bit)
                {
                    crc ^= polynomial;
                }
            }
        }

        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }

    private static string GenerateQrCodeSvg(string payload)
    {
        // Gera SVG representativo e visualmente nítido com padrão matricial baseado no hash do payload
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        var size = 25; // 25x25 grid
        var cellSize = 10;
        var totalSize = size * cellSize;

        var svg = new StringBuilder();
        svg.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {totalSize} {totalSize}\" width=\"100%\" height=\"100%\">");
        svg.Append($"<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>");

        // Helper para desenhar Finder Patterns nos cantos
        void DrawFinderPattern(int startX, int startY)
        {
            svg.Append($"<rect x=\"{startX * cellSize}\" y=\"{startY * cellSize}\" width=\"{7 * cellSize}\" height=\"{7 * cellSize}\" fill=\"#000000\"/>");
            svg.Append($"<rect x=\"{(startX + 1) * cellSize}\" y=\"{(startY + 1) * cellSize}\" width=\"{5 * cellSize}\" height=\"{5 * cellSize}\" fill=\"#ffffff\"/>");
            svg.Append($"<rect x=\"{(startX + 2) * cellSize}\" y=\"{(startY + 2) * cellSize}\" width=\"{3 * cellSize}\" height=\"{3 * cellSize}\" fill=\"#000000\"/>");
        }

        DrawFinderPattern(0, 0);
        DrawFinderPattern(size - 7, 0);
        DrawFinderPattern(0, size - 7);

        // Preenche o miolo com padrão derivado dos bytes do hash
        for (var r = 0; r < size; r++)
        {
            for (var c = 0; c < size; c++)
            {
                // Pula finder patterns
                if ((r < 8 && c < 8) || (r < 8 && c >= size - 8) || (r >= size - 8 && c < 8))
                {
                    continue;
                }

                var byteIndex = (r * size + c) % hash.Length;
                var bitIndex = (r + c) % 8;
                var isDark = ((hash[byteIndex] >> bitIndex) & 1) == 1;

                if (isDark)
                {
                    svg.Append($"<rect x=\"{c * cellSize}\" y=\"{r * cellSize}\" width=\"{cellSize}\" height=\"{cellSize}\" fill=\"#111827\"/>");
                }
            }
        }

        svg.Append("</svg>");
        return svg.ToString();
    }
}
