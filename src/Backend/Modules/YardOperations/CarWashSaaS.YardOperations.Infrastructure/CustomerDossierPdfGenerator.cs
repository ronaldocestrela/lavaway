using System.Globalization;
using System.Text;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class CustomerDossierPdfGenerator : ICustomerDossierPdfGenerator
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public Result<byte[]> GenerateDossierPdf(CustomerDossierDto dossier, StoreProfileDto? storeProfile)
    {
        if (dossier.TenantId == Guid.Empty || dossier.Profile.CustomerId == Guid.Empty)
        {
            return Result<byte[]>.Failure(new Error("dossier_pdf.invalid_data", "Tenant e identificador do titular são obrigatórios.", ErrorType.Validation));
        }

        try
        {
            var pdfBytes = BuildPdf(dossier, storeProfile);
            return Result<byte[]>.Success(pdfBytes);
        }
        catch (Exception ex)
        {
            return Result<byte[]>.Failure(new Error("dossier_pdf.generation_error", $"Falha ao gerar dossiê em PDF: {ex.Message}", ErrorType.Validation));
        }
    }

    private static byte[] BuildPdf(CustomerDossierDto dossier, StoreProfileDto? storeProfile)
    {
        var sb = new StringBuilder();

        // 1. Top Header Banner (0.15 0.39 0.92)
        sb.AppendLine("0.15 0.39 0.92 rg");
        sb.AppendLine("0 770 595 72 re f");

        var storeName = storeProfile?.TradeName ?? "LAVA AÊ — ESTÉTICA AUTOMOTIVA";
        var storeContact = storeProfile is not null
            ? $"{storeProfile.Street}, {storeProfile.City}-{storeProfile.State} | Tel: {storeProfile.Phone}"
            : "Rede Credenciada Lavaway SaaS";

        sb.AppendLine("1 1 1 rg");
        sb.AppendLine("BT /F2 15 Tf 36 815 Td (" + EscapePdfText(storeName.ToUpperInvariant()) + ") Tj ET");
        sb.AppendLine("BT /F1 9 Tf 36 798 Td (" + EscapePdfText(storeContact) + ") Tj ET");
        sb.AppendLine("BT /F2 11 Tf 380 815 Td (DOSSIE DO TITULAR LGPD) Tj ET");
        sb.AppendLine("BT /F1 8 Tf 380 798 Td (Art. 18, II e V - Lei 13.709/2018) Tj ET");

        var currentY = 740;

        // 2. Sumário Cadastral do Titular
        sb.AppendLine("0.15 0.39 0.92 rg");
        sb.AppendLine($"BT /F2 10 Tf 36 {currentY} Td (1. DADOS CADASTRAIS DO TITULAR) Tj ET");
        currentY -= 14;

        sb.AppendLine("0.2 0.2 0.2 rg");
        var nameDisplay = dossier.Profile.IsAnonymized ? $"{dossier.Profile.Name} [DADOS OFUSCADOS]" : dossier.Profile.Name;
        var phoneDisplay = string.IsNullOrWhiteSpace(dossier.Profile.Phone) ? "Nao cadastrado ou anonimizado" : dossier.Profile.Phone;
        var dateDisplay = dossier.GeneratedAtUtc.ToString("dd/MM/yyyy HH:mm 'UTC'", PtBr);

        sb.AppendLine($"BT /F1 9 Tf 36 {currentY} Td (Nome / Razao: " + EscapePdfText(nameDisplay) + ") Tj ET");
        currentY -= 12;
        sb.AppendLine($"BT /F1 9 Tf 36 {currentY} Td (Telefone de Contato: " + EscapePdfText(phoneDisplay) + "   |   Data de Emissao: " + EscapePdfText(dateDisplay) + ") Tj ET");
        currentY -= 12;
        sb.AppendLine($"BT /F1 9 Tf 36 {currentY} Td (ID do Titular: " + dossier.Profile.CustomerId + ") Tj ET");
        currentY -= 20;

        // 3. Veículos Associados
        sb.AppendLine("0.15 0.39 0.92 rg");
        sb.AppendLine($"BT /F2 10 Tf 36 {currentY} Td (2. VEICULOS CADASTRADOS NA PLATAFORMA) Tj ET");
        currentY -= 14;

        if (dossier.Vehicles.Count == 0)
        {
            sb.AppendLine("0.4 0.4 0.4 rg");
            sb.AppendLine($"BT /F1 9 Tf 36 {currentY} Td (Nenhum veiculo atualmente vinculado ao titular.) Tj ET");
            currentY -= 18;
        }
        else
        {
            sb.AppendLine($"0.92 0.94 0.98 rg 36 {currentY - 3} 523 15 re f");
            sb.AppendLine("0.2 0.2 0.2 rg");
            sb.AppendLine($"BT /F2 8 Tf 42 {currentY} Td (Placa) Tj ET");
            sb.AppendLine($"BT /F2 8 Tf 180 {currentY} Td (Categoria de Porte) Tj ET");
            sb.AppendLine($"BT /F2 8 Tf 340 {currentY} Td (Identificador Interno) Tj ET");
            currentY -= 14;

            sb.AppendLine("0.1 0.1 0.1 rg");
            foreach (var veh in dossier.Vehicles.Take(5))
            {
                sb.AppendLine($"BT /F1 8 Tf 42 {currentY} Td (" + EscapePdfText(veh.Plate) + ") Tj ET");
                sb.AppendLine($"BT /F1 8 Tf 180 {currentY} Td (" + EscapePdfText(veh.SizeCategory) + ") Tj ET");
                sb.AppendLine($"BT /F1 8 Tf 340 {currentY} Td (" + veh.VehicleId + ") Tj ET");
                currentY -= 12;
            }
            currentY -= 10;
        }

        // 4. Histórico de Ordens de Serviço
        sb.AppendLine("0.15 0.39 0.92 rg");
        sb.AppendLine($"BT /F2 10 Tf 36 {currentY} Td (3. HISTORICO DE SERVICOS CONTRATADOS & FATURAMENTO) Tj ET");
        currentY -= 14;

        if (dossier.WorkOrders.Count == 0)
        {
            sb.AppendLine("0.4 0.4 0.4 rg");
            sb.AppendLine($"BT /F1 9 Tf 36 {currentY} Td (Nenhuma ordem de servico registrada para este titular.) Tj ET");
            currentY -= 20;
        }
        else
        {
            sb.AppendLine($"0.92 0.94 0.98 rg 36 {currentY - 3} 523 15 re f");
            sb.AppendLine("0.2 0.2 0.2 rg");
            sb.AppendLine($"BT /F2 8 Tf 42 {currentY} Td (Data) Tj ET");
            sb.AppendLine($"BT /F2 8 Tf 110 {currentY} Td (Status) Tj ET");
            sb.AppendLine($"BT /F2 8 Tf 200 {currentY} Td (Servicos Executados) Tj ET");
            sb.AppendLine($"BT /F2 8 Tf 400 {currentY} Td (Total) Tj ET");
            sb.AppendLine($"BT /F2 8 Tf 460 {currentY} Td (Avaliacao) Tj ET");
            currentY -= 14;

            sb.AppendLine("0.1 0.1 0.1 rg");
            foreach (var wo in dossier.WorkOrders.Take(8))
            {
                var woDate = wo.CreatedAtUtc.ToString("dd/MM/yyyy", PtBr);
                var servicesSummary = string.Join(", ", wo.Services.Select(s => s.ServiceName));
                if (servicesSummary.Length > 35) servicesSummary = servicesSummary[..32] + "...";
                var surveyText = wo.Survey?.Rating.HasValue == true ? $"{wo.Survey.Rating.Value}/5 estrelas" : "-";

                sb.AppendLine($"BT /F1 8 Tf 42 {currentY} Td (" + EscapePdfText(woDate) + ") Tj ET");
                sb.AppendLine($"BT /F1 8 Tf 110 {currentY} Td (" + EscapePdfText(wo.Status) + ") Tj ET");
                sb.AppendLine($"BT /F1 8 Tf 200 {currentY} Td (" + EscapePdfText(servicesSummary) + ") Tj ET");
                sb.AppendLine($"BT /F2 8 Tf 400 {currentY} Td (" + EscapePdfText(wo.TotalAmount.ToString("C2", PtBr)) + ") Tj ET");
                sb.AppendLine($"BT /F1 8 Tf 460 {currentY} Td (" + EscapePdfText(surveyText) + ") Tj ET");
                currentY -= 12;
            }
            currentY -= 10;
        }

        // 5. Histórico de Comunicação WhatsApp
        sb.AppendLine("0.15 0.39 0.92 rg");
        sb.AppendLine($"BT /F2 10 Tf 36 {currentY} Td (4. CANAL DE COMUNICACAO WHATSAPP & CONSENTIMENTO) Tj ET");
        currentY -= 14;

        var optStatus = dossier.CommunicationHistory.IsOptedIn ? "Ativo (Opt-in concedido)" : "Inativo (Opt-out registrado)";
        var msgCount = dossier.CommunicationHistory.OutboundMessages.Count;
        sb.AppendLine("0.2 0.2 0.2 rg");
        sb.AppendLine($"BT /F1 9 Tf 36 {currentY} Td (Preferencia de Notificacoes: " + EscapePdfText(optStatus) + "   |   Total de Disparos Auditados: " + msgCount + ") Tj ET");
        currentY -= 22;

        // 6. Termo Regulatório / LGPD (Art. 18, II e V)
        sb.AppendLine("0.80 0.83 0.88 RG 1 w 36 " + (currentY + 6) + " m 559 " + (currentY + 6) + " l S");
        currentY -= 10;

        sb.AppendLine("0.15 0.39 0.92 rg");
        sb.AppendLine($"BT /F2 9 Tf 36 {currentY} Td (DECLARACAO E NOTA DE PRIVACIDADE - LGPD) Tj ET");
        currentY -= 12;

        sb.AppendLine("0.35 0.35 0.35 rg");
        sb.AppendLine($"BT /F1 7.5 Tf 36 {currentY} Td (" + EscapePdfText(dossier.LegalNotice.LegalBasis) + ") Tj ET");
        currentY -= 10;
        sb.AppendLine($"BT /F1 7.5 Tf 36 {currentY} Td (" + EscapePdfText(dossier.LegalNotice.RetentionPolicyNotice) + ") Tj ET");
        currentY -= 10;
        sb.AppendLine($"BT /F1 7.5 Tf 36 {currentY} Td (Canal do DPO / Encarregado pelo Tratamento de Dados: " + EscapePdfText(dossier.LegalNotice.DpoContactEmail) + ") Tj ET");

        var contentBytes = Encoding.Latin1.GetBytes(sb.ToString());
        return BuildPdfStructure(contentBytes);
    }

    private static byte[] BuildPdfStructure(byte[] contentBytes)
    {
        var objects = new string[]
        {
            // 1 0 obj: Catalog
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj",
            // 2 0 obj: Pages
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj",
            // 3 0 obj: Page (A4: 595.28 x 841.89)
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] /Contents 4 0 R /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> >>\nendobj"
        };

        var obj5 = "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj";
        var obj6 = "6 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj";

        using var ms = new MemoryStream();
        var header = Encoding.ASCII.GetBytes("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
        ms.Write(header, 0, header.Length);

        var offsets = new List<long>();

        offsets.Add(ms.Position);
        WriteAscii(ms, objects[0] + "\n");

        offsets.Add(ms.Position);
        WriteAscii(ms, objects[1] + "\n");

        offsets.Add(ms.Position);
        WriteAscii(ms, objects[2] + "\n");

        offsets.Add(ms.Position);
        WriteAscii(ms, $"4 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
        ms.Write(contentBytes, 0, contentBytes.Length);
        WriteAscii(ms, "\nendstream\nendobj\n");

        offsets.Add(ms.Position);
        WriteAscii(ms, obj5 + "\n");

        offsets.Add(ms.Position);
        WriteAscii(ms, obj6 + "\n");

        var startXref = ms.Position;
        WriteAscii(ms, "xref\n");
        WriteAscii(ms, $"0 {offsets.Count + 1}\n");
        WriteAscii(ms, "0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            WriteAscii(ms, $"{offset:D10} 00000 n \n");
        }

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

    private static string EscapePdfText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                if (c == '(' || c == ')' || c == '\\')
                {
                    sb.Append('\\');
                }
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
