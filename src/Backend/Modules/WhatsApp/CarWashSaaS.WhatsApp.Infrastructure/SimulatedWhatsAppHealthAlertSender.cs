using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Application;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.WhatsApp.Infrastructure;

public sealed class SimulatedWhatsAppHealthAlertSender(
    ILogger<SimulatedWhatsAppHealthAlertSender>? logger = null) : IWhatsAppHealthAlertSender
{
    public Task<Result> SendDisconnectionAlertAsync(
        string recipientEmail,
        string tenantName,
        string reconnectInstructionsUrl,
        string? reason = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            return Task.FromResult(Result.Failure(new Error(
                "alert.recipient_required",
                "Recipient email is required for WhatsApp disconnection alert.",
                ErrorType.Validation)));
        }

        logger?.LogWarning(
            "[WHATSAPP HEALTH ALERT] Conexão WhatsApp caiu para o lojista '{TenantName}' ({RecipientEmail}). Motivo: {Reason}. Link de reconexão: {Url}",
            tenantName,
            recipientEmail,
            reason ?? "Desconexão detectada",
            reconnectInstructionsUrl);

        return Task.FromResult(Result.Success());
    }
}
