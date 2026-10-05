using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.WhatsApp.Application;

public interface IChatbotSessionRepository
{
    Task<ChatbotConversationSession?> GetActiveByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default);
    Task AddAsync(ChatbotConversationSession session, CancellationToken ct = default);
    Task UpdateAsync(ChatbotConversationSession session, CancellationToken ct = default);
}
