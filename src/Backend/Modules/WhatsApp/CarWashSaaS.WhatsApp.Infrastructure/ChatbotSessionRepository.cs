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

    public async Task<int> PurgeInactiveSessionsOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken ct = default)
    {
        var expiredSessions = await dbContext.ChatbotSessions
            .IgnoreQueryFilters()
            .Where(s => (!s.IsActive || s.CurrentStep == ChatbotStep.Completed) && s.LastInteractionAtUtc <= cutoffUtc)
            .ToListAsync(ct);

        if (expiredSessions.Count == 0) return 0;

        dbContext.ChatbotSessions.RemoveRange(expiredSessions);
        await dbContext.SaveChangesAsync(ct);
        return expiredSessions.Count;
    }
}
