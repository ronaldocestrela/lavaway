using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Application;

public sealed class TenantPlanQuotaApplicationService(
    ITenantSaasSubscriptionRepository subscriptionRepository,
    ITenantQuotaUsageRepository quotaUsageRepository,
    ISaasPlanRepository planRepository,
    ISaasInvoiceRepository invoiceRepository,
    IGlobalTenantLookup globalTenantLookup) : ITenantPlanQuotaLookup
{
    public async Task<Result<TenantQuotaStatusDto>> CheckWorkOrderQuotaAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TenantQuotaStatusDto>.Failure(new Error("tenant.id.required", "TenantId é obrigatório.", ErrorType.Validation));
        }

        var (subscription, plan, quotaUsage) = await EnsureSubscriptionAndQuotaAsync(tenantId, ct);

        if (!subscription.IsOperationalAllowed())
        {
            return Result<TenantQuotaStatusDto>.Success(new TenantQuotaStatusDto(
                CanProceed: false,
                PlanTier: subscription.PlanTier,
                SubscriptionStatus: subscription.Status,
                UsedWorkOrders: quotaUsage.WorkOrdersCreatedCount,
                MaxWorkOrders: plan.MaxWorkOrdersPerCycle,
                UsedWhatsAppMessages: quotaUsage.WhatsAppMessagesSentCount,
                MaxWhatsAppMessages: plan.MaxWhatsAppMessagesPerCycle,
                Reason: subscription.StatusReason ?? "Operação bloqueada por pendência financeira. Regularize sua assinatura."));
        }

        var canCreate = quotaUsage.CanCreateWorkOrder(plan.MaxWorkOrdersPerCycle);
        string? reason = canCreate ? null : $"Limite mensal de {plan.MaxWorkOrdersPerCycle} ordens de serviço atingido para o plano {plan.Name}. Faça upgrade para continuar.";

        return Result<TenantQuotaStatusDto>.Success(new TenantQuotaStatusDto(
            CanProceed: canCreate,
            PlanTier: subscription.PlanTier,
            SubscriptionStatus: subscription.Status,
            UsedWorkOrders: quotaUsage.WorkOrdersCreatedCount,
            MaxWorkOrders: plan.MaxWorkOrdersPerCycle,
            UsedWhatsAppMessages: quotaUsage.WhatsAppMessagesSentCount,
            MaxWhatsAppMessages: plan.MaxWhatsAppMessagesPerCycle,
            Reason: reason));
    }

    public async Task<Result> ConsumeWorkOrderQuotaAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure(new Error("tenant.id.required", "TenantId é obrigatório.", ErrorType.Validation));
        }

        var (subscription, plan, quotaUsage) = await EnsureSubscriptionAndQuotaAsync(tenantId, ct);

        if (!subscription.IsOperationalAllowed())
        {
            return Result.Failure(new Error("tenant.subscription.delinquent", subscription.StatusReason ?? "Operação bloqueada por pendência financeira.", ErrorType.Conflict));
        }

        var recordResult = quotaUsage.RecordWorkOrderCreated(plan.MaxWorkOrdersPerCycle);
        if (!recordResult.IsSuccess)
        {
            return recordResult;
        }

        quotaUsageRepository.Update(quotaUsage);
        await quotaUsageRepository.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<TenantQuotaStatusDto>> CheckWhatsAppQuotaAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TenantQuotaStatusDto>.Failure(new Error("tenant.id.required", "TenantId é obrigatório.", ErrorType.Validation));
        }

        var (subscription, plan, quotaUsage) = await EnsureSubscriptionAndQuotaAsync(tenantId, ct);

        if (!subscription.IsOperationalAllowed())
        {
            return Result<TenantQuotaStatusDto>.Success(new TenantQuotaStatusDto(
                CanProceed: false,
                PlanTier: subscription.PlanTier,
                SubscriptionStatus: subscription.Status,
                UsedWorkOrders: quotaUsage.WorkOrdersCreatedCount,
                MaxWorkOrders: plan.MaxWorkOrdersPerCycle,
                UsedWhatsAppMessages: quotaUsage.WhatsAppMessagesSentCount,
                MaxWhatsAppMessages: plan.MaxWhatsAppMessagesPerCycle,
                Reason: subscription.StatusReason ?? "Envios bloqueados por pendência financeira. Regularize sua assinatura."));
        }

        var canSend = quotaUsage.CanSendWhatsApp(plan.MaxWhatsAppMessagesPerCycle);
        string? reason = canSend ? null : $"Limite mensal de {plan.MaxWhatsAppMessagesPerCycle} mensagens WhatsApp atingido para o plano {plan.Name}. Faça upgrade para continuar.";

        return Result<TenantQuotaStatusDto>.Success(new TenantQuotaStatusDto(
            CanProceed: canSend,
            PlanTier: subscription.PlanTier,
            SubscriptionStatus: subscription.Status,
            UsedWorkOrders: quotaUsage.WorkOrdersCreatedCount,
            MaxWorkOrders: plan.MaxWorkOrdersPerCycle,
            UsedWhatsAppMessages: quotaUsage.WhatsAppMessagesSentCount,
            MaxWhatsAppMessages: plan.MaxWhatsAppMessagesPerCycle,
            Reason: reason));
    }

    public async Task<Result> ConsumeWhatsAppQuotaAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure(new Error("tenant.id.required", "TenantId é obrigatório.", ErrorType.Validation));
        }

        var (subscription, plan, quotaUsage) = await EnsureSubscriptionAndQuotaAsync(tenantId, ct);

        if (!subscription.IsOperationalAllowed())
        {
            return Result.Failure(new Error("tenant.subscription.delinquent", subscription.StatusReason ?? "Envios bloqueados por pendência financeira.", ErrorType.Conflict));
        }

        var recordResult = quotaUsage.RecordWhatsAppSent(plan.MaxWhatsAppMessagesPerCycle);
        if (!recordResult.IsSuccess)
        {
            return recordResult;
        }

        quotaUsageRepository.Update(quotaUsage);
        await quotaUsageRepository.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<TenantSubscriptionOverviewDto>> GetSubscriptionOverviewAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TenantSubscriptionOverviewDto>.Failure(new Error("tenant.id.required", "TenantId é obrigatório.", ErrorType.Validation));
        }

        var (subscription, plan, quotaUsage) = await EnsureSubscriptionAndQuotaAsync(tenantId, ct);
        var invoices = await invoiceRepository.ListByTenantIdAsync(tenantId, ct);
        var pendingInvoice = invoices.FirstOrDefault(i => i.Status == "Pending" || i.Status == "Overdue");

        var tenantSummaryResult = await globalTenantLookup.GetSummaryAsync(tenantId, ct);
        var tenantName = tenantSummaryResult.IsSuccess ? tenantSummaryResult.Value!.Name : "Estabelecimento";

        var invoiceDtos = invoices.Select(ToDto).ToList();
        var pendingDto = pendingInvoice is not null ? ToDto(pendingInvoice) : null;

        var overview = new TenantSubscriptionOverviewDto(
            TenantId: tenantId,
            TenantName: tenantName,
            PlanTier: subscription.PlanTier,
            PlanName: plan.Name,
            MonthlyPrice: subscription.MonthlyPrice,
            Status: subscription.Status,
            CurrentPeriodStartUtc: subscription.CurrentPeriodStartUtc,
            CurrentPeriodEndUtc: subscription.CurrentPeriodEndUtc,
            GracePeriodEndsAtUtc: subscription.GracePeriodEndsAtUtc,
            NextBillingDateUtc: subscription.NextBillingDateUtc,
            UsedWorkOrders: quotaUsage.WorkOrdersCreatedCount,
            MaxWorkOrders: plan.MaxWorkOrdersPerCycle,
            UsedWhatsAppMessages: quotaUsage.WhatsAppMessagesSentCount,
            MaxWhatsAppMessages: plan.MaxWhatsAppMessagesPerCycle,
            GatewaySubscriptionId: subscription.GatewaySubscriptionId,
            PendingInvoice: pendingDto,
            Invoices: invoiceDtos);

        return Result<TenantSubscriptionOverviewDto>.Success(overview);
    }

    private async Task<(TenantSaasSubscription Subscription, SaasPlan Plan, TenantQuotaUsage QuotaUsage)> EnsureSubscriptionAndQuotaAsync(
        Guid tenantId,
        CancellationToken ct)
    {
        var subscription = await subscriptionRepository.GetByTenantIdAsync(tenantId, ct);
        if (subscription is null)
        {
            subscription = TenantSaasSubscription.CreateTrial(tenantId).Value!;
            await subscriptionRepository.AddAsync(subscription, ct);
            await subscriptionRepository.SaveChangesAsync(ct);
        }

        var plan = await planRepository.GetByTierAsync(subscription.PlanTier, ct)
                   ?? SaasPlan.GetStandardPlans().First(p => p.Tier == subscription.PlanTier);

        var quotaUsage = await quotaUsageRepository.GetCurrentCycleByTenantIdAsync(tenantId, ct);
        if (quotaUsage is null)
        {
            quotaUsage = TenantQuotaUsage.CreateForCycle(tenantId, subscription.CurrentPeriodStartUtc, subscription.CurrentPeriodEndUtc).Value!;
            await quotaUsageRepository.AddAsync(quotaUsage, ct);
            await quotaUsageRepository.SaveChangesAsync(ct);
        }

        return (subscription, plan, quotaUsage);
    }

    private static SaasInvoiceDto ToDto(SaasInvoice invoice) => new(
        invoice.Id,
        invoice.TenantId,
        invoice.GatewayInvoiceId,
        invoice.Amount,
        invoice.DueDateUtc,
        invoice.PaidAtUtc,
        invoice.Status,
        invoice.PaymentUrl,
        invoice.PixQrCode,
        invoice.PixCopiaECola);
}
