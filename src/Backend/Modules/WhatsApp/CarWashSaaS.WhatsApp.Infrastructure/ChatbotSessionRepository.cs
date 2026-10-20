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
            .Where(s => s.TenantId == tenantId &&
                        s.CustomerPhone == normalizedPhone &&
                        s.IsActive)
            .OrderByDescending(s => s.LastInteractionAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(ChatbotConversationSession session, CancellationToken ct = default)
    {
        await dbContext.ChatbotSessions.AddAsync(session, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ChatbotConversationSession session, CancellationToken ct = default)
    {
        if (dbContext.Entry(session).State == EntityState.Detached)
        {
            dbContext.ChatbotSessions.Update(session);
        }
        await dbContext.SaveChangesAsync(ct);
    }
}
