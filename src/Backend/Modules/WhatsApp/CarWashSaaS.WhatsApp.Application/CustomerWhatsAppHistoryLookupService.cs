using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.WhatsApp.Application;

public sealed class CustomerWhatsAppHistoryLookupService(
    ICustomerCommunicationPreferenceRepository preferenceRepository,
    IOutboundWhatsAppMessageRepository messageRepository) : ICustomerWhatsAppHistoryLookup
{
    public async Task<Result<WhatsAppHistoryDossierDto>> GetCustomerWhatsAppHistoryAsync(
        Guid tenantId,
        string phone,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(phone))
        {
            return Result<WhatsAppHistoryDossierDto>.Success(new WhatsAppHistoryDossierDto(false, null, null, []));
        }

        var normalized = OutboundWhatsAppMessage.CleanPhoneNumber(phone);
        var pref = await preferenceRepository.GetByPhoneAsync(tenantId, normalized, ct);
        var messages = await messageRepository.GetByRecipientPhoneAsync(tenantId, normalized, 50, ct);

        var messageDtos = messages.Select(m => new OutboundMessageDossierDto(
            m.SentAtUtc ?? m.CreatedAt,
            m.MediaType ?? "Text",
            m.Status.ToString())).ToList();

        var history = new WhatsAppHistoryDossierDto(
            pref?.IsOptedIn ?? true,
            pref?.UpdatedAt,
            pref?.Reason,
            messageDtos);

        return Result<WhatsAppHistoryDossierDto>.Success(history);
    }
}
