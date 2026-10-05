using System.Globalization;
using System.Text;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class WorkOrderReceiptPdfGenerator : IWorkOrderReceiptPdfGenerator
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public Result<byte[]> GenerateReceiptPdf(WorkOrderReceiptPdfModel model)
    {
        if (model.TenantId == Guid.Empty || model.WorkOrderId == Guid.Empty)
        {
            return Result<byte[]>.Failure(new Error("receipt_pdf.invalid_data", "Tenant e ordem de serviço são obrigatórios.", ErrorType.Validation));
        }

        try
        {
            var pdfBytes = BuildPdf(model);
            return Result<byte[]>.Success(pdfBytes);
        }
        catch (Exception ex)
        {
            return Result<byte[]>.Failure(new Error("receipt_pdf.generation_error", $"Falha ao gerar comprovante em PDF: {ex.Message}", ErrorType.Validation));
        }
    }

    private static byte[] BuildPdf(WorkOrderReceiptPdfModel model)
    {
        var sb = new StringBuilder();

        // 1. Top Header Banner
        // Banner retangular azul no topo (0.15 0.38 0.92)
        sb.AppendLine("0.15 0.39 0.92 rg");
        sb.AppendLine("0 770 595 72 re f");

        // Título do cabeçalho em branco
        sb.AppendLine("1 1 1 rg");
        sb.AppendLine("BT /F2 16 Tf 36 815 Td (" + EscapePdfText(model.StoreTradeName.ToUpperInvariant()) + ") Tj ET");
        sb.AppendLine("BT /F1 9 Tf 36 798 Td (" + EscapePdfText(BuildStoreContact(model)) + ") Tj ET");
        sb.AppendLine("BT /F2 12 Tf 380 812 Td (COMPROVANTE DE ENTRADA) Tj ET");
        sb.AppendLine("BT /F1 9 Tf 380 798 Td (VISTORIA & CHECK-IN) Tj ET");

        // 2. Info Box (OS, Cliente, Veículo)
        sb.AppendLine("0.95 0.96 0.98 rg");
        sb.AppendLine("36 675 523 80 re f");
        sb.AppendLine("0.80 0.83 0.88 RG 1 w");
        sb.AppendLine("36 675 523 80 re S");

        sb.AppendLine("0.1 0.1 0.1 rg");
        var shortOs = model.WorkOrderId.ToString("D")[..8].ToUpperInvariant();
        sb.AppendLine("BT /F2 10 Tf 46 738 Td (ORDEM DE SERVICO: #" + EscapePdfText(shortOs) + ") Tj ET");
        sb.AppendLine("BT /F1 9 Tf 46 722 Td (Cliente: " + EscapePdfText(model.CustomerName) + "  |  Telefone: " + EscapePdfText(model.CustomerPhone) + ") Tj ET");
        sb.AppendLine("BT /F1 9 Tf 46 706 Td (Veiculo: " + EscapePdfText(model.VehicleModel) + "  |  Placa: " + EscapePdfText(model.VehiclePlate) + " (" + EscapePdfText(model.VehicleSize) + ")) Tj ET");

        var checkinStr = model.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", PtBr);
        var forecastStr = model.EstimatedCompletionAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", PtBr);
        sb.AppendLine("BT /F1 9 Tf 46 688 Td (Entrada: " + EscapePdfText(checkinStr) + "  |  Previsao de Entrega: " + EscapePdfText(forecastStr) + ") Tj ET");

        // 3. Tabela de Serviços
        var currentY = 645;
        sb.AppendLine("0.15 0.39 0.92 rg");
        sb.AppendLine($"BT /F2 11 Tf 36 {currentY} Td (SERVICOS CONTRATADOS) Tj ET");
        currentY -= 16;

        // Cabeçalho da tabela
        sb.AppendLine($"0.90 0.92 0.96 rg 36 {currentY - 4} 523 18 re f");
        sb.AppendLine("0.2 0.2 0.2 rg");
        sb.AppendLine($"BT /F2 9 Tf 42 {currentY} Td (Descricao do Servico) Tj ET");
        sb.AppendLine($"BT /F2 9 Tf 360 {currentY} Td (Qtd) Tj ET");
        sb.AppendLine($"BT /F2 9 Tf 420 {currentY} Td (Valor Unit.) Tj ET");
        sb.AppendLine($"BT /F2 9 Tf 490 {currentY} Td (Total) Tj ET");
        currentY -= 16;

        // Itens
        sb.AppendLine("0.1 0.1 0.1 rg");
        foreach (var item in model.Items)
        {
            sb.AppendLine($"BT /F1 9 Tf 42 {currentY} Td (" + EscapePdfText(item.ServiceName) + ") Tj ET");
            sb.AppendLine($"BT /F1 9 Tf 368 {currentY} Td (" + item.Quantity + ") Tj ET");
            sb.AppendLine($"BT /F1 9 Tf 420 {currentY} Td (" + EscapePdfText(item.UnitPrice.ToString("C2", PtBr)) + ") Tj ET");
            sb.AppendLine($"BT /F2 9 Tf 490 {currentY} Td (" + EscapePdfText(item.TotalAmount.ToString("C2", PtBr)) + ") Tj ET");
            currentY -= 14;
        }

        // Linha e Total
        sb.AppendLine($"0.80 0.83 0.88 RG 1 w 36 {currentY + 6} m 559 {currentY + 6} l S");
        sb.AppendLine("0.05 0.5 0.2 rg");
        sb.AppendLine($"BT /F2 11 Tf 360 {currentY - 8} Td (TOTAL A PAGAR: " + EscapePdfText(model.TotalAmount.ToString("C2", PtBr)) + ") Tj ET");
        currentY -= 30;

        // 4. Seção da Vistoria (se houver)
        if (model.Inspection is not null)
        {
            sb.AppendLine("0.15 0.39 0.92 rg");
            sb.AppendLine($"BT /F2 11 Tf 36 {currentY} Td (CHECKLIST DA VISTORIA DE ENTRADA) Tj ET");
            currentY -= 14;

            var odo = model.Inspection.OdometerKm.HasValue ? $"{model.Inspection.OdometerKm.Value.ToString("N0", PtBr)} km" : "Nao informado";
            var fuel = !string.IsNullOrWhiteSpace(model.Inspection.FuelLevel) ? model.Inspection.FuelLevel : "Nao informado";
            sb.AppendLine("0.2 0.2 0.2 rg");
            sb.AppendLine($"BT /F1 9 Tf 36 {currentY} Td (Odometro: " + EscapePdfText(odo) + "   |   Nivel de Combustivel: " + EscapePdfText(fuel) + ") Tj ET");
            currentY -= 16;

            // Itens do checklist
            if (model.Inspection.ChecklistItems.Count > 0)
            {
                sb.AppendLine($"0.90 0.92 0.96 rg 36 {currentY - 4} 523 16 re f");
                sb.AppendLine("0.2 0.2 0.2 rg");
                sb.AppendLine($"BT /F2 8 Tf 42 {currentY} Td (Item Inspecionado) Tj ET");
                sb.AppendLine($"BT /F2 8 Tf 220 {currentY} Td (Status) Tj ET");
                sb.AppendLine($"BT /F2 8 Tf 320 {currentY} Td (Observacao) Tj ET");
                currentY -= 14;

                sb.AppendLine("0.1 0.1 0.1 rg");
                foreach (var chk in model.Inspection.ChecklistItems.Take(12)) // cabe na página A4
                {
                    sb.AppendLine($"BT /F1 8 Tf 42 {currentY} Td (" + EscapePdfText(chk.Title) + ") Tj ET");
                    sb.AppendLine($"BT /F2 8 Tf 220 {currentY} Td (" + EscapePdfText(chk.Status) + ") Tj ET");
                    if (!string.IsNullOrWhiteSpace(chk.Observation))
                    {
                        sb.AppendLine($"BT /F1 8 Tf 320 {currentY} Td (" + EscapePdfText(chk.Observation) + ") Tj ET");
                    }
                    currentY -= 12;
                }
            }

            // Avarias
            if (model.Inspection.Damages.Count > 0 && currentY > 120)
            {
                currentY -= 6;
                sb.AppendLine("0.85 0.2 0.1 rg");
                sb.AppendLine($"BT /F2 9 Tf 36 {currentY} Td (Avarias / Danos Registrados na Entrada:) Tj ET");
                currentY -= 12;
                sb.AppendLine("0.2 0.2 0.2 rg");
                foreach (var dmg in model.Inspection.Damages.Take(6))
                {
                    var dmgLine = $"- {dmg.Type} ({dmg.View}, {dmg.Severity}): {dmg.Description ?? "Sem observacoes adicionais"}";
                    sb.AppendLine($"BT /F1 8 Tf 42 {currentY} Td (" + EscapePdfText(dmgLine) + ") Tj ET");
                    currentY -= 11;
                }
            }
        }

        // 5. Notas & Disclaimer Legal
        currentY = Math.Min(currentY, 110);
        sb.AppendLine("0.96 0.96 0.96 rg");
        sb.AppendLine($"36 {currentY - 45} 523 45 re f");
        sb.AppendLine("0.4 0.4 0.4 rg");
        sb.AppendLine($"BT /F1 7 Tf 42 {currentY - 12} Td (Termo de Ciencia: O veiculo sera entregue mediante apresentacao deste comprovante ou documento com foto.) Tj ET");
        sb.AppendLine($"BT /F1 7 Tf 42 {currentY - 24} Td (Nao nos responsabilizamos por objetos de valor deixados no interior do veiculo que nao constem no checklist acima.) Tj ET");
        sb.AppendLine($"BT /F1 7 Tf 42 {currentY - 36} Td (Apos o aviso de conclusao, gentileza retirar o veiculo no prazo acordado.) Tj ET");

        // 6. Rodapé
        sb.AppendLine("0.5 0.5 0.5 rg");
        sb.AppendLine("BT /F1 7 Tf 36 20 Td (Lavaway SaaS - Gestao Inteligente para Estetica Automotiva e Lava-Jato) Tj ET");
        sb.AppendLine("BT /F1 7 Tf 420 20 Td (Documento gerado em " + EscapePdfText(DateTimeOffset.UtcNow.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", PtBr)) + ") Tj ET");

        var contentStream = sb.ToString();
        var contentBytes = Encoding.Latin1.GetBytes(contentStream);

        return AssemblePdfDocument(contentBytes);
    }

    private static string BuildStoreContact(WorkOrderReceiptPdfModel model)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(model.StorePhone)) parts.Add("Tel: " + model.StorePhone);
        if (!string.IsNullOrWhiteSpace(model.StoreAddress)) parts.Add("End: " + model.StoreAddress);
        return parts.Count > 0 ? string.Join("  •  ", parts) : "Atendimento ao Cliente";
    }

    private static string EscapePdfText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        // Normalizar acentuação para texto legível em Latin1 / ASCII padrão de fontes embutidas Helvetica
        var normalized = RemoveDiacritics(text);
        return normalized
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }

    private static string RemoveDiacritics(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(normalizedString.Length);

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static byte[] AssemblePdfDocument(byte[] contentBytes)
    {
        // Documento PDF estruturado
        var objects = new List<string>();

        // 1 0 obj: Catalog
        objects.Add("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj");

        // 2 0 obj: Pages
        objects.Add("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj");

        // 3 0 obj: Page
        objects.Add("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] /Contents 4 0 R /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> >>\nendobj");

        // 4 0 obj: Content stream
        objects.Add($"4 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n"); // stream content will be appended in bytes

        // 5 0 obj: Font F1 (Helvetica)
        var obj5 = "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj";

        // 6 0 obj: Font F2 (Helvetica-Bold)
        var obj6 = "6 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj";

        using var ms = new MemoryStream();
        var header = Encoding.ASCII.GetBytes("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
        ms.Write(header, 0, header.Length);

        var offsets = new List<long>();

        // Objeto 1
        offsets.Add(ms.Position);
        WriteAscii(ms, objects[0] + "\n");

        // Objeto 2
        offsets.Add(ms.Position);
        WriteAscii(ms, objects[1] + "\n");

        // Objeto 3
        offsets.Add(ms.Position);
        WriteAscii(ms, objects[2] + "\n");

        // Objeto 4 (Stream)
        offsets.Add(ms.Position);
        WriteAscii(ms, $"4 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
        ms.Write(contentBytes, 0, contentBytes.Length);
        WriteAscii(ms, "\nendstream\nendobj\n");

        // Objeto 5
        offsets.Add(ms.Position);
        WriteAscii(ms, obj5 + "\n");

        // Objeto 6
        offsets.Add(ms.Position);
        WriteAscii(ms, obj6 + "\n");

        // Tabela XREF
        var startXref = ms.Position;
        WriteAscii(ms, "xref\n");
        WriteAscii(ms, $"0 {offsets.Count + 1}\n");
        WriteAscii(ms, "0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            WriteAscii(ms, $"{offset:D10} 00000 n \n");
        }

        // Trailer
        WriteAscii(ms, "trailer\n");
        WriteAscii(ms, $"<< /Size {offsets.Count + 1} /Root 1 0 R >>\n");
        WriteAscii(ms, "startxref\n");
        WriteAscii(ms, $"{startXref}\n");
        WriteAscii(ms, "%%EOF\n");

        return ms.ToArray();
    }

    private static void WriteAscii(Stream stream, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }
}
