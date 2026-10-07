using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.WhatsApp.Application;

public interface IWhatsAppHealthAlertSender
{
    Task<Result> SendDisconnectionAlertAsync(
        string recipientEmail,
        string tenantName,
        string reconnectInstructionsUrl,
        string? reason = null,
        CancellationToken ct = default);
}
