using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.Billing.Application;

public sealed class SaasBillingWebhookApplicationService(
    ISaasBillingGatewayProvider gatewayProvider,
    IProcessedSaasWebhookEventRepository processedEventsRepository,
    ITenantSaasSubscriptionRepository subscriptionRepository,
    ITenantQuotaUsageRepository quotaUsageRepository,
    ISaasInvoiceRepository invoiceRepository,
    ISaasPlanRepository planRepository,
    IGlobalTenantLookup globalTenantLookup,
    ILogger<SaasBillingWebhookApplicationService>? logger = null)
{
    public async Task<Result> ProcessWebhookAsync(
        SaasBillingWebhookPayload payload,
        string? rawPayload = null,
        string? signatureHeader = null,
        string? webhookSecret = null,
        CancellationToken ct = default)
    {
        if (payload is null)
        {
            logger?.LogWarning("Payload do webhook recebido é nulo.");
            return Result.Failure(new Error("webhook.payload.required", "Payload do webhook é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(payload.EventId))
        {
            logger?.LogWarning("Payload do webhook não possui EventId.");
            return Result.Failure(new Error("webhook.event_id.required", "EventId do webhook é obrigatório.", ErrorType.Validation));
        }

        // Validação de assinatura se fornecida ou configurada
        if (!string.IsNullOrWhiteSpace(signatureHeader) && !string.IsNullOrWhiteSpace(webhookSecret) && !string.IsNullOrWhiteSpace(rawPayload))
        {
            if (!gatewayProvider.VerifyWebhookSignature(rawPayload, signatureHeader, webhookSecret))
            {
                logger?.LogWarning("Assinatura do webhook inválida para o evento {EventId} ({EventType}).", payload.EventId, payload.EventType);
                return Result.Failure(new Error("webhook.signature.invalid", "Assinatura do webhook inválida.", ErrorType.Unauthorized));
            }
        }

        // Idempotência
        if (await processedEventsRepository.HasBeenProcessedAsync(payload.EventId, ct))
        {
            logger?.LogInformation("Webhook {EventId} já foi processado anteriormente. Ignorando com 200 OK.", payload.EventId);
            return Result.Success(); // Retorna 200 OK sem reprocessar
        }

        var normalizedEvent = payload.EventType.Trim().ToLowerInvariant();
        logger?.LogInformation("Processando webhook {EventId}: Tipo={EventType}, Tenant={TenantId}, SubId={SubId}, CustId={CustId}",
            payload.EventId, normalizedEvent, payload.TenantId, payload.GatewaySubscriptionId, payload.GatewayCustomerId);

        // Identifica se o evento requer mutação no ciclo de vida da assinatura
        var isActionableEvent = normalizedEvent switch
        {
            "checkout.session.completed" or
            "invoice.paid" or "invoice.payment_succeeded" or "payment.confirmed" or "subscription.renewed" or
            "customer.subscription.updated" or
            "invoice.overdue" or "invoice.payment_failed" or "payment.failed" or
            "subscription.canceled" or "customer.subscription.deleted" => true,
            _ => false
        };

        // Eventos não acionáveis (ex.: customer.created, payment_intent.created, setup_intent.created)
        // são reconhecidos com 200 OK sem disparar mutações desnecessárias
        if (!isActionableEvent)
        {
            logger?.LogInformation("Evento {EventType} ({EventId}) reconhecido e ignorado com sucesso (evento não acionável para o ciclo de vida da assinatura).",
                normalizedEvent, payload.EventId);

            await processedEventsRepository.AddAsync(new ProcessedSaasWebhookEvent(
                Guid.CreateVersion7(),
                payload.EventId,
                payload.EventType,
                payload.TenantId != Guid.Empty ? payload.TenantId : Guid.Empty,
                DateTimeOffset.UtcNow,
                rawPayload: rawPayload), ct);
            await processedEventsRepository.SaveChangesAsync(ct);

            return Result.Success();
        }

        // Resolução do Tenant e da Assinatura
        var tenantId = payload.TenantId;
        TenantSaasSubscription? subscription = null;

        if (tenantId != Guid.Empty)
        {
            subscription = await subscriptionRepository.GetByTenantIdAsync(tenantId, ct);
        }

        if (subscription is null && !string.IsNullOrWhiteSpace(payload.GatewaySubscriptionId))
        {
            subscription = await subscriptionRepository.GetByGatewaySubscriptionIdAsync(payload.GatewaySubscriptionId, ct);
            if (subscription is not null)
            {
                tenantId = subscription.TenantId;
            }
        }

        if (subscription is null && !string.IsNullOrWhiteSpace(payload.GatewayCustomerId))
        {
            subscription = await subscriptionRepository.GetByGatewayCustomerIdAsync(payload.GatewayCustomerId, ct);
            if (subscription is not null)
            {
                tenantId = subscription.TenantId;
            }
        }

        // Se o TenantId não pôde ser resolvido para um evento acionável, responde 200 OK e loga aviso
        if (tenantId == Guid.Empty)
        {
            logger?.LogWarning("Evento acionável {EventType} ({EventId}) recebido sem TenantId associado. Ignorando com 200 OK para evitar inconsistências.",
                normalizedEvent, payload.EventId);

            await processedEventsRepository.AddAsync(new ProcessedSaasWebhookEvent(
                Guid.CreateVersion7(),
                payload.EventId,
                payload.EventType,
                Guid.Empty,
                DateTimeOffset.UtcNow,
                rawPayload: rawPayload), ct);
            await processedEventsRepository.SaveChangesAsync(ct);

            return Result.Success();
        }

        // Garante que o contexto de tenant esteja ativo antes de gravar entidades IMustHaveTenant
        subscriptionRepository.SetTenantContext(tenantId);

        if (subscription is null)
        {
            if (normalizedEvent == "checkout.session.completed")
            {
                var initialTier = payload.PlanTier ?? SaasPlanTier.Pro;
                var trialResult = TenantSaasSubscription.CreateTrial(tenantId, defaultTier: initialTier);
                if (trialResult.IsSuccess && trialResult.Value is not null)
                {
                    subscription = trialResult.Value;
                    await subscriptionRepository.AddAsync(subscription, ct);
                }
            }
            else
            {
                logger?.LogWarning("Assinatura não encontrada para o tenant {TenantId} no evento {EventType}. Evento registrado sem mutações.",
                    tenantId, normalizedEvent);

                await processedEventsRepository.AddAsync(new ProcessedSaasWebhookEvent(
                    Guid.CreateVersion7(),
                    payload.EventId,
                    payload.EventType,
                    tenantId,
                    DateTimeOffset.UtcNow,
                    rawPayload: rawPayload), ct);
                await processedEventsRepository.SaveChangesAsync(ct);

                return Result.Success();
            }
        }

        if (subscription is null)
        {
            logger?.LogWarning("Não foi possível instanciar a assinatura do tenant {TenantId}. Abortando processamento com sucesso.", tenantId);
            return Result.Success();
        }

        switch (normalizedEvent)
        {
            case "checkout.session.completed":
                {
                    var targetTier = payload.PlanTier ?? subscription.PlanTier;
                    var plan = await planRepository.GetByTierAsync(targetTier, ct)
                               ?? await planRepository.GetByTierAsync(subscription.PlanTier, ct);

                    if (plan is not null)
                    {
                        if (subscription.PlanTier != plan.Tier)
                        {
                            subscription.ChangePlan(plan);
                        }

                        if (!string.IsNullOrWhiteSpace(payload.GatewaySubscriptionId) || !string.IsNullOrWhiteSpace(payload.GatewayCustomerId))
                        {
                            subscription.Activate(plan, payload.GatewaySubscriptionId, payload.GatewayCustomerId);
                        }
                    }

                    var now = DateTimeOffset.UtcNow;
                    var nextCycle = now.AddMonths(1);
                    subscription.RenewCycle(now, nextCycle);
                    subscriptionRepository.Update(subscription);

                    var quotaUsage = await quotaUsageRepository.GetCurrentCycleByTenantIdAsync(tenantId, ct);
                    if (quotaUsage is not null)
                    {
                        quotaUsage.ResetForNewCycle(now, nextCycle);
                        quotaUsageRepository.Update(quotaUsage);
                    }

                    await globalTenantLookup.UpdateTenantStatusAsync(tenantId, new UpdateTenantStatusRequest(
                        TenantStatus.Active,
                        "Assinatura confirmada e ativada via Stripe Checkout."), ct);
                    break;
                }

            case "customer.subscription.updated":
                {
                    var targetTier = payload.PlanTier ?? subscription.PlanTier;
                    var plan = await planRepository.GetByTierAsync(targetTier, ct)
                               ?? await planRepository.GetByTierAsync(subscription.PlanTier, ct);

                    if (plan is not null)
                    {
                        if (subscription.PlanTier != plan.Tier)
                        {
                            subscription.ChangePlan(plan);
                        }

                        if (!string.IsNullOrWhiteSpace(payload.GatewaySubscriptionId) || !string.IsNullOrWhiteSpace(payload.GatewayCustomerId))
                        {
                            subscription.Activate(plan, payload.GatewaySubscriptionId, payload.GatewayCustomerId);
                        }
                    }

                    subscriptionRepository.Update(subscription);
                    break;
                }

            case "invoice.paid":
            case "invoice.payment_succeeded":
            case "payment.confirmed":
            case "subscription.renewed":
                {
                    var targetTier = payload.PlanTier ?? subscription.PlanTier;
                    var plan = await planRepository.GetByTierAsync(targetTier, ct)
                               ?? await planRepository.GetByTierAsync(subscription.PlanTier, ct);

                    if (plan is not null)
                    {
                        if (subscription.PlanTier != plan.Tier)
                        {
                            subscription.ChangePlan(plan);
                        }

                        if (!string.IsNullOrWhiteSpace(payload.GatewayCustomerId))
                        {
                            subscription.Activate(plan, payload.GatewaySubscriptionId, payload.GatewayCustomerId);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(payload.GatewayInvoiceId))
                    {
                        var invoice = await invoiceRepository.GetByGatewayInvoiceIdAsync(payload.GatewayInvoiceId, ct);
                        if (invoice is not null)
                        {
                            invoice.MarkPaid(payload.TimestampUtc);
                            invoiceRepository.Update(invoice);
                        }
                    }

                    var now = DateTimeOffset.UtcNow;
                    var nextCycle = now.AddMonths(1);
                    subscription.RenewCycle(now, nextCycle);
                    subscriptionRepository.Update(subscription);

                    var quotaUsage = await quotaUsageRepository.GetCurrentCycleByTenantIdAsync(tenantId, ct);
                    if (quotaUsage is not null)
                    {
                        quotaUsage.ResetForNewCycle(now, nextCycle);
                        quotaUsageRepository.Update(quotaUsage);
                    }

                    await globalTenantLookup.UpdateTenantStatusAsync(tenantId, new UpdateTenantStatusRequest(
                        TenantStatus.Active,
                        "Assinatura confirmada e ativada via webhook de pagamento."), ct);
                    break;
                }

            case "invoice.overdue":
            case "invoice.payment_failed":
            case "payment.failed":
                {
                    if (!string.IsNullOrWhiteSpace(payload.GatewayInvoiceId))
                    {
                        var invoice = await invoiceRepository.GetByGatewayInvoiceIdAsync(payload.GatewayInvoiceId, ct);
                        if (invoice is not null)
                        {
                            invoice.MarkOverdue();
                            invoiceRepository.Update(invoice);
                        }
                    }

                    var now = payload.TimestampUtc;
                    subscription.MarkOverdue(now, gracePeriodDays: 5, "Fatura em atraso notificada pelo provedor de pagamentos.");
                    subscriptionRepository.Update(subscription);

                    if (subscription.Status == TenantSubscriptionStatus.Delinquent)
                    {
                        await globalTenantLookup.UpdateTenantStatusAsync(tenantId, new UpdateTenantStatusRequest(
                            TenantStatus.Delinquent,
                            "Suspenso por inadimplência após vencimento do prazo de carência da assinatura."), ct);
                    }
                    break;
                }

            case "subscription.canceled":
            case "customer.subscription.deleted":
                {
                    subscription.Cancel("Assinatura cancelada via provedor de pagamentos.");
                    subscriptionRepository.Update(subscription);

                    await globalTenantLookup.UpdateTenantStatusAsync(tenantId, new UpdateTenantStatusRequest(
                        TenantStatus.Canceled,
                        "Assinatura cancelada no SaaS."), ct);
                    break;
                }
        }

        await processedEventsRepository.AddAsync(new ProcessedSaasWebhookEvent(
            Guid.CreateVersion7(),
            payload.EventId,
            payload.EventType,
            tenantId,
            DateTimeOffset.UtcNow,
            rawPayload: rawPayload), ct);

        await subscriptionRepository.SaveChangesAsync(ct);
        await quotaUsageRepository.SaveChangesAsync(ct);
        await invoiceRepository.SaveChangesAsync(ct);
        await processedEventsRepository.SaveChangesAsync(ct);

        logger?.LogInformation("Webhook {EventId} ({EventType}) concluído com sucesso para o tenant {TenantId}.",
            payload.EventId, normalizedEvent, tenantId);

        return Result.Success();
    }
}
