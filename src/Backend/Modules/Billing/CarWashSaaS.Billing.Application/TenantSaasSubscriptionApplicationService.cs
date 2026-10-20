using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Application;

public sealed class TenantSaasSubscriptionApplicationService(
    ITenantSaasSubscriptionRepository subscriptionRepository,
    ITenantQuotaUsageRepository quotaUsageRepository,
    ISaasInvoiceRepository invoiceRepository,
    ISaasPlanRepository planRepository,
    ISaasBillingGatewayProvider gatewayProvider,
    IGlobalTenantLookup globalTenantLookup,
    ITenantPlanQuotaLookup quotaLookup)
{
    public async Task<Result<TenantSubscriptionOverviewDto>> GetOverviewAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await quotaLookup.GetSubscriptionOverviewAsync(tenantId, ct);
    }

    public async Task<Result<IReadOnlyList<SaasPlanDto>>> ListPlansAsync(CancellationToken ct = default)
    {
        var plans = await planRepository.ListActiveAsync(ct);
        if (plans.Count == 0)
        {
            await planRepository.EnsureSeedDataAsync(ct);
            plans = await planRepository.ListActiveAsync(ct);
        }

        var dtos = plans.Select(ToPlanDto).ToList();
        return Result<IReadOnlyList<SaasPlanDto>>.Success(dtos);
    }

    public async Task<Result<StripeCheckoutSessionDto>> CreateCheckoutSessionAsync(
        Guid tenantId,
        SaasPlanTier targetTier,
        string? successUrl = null,
        string? cancelUrl = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<StripeCheckoutSessionDto>.Failure(new Error("tenant.id.required", "TenantId é obrigatório.", ErrorType.Validation));
        }

        var targetPlan = await planRepository.GetByTierAsync(targetTier, ct);
        if (targetPlan is null)
        {
            return Result<StripeCheckoutSessionDto>.Failure(new Error("saas_plan.not_found", "Plano solicitado não foi encontrado.", ErrorType.NotFound));
        }

        var tenantSummaryResult = await globalTenantLookup.GetSummaryAsync(tenantId, ct);
        var customerName = tenantSummaryResult.IsSuccess ? tenantSummaryResult.Value!.Name : "Lava-Jato";
        var customerEmail = $"{tenantId}@lavaae.com";

        var sessionResult = await gatewayProvider.CreateSubscriptionCheckoutSessionAsync(
            tenantId,
            customerName,
            customerEmail,
            targetPlan,
            successUrl ?? string.Empty,
            cancelUrl ?? string.Empty,
            ct);

        if (!sessionResult.IsSuccess)
        {
            return Result<StripeCheckoutSessionDto>.Failure(sessionResult.Error!);
        }

        return Result<StripeCheckoutSessionDto>.Success(new StripeCheckoutSessionDto(
            sessionResult.Value!.SessionId,
            sessionResult.Value!.CheckoutUrl));
    }

    public async Task<Result<TenantSubscriptionOverviewDto>> SyncCheckoutSessionAsync(
        Guid tenantId,
        string sessionId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TenantSubscriptionOverviewDto>.Failure(new Error("tenant.id.required", "TenantId é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Result<TenantSubscriptionOverviewDto>.Failure(new Error("stripe.session_id.required", "SessionId é obrigatório.", ErrorType.Validation));
        }

        var detailsResult = await gatewayProvider.GetCheckoutSessionAsync(sessionId, ct);
        if (!detailsResult.IsSuccess)
        {
            return Result<TenantSubscriptionOverviewDto>.Failure(detailsResult.Error!);
        }

        var details = detailsResult.Value!;
        if (details.TenantId != Guid.Empty && details.TenantId != tenantId)
        {
            return Result<TenantSubscriptionOverviewDto>.Failure(new Error("stripe.session.tenant_mismatch", "Sessão pertence a outro estabelecimento.", ErrorType.Conflict));
        }

        var isPaid = string.Equals(details.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(details.Status, "complete", StringComparison.OrdinalIgnoreCase);

        if (!isPaid)
        {
            return await quotaLookup.GetSubscriptionOverviewAsync(tenantId, ct);
        }

        var subscription = await subscriptionRepository.GetByTenantIdAsync(tenantId, ct);
        if (subscription is null)
        {
            subscription = TenantSaasSubscription.CreateTrial(tenantId, defaultTier: details.PlanTier ?? SaasPlanTier.Pro).Value!;
            await subscriptionRepository.AddAsync(subscription, ct);
        }

        var targetTier = details.PlanTier ?? subscription.PlanTier;
        var targetPlan = await planRepository.GetByTierAsync(targetTier, ct)
                         ?? await planRepository.GetByTierAsync(subscription.PlanTier, ct);

        if (targetPlan is not null)
        {
            if (subscription.PlanTier != targetPlan.Tier)
            {
                subscription.ChangePlan(targetPlan);
            }

            subscription.Activate(targetPlan, details.GatewaySubscriptionId, details.GatewayCustomerId);
        }

        var now = DateTimeOffset.UtcNow;
        var nextCycle = now.AddMonths(1);
        subscription.RenewCycle(now, nextCycle);
        subscriptionRepository.Update(subscription);
        await subscriptionRepository.SaveChangesAsync(ct);

        var quotaUsage = await quotaUsageRepository.GetCurrentCycleByTenantIdAsync(tenantId, ct);
        if (quotaUsage is not null)
        {
            quotaUsage.ResetForNewCycle(now, nextCycle);
            quotaUsageRepository.Update(quotaUsage);
            await quotaUsageRepository.SaveChangesAsync(ct);
        }

        await globalTenantLookup.UpdateTenantStatusAsync(tenantId, new UpdateTenantStatusRequest(
            TenantStatus.Active,
            "Assinatura confirmada e ativada via checkout seguro."), ct);

        return await quotaLookup.GetSubscriptionOverviewAsync(tenantId, ct);
    }

    public async Task<Result<StripeCustomerPortalSessionDto>> CreateCustomerPortalSessionAsync(
        Guid tenantId,
        string? returnUrl = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<StripeCustomerPortalSessionDto>.Failure(new Error("tenant.id.required", "TenantId é obrigatório.", ErrorType.Validation));
        }

        var subscription = await subscriptionRepository.GetByTenantIdAsync(tenantId, ct);
        if (subscription is null || string.IsNullOrWhiteSpace(subscription.GatewayCustomerId))
        {
            return Result<StripeCustomerPortalSessionDto>.Failure(new Error("stripe.customer_id.missing", "Estabelecimento não possui conta de cobrança configurada na Stripe.", ErrorType.Conflict));
        }

        var portalResult = await gatewayProvider.CreateCustomerPortalSessionAsync(
            subscription.GatewayCustomerId,
            returnUrl ?? string.Empty,
            ct);

        if (!portalResult.IsSuccess)
        {
            return Result<StripeCustomerPortalSessionDto>.Failure(portalResult.Error!);
        }

        return Result<StripeCustomerPortalSessionDto>.Success(new StripeCustomerPortalSessionDto(
            portalResult.Value!.PortalUrl));
    }

    public async Task<Result<TenantSubscriptionOverviewDto>> ChangePlanAsync(
        Guid tenantId,
        SaasPlanTier targetTier,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TenantSubscriptionOverviewDto>.Failure(new Error("tenant.id.required", "TenantId é obrigatório.", ErrorType.Validation));
        }

        var subscription = await subscriptionRepository.GetByTenantIdAsync(tenantId, ct);
        if (subscription is null)
        {
            subscription = TenantSaasSubscription.CreateTrial(tenantId).Value!;
            await subscriptionRepository.AddAsync(subscription, ct);
        }

        if (subscription.PlanTier == targetTier)
        {
            return Result<TenantSubscriptionOverviewDto>.Failure(new Error("saas_plan.same_tier", "O estabelecimento já está no plano solicitado.", ErrorType.Validation));
        }

        var targetPlan = await planRepository.GetByTierAsync(targetTier, ct);
        if (targetPlan is null)
        {
            return Result<TenantSubscriptionOverviewDto>.Failure(new Error("saas_plan.not_found", "Plano solicitado não foi encontrado.", ErrorType.NotFound));
        }

        var changeResult = subscription.ChangePlan(targetPlan);
        if (!changeResult.IsSuccess)
        {
            return Result<TenantSubscriptionOverviewDto>.Failure(changeResult.Error!);
        }

        // Se estiver ativando pela primeira vez a partir de um trial:
        if (subscription.Status == TenantSubscriptionStatus.Trial)
        {
            var tenantSummaryResult = await globalTenantLookup.GetSummaryAsync(tenantId, ct);
            var customerName = tenantSummaryResult.IsSuccess ? tenantSummaryResult.Value!.Name : "Lava-Jato";
            var customerEmail = $"{tenantId}@lavaae.com";

            var gatewaySubResult = await gatewayProvider.CreateSubscriptionAsync(tenantId, customerName, customerEmail, targetPlan, ct);
            if (gatewaySubResult.IsSuccess)
            {
                var g = gatewaySubResult.Value!;
                subscription.Activate(targetPlan, g.GatewaySubscriptionId, g.GatewayCustomerId);

                // Cria fatura pendente inicial com QR Code Pix
                var invoiceResult = SaasInvoice.CreatePending(
                    tenantId,
                    g.InitialInvoiceId,
                    targetPlan.MonthlyPrice,
                    dueDateUtc: DateTimeOffset.UtcNow.AddDays(3),
                    paymentUrl: g.PaymentUrl,
                    pixQrCode: g.PixQrCode,
                    pixCopiaECola: g.PixCopiaECola);

                if (invoiceResult.IsSuccess)
                {
                    await invoiceRepository.AddAsync(invoiceResult.Value!, ct);
                    await invoiceRepository.SaveChangesAsync(ct);
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(subscription.GatewaySubscriptionId))
        {
            await gatewayProvider.ChangeSubscriptionPlanAsync(subscription.GatewaySubscriptionId, targetPlan, ct);
        }

        subscriptionRepository.Update(subscription);
        await subscriptionRepository.SaveChangesAsync(ct);

        return await quotaLookup.GetSubscriptionOverviewAsync(tenantId, ct);
    }

    public async Task<Result<TenantSubscriptionOverviewDto>> SimulateSettleInvoiceAsync(
        Guid tenantId,
        Guid invoiceId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TenantSubscriptionOverviewDto>.Failure(new Error("tenant.id.required", "TenantId é obrigatório.", ErrorType.Validation));
        }

        var invoice = await invoiceRepository.GetByIdAsync(invoiceId, ct);
        if (invoice is null || invoice.TenantId != tenantId)
        {
            return Result<TenantSubscriptionOverviewDto>.Failure(new Error("saas_invoice.not_found", "Fatura não encontrada.", ErrorType.NotFound));
        }

        var paidResult = invoice.MarkPaid(DateTimeOffset.UtcNow);
        if (!paidResult.IsSuccess)
        {
            return Result<TenantSubscriptionOverviewDto>.Failure(paidResult.Error!);
        }

        invoiceRepository.Update(invoice);
        await invoiceRepository.SaveChangesAsync(ct);

        var subscription = await subscriptionRepository.GetByTenantIdAsync(tenantId, ct);
        if (subscription is not null)
        {
            var now = DateTimeOffset.UtcNow;
            var nextCycle = now.AddMonths(1);
            subscription.RenewCycle(now, nextCycle);
            subscriptionRepository.Update(subscription);
            await subscriptionRepository.SaveChangesAsync(ct);

            var quotaUsage = await quotaUsageRepository.GetCurrentCycleByTenantIdAsync(tenantId, ct);
            if (quotaUsage is not null)
            {
                quotaUsage.ResetForNewCycle(now, nextCycle);
                quotaUsageRepository.Update(quotaUsage);
                await quotaUsageRepository.SaveChangesAsync(ct);
            }

            await globalTenantLookup.UpdateTenantStatusAsync(tenantId, new UpdateTenantStatusRequest(
                TenantStatus.Active,
                "Fatura quitada com sucesso."), ct);
        }

        return await quotaLookup.GetSubscriptionOverviewAsync(tenantId, ct);
    }

    private static SaasPlanDto ToPlanDto(SaasPlan plan) => new(
        plan.Tier,
        plan.Name,
        plan.Description,
        plan.MonthlyPrice,
        plan.MaxWorkOrdersPerCycle,
        plan.MaxWhatsAppMessagesPerCycle,
        plan.HasCustomerSubscriptions,
        plan.HasLoyalty,
        plan.HasCommissions,
        plan.HasAiChatbot,
        plan.MaxTeamMembers,
        plan.MaxBoxes,
        plan.IsActive);
}
