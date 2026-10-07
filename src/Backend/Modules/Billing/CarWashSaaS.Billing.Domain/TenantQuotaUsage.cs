using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Domain;

public sealed class TenantQuotaUsage : IMustHaveTenant
{
    private TenantQuotaUsage()
    {
    }

    private TenantQuotaUsage(
        Guid id,
        Guid tenantId,
        DateTimeOffset cycleStartUtc,
        DateTimeOffset cycleEndUtc,
        int workOrdersCount,
        int whatsAppMessagesCount)
    {
        Id = id;
        TenantId = tenantId;
        CycleStartUtc = cycleStartUtc;
        CycleEndUtc = cycleEndUtc;
        WorkOrdersCreatedCount = workOrdersCount;
        WhatsAppMessagesSentCount = whatsAppMessagesCount;
        LastUpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    public DateTimeOffset CycleStartUtc { get; private set; }
    public DateTimeOffset CycleEndUtc { get; private set; }
    public int WorkOrdersCreatedCount { get; private set; }
    public int WhatsAppMessagesSentCount { get; private set; }
    public DateTimeOffset LastUpdatedAtUtc { get; private set; }

    public static Result<TenantQuotaUsage> CreateForCycle(Guid tenantId, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TenantQuotaUsage>.Failure(new Error("quota_usage.tenant.required", "TenantId é obrigatório.", ErrorType.Validation));
        }

        if (endUtc <= startUtc)
        {
            return Result<TenantQuotaUsage>.Failure(new Error("quota_usage.dates.invalid", "Data final do ciclo deve ser posterior à inicial.", ErrorType.Validation));
        }

        return Result<TenantQuotaUsage>.Success(new TenantQuotaUsage(
            Guid.CreateVersion7(),
            tenantId,
            startUtc,
            endUtc,
            workOrdersCount: 0,
            whatsAppMessagesCount: 0));
    }

    public bool CanCreateWorkOrder(int maxLimit)
    {
        if (maxLimit <= 0) return true; // 0 ou negativo = ilimitado
        return WorkOrdersCreatedCount < maxLimit;
    }

    public Result RecordWorkOrderCreated(int maxLimit)
    {
        if (!CanCreateWorkOrder(maxLimit))
        {
            return Result.Failure(new Error(
                "tenant.quota.work_orders_exceeded",
                $"Limite mensal de ordens de serviço ({maxLimit} OS) atingido para este plano. Faça upgrade para continuar emitindo ordens.",
                ErrorType.Conflict));
        }

        WorkOrdersCreatedCount++;
        LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public bool CanSendWhatsApp(int maxLimit)
    {
        if (maxLimit <= 0) return true; // 0 ou negativo = ilimitado
        return WhatsAppMessagesSentCount < maxLimit;
    }

    public Result RecordWhatsAppSent(int maxLimit)
    {
        if (!CanSendWhatsApp(maxLimit))
        {
            return Result.Failure(new Error(
                "tenant.quota.whatsapp_exceeded",
                $"Limite mensal de mensagens WhatsApp ({maxLimit} mensagens) atingido para este plano. Faça upgrade para continuar enviando notificações.",
                ErrorType.Conflict));
        }

        WhatsAppMessagesSentCount++;
        LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public void ResetForNewCycle(DateTimeOffset newStartUtc, DateTimeOffset newEndUtc)
    {
        CycleStartUtc = newStartUtc;
        CycleEndUtc = newEndUtc;
        WorkOrdersCreatedCount = 0;
        WhatsAppMessagesSentCount = 0;
        LastUpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
