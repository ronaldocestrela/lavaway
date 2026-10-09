using System.Globalization;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.Billing.Application;

public sealed class PixBillingApplicationService(
    IPixChargeRepository pixChargeRepository,
    IPixGatewayProvider pixGatewayProvider,
    IWorkOrderPaymentLookup workOrderPaymentLookup,
    IProcessedPaymentWebhookRepository? processedWebhookRepository = null,
    IWorkOrderPaymentSettlementService? workOrderSettlementService = null,
    IOutboundWhatsAppDispatcher? whatsAppDispatcher = null,
    ICashTransactionRepository? cashTransactionRepository = null,
    ITenantPixGatewayResolver? gatewayResolver = null,
    ILogger<PixBillingApplicationService>? logger = null) : IPixBillingLookup
{
    private static readonly CultureInfo PtBrCulture = new("pt-BR");

    private async Task<IPixGatewayProvider> GetProviderForTenantAsync(Guid tenantId, CancellationToken ct)
    {
        if (gatewayResolver is not null)
        {
            return await gatewayResolver.ResolveForTenantAsync(tenantId, ct);
        }

        return pixGatewayProvider;
    }

    public async Task<Result<WorkOrderPixChargeDto>> GetOrCreatePixChargeForWorkOrderAsync(
        Guid tenantId,
        Guid workOrderId,
        int? expirationMinutes = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<WorkOrderPixChargeDto>.Failure(new Error("billing.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (workOrderId == Guid.Empty)
        {
            return Result<WorkOrderPixChargeDto>.Failure(new Error("billing.work_order_required", "Ordem de serviço é obrigatória.", ErrorType.Validation));
        }

        var workOrderSummaryResult = await workOrderPaymentLookup.GetPaymentSummaryAsync(tenantId, workOrderId, ct);
        if (!workOrderSummaryResult.IsSuccess || workOrderSummaryResult.Value is null)
        {
            return Result<WorkOrderPixChargeDto>.Failure(
                workOrderSummaryResult.Error ?? new Error("billing.work_order_not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        var workOrder = workOrderSummaryResult.Value;
        if (string.Equals(workOrder.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return Result<WorkOrderPixChargeDto>.Failure(new Error("billing.work_order_cancelled", "Não é possível gerar cobrança Pix para ordem de serviço cancelada.", ErrorType.Conflict));
        }

        var existingCharge = await pixChargeRepository.GetActiveByWorkOrderIdAsync(tenantId, workOrderId, ct);
        if (existingCharge is not null)
        {
            if (existingCharge.IsActiveAndPending())
            {
                if (existingCharge.Amount == workOrder.TotalAmount)
                {
                    return Result<WorkOrderPixChargeDto>.Success(MapToDto(existingCharge));
                }

                // Valor foi modificado: cancela anterior para emitir nova cobrança atualizada
                existingCharge.Cancel();
                pixChargeRepository.Update(existingCharge);
                await pixChargeRepository.SaveChangesAsync(ct);
            }
            else if (existingCharge.Status == PixChargeStatusConstants.Pending && existingCharge.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                existingCharge.Expire();
                pixChargeRepository.Update(existingCharge);
                await pixChargeRepository.SaveChangesAsync(ct);
            }
        }

        var minutes = Math.Clamp(expirationMinutes ?? 30, 5, 1440);
        var expiration = TimeSpan.FromMinutes(minutes);

        var gatewayRequest = new PixGatewayChargeRequest(
            tenantId,
            workOrderId,
            workOrder.TotalAmount,
            $"Pagamento Lavaway OS #{workOrderId.ToString()[..8].ToUpperInvariant()} - Placa {workOrder.Plate}",
            workOrder.CustomerName,
            workOrder.CustomerPhone,
            expiration);

        var provider = await GetProviderForTenantAsync(tenantId, ct);
        var gatewayResult = await provider.CreateImmediateChargeAsync(gatewayRequest, ct);
        if (!gatewayResult.IsSuccess || gatewayResult.Value is null)
        {
            return Result<WorkOrderPixChargeDto>.Failure(
                gatewayResult.Error ?? new Error("billing.gateway_error", "Falha ao gerar cobrança no provedor Pix.", ErrorType.Validation));
        }

        var gatewayResponse = gatewayResult.Value;
        var chargeResult = PixCharge.Create(
            tenantId,
            workOrderId,
            workOrder.TotalAmount,
            gatewayResponse.TxId,
            gatewayResponse.QrCodeBase64,
            gatewayResponse.CopyPasteKey,
            gatewayResponse.ExpiresAtUtc);

        if (!chargeResult.IsSuccess || chargeResult.Value is null)
        {
            return Result<WorkOrderPixChargeDto>.Failure(chargeResult.Error!);
        }

        var charge = chargeResult.Value;
        await pixChargeRepository.AddAsync(charge, ct);
        await pixChargeRepository.SaveChangesAsync(ct);

        logger?.LogInformation(
            "Pix charge {ChargeId} created for work order {WorkOrderId} in tenant {TenantId}, amount {Amount}",
            charge.Id, workOrderId, tenantId, charge.Amount);

        return Result<WorkOrderPixChargeDto>.Success(MapToDto(charge));
    }

    public async Task<Result<WorkOrderPixChargeDto>> GetPixChargeByWorkOrderIdAsync(
        Guid tenantId,
        Guid workOrderId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<WorkOrderPixChargeDto>.Failure(new Error("billing.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (workOrderId == Guid.Empty)
        {
            return Result<WorkOrderPixChargeDto>.Failure(new Error("billing.work_order_required", "Ordem de serviço é obrigatória.", ErrorType.Validation));
        }

        var charge = await pixChargeRepository.GetLatestByWorkOrderIdAsync(tenantId, workOrderId, ct);
        if (charge is null)
        {
            return Result<WorkOrderPixChargeDto>.Failure(new Error("billing.charge_not_found", "Cobrança Pix não encontrada para esta ordem de serviço.", ErrorType.NotFound));
        }

        if (charge.Status == PixChargeStatusConstants.Pending && charge.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            charge.Expire();
            pixChargeRepository.Update(charge);
            await pixChargeRepository.SaveChangesAsync(ct);
        }

        return Result<WorkOrderPixChargeDto>.Success(MapToDto(charge));
    }

    public async Task<Result<WorkOrderPixChargeDto>> SendPixChargeToCustomerWhatsAppAsync(
        Guid tenantId,
        Guid workOrderId,
        string? customMessage = null,
        CancellationToken ct = default)
    {
        if (whatsAppDispatcher is null)
        {
            return Result<WorkOrderPixChargeDto>.Failure(new Error("whatsapp.dispatcher_not_configured", "Serviço de WhatsApp não configurado.", ErrorType.Conflict));
        }

        var chargeResult = await GetOrCreatePixChargeForWorkOrderAsync(tenantId, workOrderId, ct: ct);
        if (!chargeResult.IsSuccess || chargeResult.Value is null)
        {
            return chargeResult;
        }

        var charge = chargeResult.Value;
        var summaryResult = await workOrderPaymentLookup.GetPaymentSummaryAsync(tenantId, workOrderId, ct);
        if (!summaryResult.IsSuccess || summaryResult.Value is null)
        {
            return Result<WorkOrderPixChargeDto>.Failure(summaryResult.Error ?? new Error("billing.work_order_not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        var summary = summaryResult.Value;
        if (string.IsNullOrWhiteSpace(summary.CustomerPhone))
        {
            return Result<WorkOrderPixChargeDto>.Failure(new Error("billing.customer_phone_missing", "Cliente não possui telefone cadastrado para envio via WhatsApp.", ErrorType.Validation));
        }

        var formattedAmount = charge.Amount.ToString("C2", PtBrCulture);
        var expirationLocal = charge.ExpiresAtUtc.ToOffset(TimeSpan.FromHours(-3)).ToString("HH:mm", PtBrCulture);

        var messageText =
            $"Olá, {summary.CustomerName}! Seguem os dados para pagamento Pix da sua Ordem de Serviço do veículo *{summary.Plate}*:\n\n" +
            $"💰 *Valor a pagar:* {formattedAmount}\n\n" +
            $"*Código Pix Copia e Cola:*\n```{charge.CopyPasteKey}```\n\n" +
            $"⏳ *Validade:* até {expirationLocal} (horário de Brasília).\n\n" +
            (string.IsNullOrWhiteSpace(customMessage) ? "Assim que o pagamento for concluído, nosso sistema identificará automaticamente!" : customMessage.Trim());

        var idempotencyKey = $"pix-{charge.Id}-{charge.WhatsAppSentAtUtc?.Ticks ?? 0}";
        var dispatchResult = await whatsAppDispatcher.DispatchTextMessageAsync(
            tenantId,
            summary.CustomerPhone,
            messageText,
            idempotencyKey,
            ct);

        if (!dispatchResult.IsSuccess)
        {
            logger?.LogWarning("Failed to dispatch Pix charge {ChargeId} via WhatsApp: {Error}", charge.Id, dispatchResult.Error?.Description);
            return Result<WorkOrderPixChargeDto>.Failure(
                dispatchResult.Error ?? new Error("whatsapp.dispatch_failed", "Falha ao enviar mensagem de cobrança via WhatsApp.", ErrorType.Validation));
        }

        var entity = await pixChargeRepository.GetByIdAsync(tenantId, charge.Id, ct);
        if (entity is not null)
        {
            entity.MarkAsSentToWhatsApp(DateTimeOffset.UtcNow);
            pixChargeRepository.Update(entity);
            await pixChargeRepository.SaveChangesAsync(ct);
            return Result<WorkOrderPixChargeDto>.Success(MapToDto(entity));
        }

        return Result<WorkOrderPixChargeDto>.Success(charge);
    }

    public async Task<Result<PaymentWebhookProcessingResultDto>> ProcessPaymentWebhookAsync(
        Guid tenantId,
        PaymentWebhookPayloadDto payload,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<PaymentWebhookProcessingResultDto>.Failure(new Error("billing.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (payload is null)
        {
            return Result<PaymentWebhookProcessingResultDto>.Failure(new Error("billing.webhook_payload_required", "Payload do webhook é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(payload.Provider))
        {
            return Result<PaymentWebhookProcessingResultDto>.Failure(new Error("billing.webhook_provider_required", "Provedor é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(payload.EventId))
        {
            return Result<PaymentWebhookProcessingResultDto>.Failure(new Error("billing.webhook_event_id_required", "Identificador do evento é obrigatório.", ErrorType.Validation));
        }

        // 1. Idempotência: verifica se o evento já foi processado
        if (processedWebhookRepository is not null)
        {
            var alreadyProcessed = await processedWebhookRepository.HasBeenProcessedAsync(tenantId, payload.Provider, payload.EventId, ct);
            if (alreadyProcessed)
            {
                logger?.LogInformation(
                    "Webhook {EventId} from provider {Provider} already processed for tenant {TenantId}. Skipping idempotently.",
                    payload.EventId, payload.Provider, tenantId);

                return Result<PaymentWebhookProcessingResultDto>.Success(new PaymentWebhookProcessingResultDto(
                    Processed: true,
                    Message: "Evento já processado anteriormente (idempotência).",
                    TxId: payload.TxId));
            }
        }

        // 2. Determinar status e dados do pagamento (consulta no gateway caso necessário)
        var isApproved = string.Equals(payload.Status, "approved", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(payload.Status, "paid", StringComparison.OrdinalIgnoreCase);

        DateTimeOffset paidAtUtc = payload.OccurredAtUtc ?? DateTimeOffset.UtcNow;
        string? matchedTxId = payload.TxId;
        var paymentReference = !string.IsNullOrWhiteSpace(payload.TxId) ? payload.TxId : payload.PaymentId;

        if (!string.IsNullOrWhiteSpace(payload.PaymentId) && (!isApproved || string.IsNullOrWhiteSpace(matchedTxId)))
        {
            var provider = await GetProviderForTenantAsync(tenantId, ct);
            var queryResult = await provider.GetPaymentDetailsAsync(tenantId, payload.PaymentId, ct);
            if (queryResult.IsSuccess && queryResult.Value is not null)
            {
                var details = queryResult.Value;
                isApproved = string.Equals(details.Status, "approved", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(details.Status, "paid", StringComparison.OrdinalIgnoreCase);

                if (details.PaidAtUtc.HasValue)
                {
                    paidAtUtc = details.PaidAtUtc.Value;
                }

                if (!string.IsNullOrWhiteSpace(details.TxId))
                {
                    matchedTxId = details.TxId;
                }
            }
        }

        if (!isApproved)
        {
            logger?.LogInformation(
                "Webhook {EventId} from {Provider} with status {Status} ignored (not approved/paid).",
                payload.EventId, payload.Provider, payload.Status);

            if (processedWebhookRepository is not null)
            {
                var ignoredRecord = ProcessedPaymentWebhook.Create(
                    tenantId,
                    payload.Provider,
                    payload.EventId,
                    matchedTxId ?? paymentReference ?? string.Empty,
                    "Ignored",
                    notes: $"Status não aprovado ({payload.Status}).");

                if (ignoredRecord.IsSuccess)
                {
                    await processedWebhookRepository.AddAsync(ignoredRecord.Value!, ct);
                    await processedWebhookRepository.SaveChangesAsync(ct);
                }
            }

            return Result<PaymentWebhookProcessingResultDto>.Success(new PaymentWebhookProcessingResultDto(
                Processed: true,
                Message: $"Evento com status '{payload.Status}' recebido e registrado como não aprovado.",
                TxId: matchedTxId ?? paymentReference));
        }

        // 3. Localizar PixCharge correspondente
        PixCharge? charge = null;
        if (!string.IsNullOrWhiteSpace(matchedTxId))
        {
            charge = await pixChargeRepository.GetByTxIdAsync(tenantId, matchedTxId, ct);
        }

        if (charge is null && !string.IsNullOrWhiteSpace(paymentReference))
        {
            charge = await pixChargeRepository.GetByTxIdAsync(tenantId, paymentReference, ct);
        }

        if (charge is null && !string.IsNullOrWhiteSpace(paymentReference) && Guid.TryParse(paymentReference, out var possibleWorkOrderId))
        {
            charge = await pixChargeRepository.GetActiveByWorkOrderIdAsync(tenantId, possibleWorkOrderId, ct);
        }

        if (charge is null)
        {
            logger?.LogWarning("Pix charge not found for webhook {EventId}, ref {PaymentReference}", payload.EventId, paymentReference);
            return Result<PaymentWebhookProcessingResultDto>.Failure(new Error(
                "billing.charge_not_found",
                $"Cobrança Pix não encontrada para o pagamento '{paymentReference}'.",
                ErrorType.NotFound));
        }

        // 4. Atualizar PixCharge para Paid (idempotente)
        charge.MarkAsPaid(paidAtUtc);
        pixChargeRepository.Update(charge);
        await pixChargeRepository.SaveChangesAsync(ct);

        // 5. Baixar e conciliar a OS no módulo YardOperations via interface pública
        bool settled = false;
        if (workOrderSettlementService is not null)
        {
            var settleResult = await workOrderSettlementService.SettlePaymentAsync(
                tenantId,
                charge.WorkOrderId,
                charge.Amount,
                "Pix",
                charge.TxId,
                paidAtUtc,
                ct);

            settled = settleResult.IsSuccess;
            if (!settled)
            {
                logger?.LogWarning("Failed to settle work order {WorkOrderId} for Pix charge {ChargeId}: {Error}",
                    charge.WorkOrderId, charge.Id, settleResult.Error?.Description);
            }
        }

        // 5.1. Registrar transação financeira de receita no caixa
        if (cashTransactionRepository is not null)
        {
            var exists = await cashTransactionRepository.ExistsForWorkOrderAsync(tenantId, charge.WorkOrderId, CashTransactionType.Income, ct);
            if (!exists)
            {
                var txResult = CashTransaction.CreateIncome(
                    tenantId,
                    charge.Amount,
                    PaymentMethodConstants.Pix,
                    $"Baixa OS #{charge.WorkOrderId.ToString()[..8]} - Pix (TxId: {charge.TxId})",
                    paidAtUtc,
                    charge.WorkOrderId,
                    externalReference: charge.TxId);

                if (txResult.IsSuccess)
                {
                    await cashTransactionRepository.AddAsync(txResult.Value!, ct);
                    await cashTransactionRepository.SaveChangesAsync(ct);
                }
            }
        }

        // 6. Gravar auditoria de webhook processado
        if (processedWebhookRepository is not null)
        {
            var processedRecord = ProcessedPaymentWebhook.Create(
                tenantId,
                payload.Provider,
                payload.EventId,
                charge.TxId,
                "Processed",
                payload.RawPayload != null ? (payload.RawPayload.Length > 120 ? payload.RawPayload[..120] : payload.RawPayload) : null,
                $"Pagamento Pix conciliado com sucesso para OS {charge.WorkOrderId}.");

            if (processedRecord.IsSuccess)
            {
                await processedWebhookRepository.AddAsync(processedRecord.Value!, ct);
                await processedWebhookRepository.SaveChangesAsync(ct);
            }
        }

        // 7. Notificar cliente no WhatsApp (se configurado)
        if (whatsAppDispatcher is not null)
        {
            try
            {
                var summaryResult = await workOrderPaymentLookup.GetPaymentSummaryAsync(tenantId, charge.WorkOrderId, ct);
                if (summaryResult.IsSuccess && summaryResult.Value is not null && !string.IsNullOrWhiteSpace(summaryResult.Value.CustomerPhone))
                {
                    var summary = summaryResult.Value;
                    var confirmationMsg =
                        $"🎉 *Pagamento Pix Confirmado!*\n\n" +
                        $"Olá, {summary.CustomerName}! Recebemos com sucesso o pagamento de {charge.Amount.ToString("C2", PtBrCulture)} referente à sua Ordem de Serviço do veículo *{summary.Plate}*.\n\n" +
                        $"Agradecemos a sua preferência!";

                    var idempotencyKey = $"pix-confirmed-{charge.Id}";
                    await whatsAppDispatcher.DispatchTextMessageAsync(tenantId, summary.CustomerPhone, confirmationMsg, idempotencyKey, ct);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to dispatch WhatsApp payment confirmation for charge {ChargeId}", charge.Id);
            }
        }

        logger?.LogInformation(
            "Pix charge {ChargeId} and WorkOrder {WorkOrderId} settled via webhook {EventId} for tenant {TenantId}",
            charge.Id, charge.WorkOrderId, payload.EventId, tenantId);

        return Result<PaymentWebhookProcessingResultDto>.Success(new PaymentWebhookProcessingResultDto(
            Processed: true,
            Message: "Pagamento Pix confirmado e OS baixada com sucesso.",
            TxId: charge.TxId,
            WorkOrderId: charge.WorkOrderId,
            ChargeStatus: charge.Status,
            OrderSettled: settled));
    }

    Task<Result<WorkOrderPixChargeDto>> IPixBillingLookup.GetOrCreateWorkOrderPixChargeAsync(Guid tenantId, Guid workOrderId, CancellationToken ct) =>
        GetOrCreatePixChargeForWorkOrderAsync(tenantId, workOrderId, ct: ct);

    Task<Result<WorkOrderPixChargeDto>> IPixBillingLookup.SendPixChargeToWhatsAppAsync(Guid tenantId, Guid workOrderId, CancellationToken ct) =>
        SendPixChargeToCustomerWhatsAppAsync(tenantId, workOrderId, ct: ct);

    Task<Result<PaymentWebhookProcessingResultDto>> IPixBillingLookup.ProcessPaymentWebhookAsync(Guid tenantId, PaymentWebhookPayloadDto payload, CancellationToken ct) =>
        ProcessPaymentWebhookAsync(tenantId, payload, ct);

    private static WorkOrderPixChargeDto MapToDto(PixCharge entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.WorkOrderId,
            entity.Amount,
            entity.Status,
            entity.TxId,
            entity.QrCodeBase64,
            entity.CopyPasteKey,
            entity.ExpiresAtUtc,
            entity.CreatedAtUtc,
            entity.WhatsAppSentAtUtc,
            entity.PaidAtUtc);
}
