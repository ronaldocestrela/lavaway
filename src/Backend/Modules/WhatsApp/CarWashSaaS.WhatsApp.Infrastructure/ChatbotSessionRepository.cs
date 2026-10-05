using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.WhatsApp.Infrastructure;

public sealed class ChatbotSessionRepository(WhatsAppDbContext dbContext) : IChatbotSessionRepository
{
    public async Task<ChatbotConversationSession?> GetActiveByPhoneAsync(
        Guid tenantId,
        string customerPhone,
        CancellationToken ct = default)
    {
        var normalizedPhone = new string(customerPhone.Where(char.IsDigit).ToArray());
        return await dbContext.ChatbotSessions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId &&
                                      s.CustomerPhone == normalizedPhone &&
                                      s.IsActive, ct);
    }

    public async Task AddAsync(ChatbotConversationSession session, CancellationToken ct = default)
    {
        await dbContext.ChatbotSessions.AddAsync(session, ct);
    }

    public Task UpdateAsync(ChatbotConversationSession session, CancellationToken ct = default)
    {
        dbContext.ChatbotSessions.Update(session);
        return Task.CompletedTask;
    }
}
