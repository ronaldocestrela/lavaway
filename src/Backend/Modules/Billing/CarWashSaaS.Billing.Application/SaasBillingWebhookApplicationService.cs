using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Application;

public sealed class SaasBillingWebhookApplicationService(
    ISaasBillingGatewayProvider gatewayProvider,
    IProcessedSaasWebhookEventRepository processedEventsRepository,
    ITenantSaasSubscriptionRepository subscriptionRepository,
    ITenantQuotaUsageRepository quotaUsageRepository,
    ISaasInvoiceRepository invoiceRepository,
    ISaasPlanRepository planRepository,
    IGlobalTenantLookup globalTenantLookup)
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
            return Result.Failure(new Error("webhook.payload.required", "Payload do webhook é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(payload.EventId))
        {
            return Result.Failure(new Error("webhook.event_id.required", "EventId do webhook é obrigatório.", ErrorType.Validation));
        }

        // Validação de assinatura se fornecida ou configurada
        if (!string.IsNullOrWhiteSpace(signatureHeader) && !string.IsNullOrWhiteSpace(webhookSecret) && !string.IsNullOrWhiteSpace(rawPayload))
        {
            if (!gatewayProvider.VerifyWebhookSignature(rawPayload, signatureHeader, webhookSecret))
            {
                return Result.Failure(new Error("webhook.signature.invalid", "Assinatura do webhook inválida.", ErrorType.Unauthorized));
            }
        }

        // Idempotência
        if (await processedEventsRepository.HasBeenProcessedAsync(payload.EventId, ct))
        {
            return Result.Success(); // Retorna 200 OK sem reprocessar
        }

        var tenantId = payload.TenantId;
        var subscription = await subscriptionRepository.GetByTenantIdAsync(tenantId, ct);
        if (subscription is null)
        {
            // Cria trial inicial caso ainda não exista registro
            subscription = TenantSaasSubscription.CreateTrial(tenantId).Value!;
            await subscriptionRepository.AddAsync(subscription, ct);
        }

        var normalizedEvent = payload.EventType.Trim().ToLowerInvariant();

        switch (normalizedEvent)
        {
            case "invoice.paid":
            case "payment.confirmed":
            case "subscription.renewed":
                {
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
            DateTimeOffset.UtcNow), ct);

        await subscriptionRepository.SaveChangesAsync(ct);
        await quotaUsageRepository.SaveChangesAsync(ct);
        await invoiceRepository.SaveChangesAsync(ct);
        await processedEventsRepository.SaveChangesAsync(ct);

        return Result.Success();
    }
}
