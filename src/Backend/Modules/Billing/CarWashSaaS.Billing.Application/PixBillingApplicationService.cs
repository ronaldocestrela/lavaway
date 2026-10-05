using System.Globalization;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.Billing.Application;

public sealed class PixBillingApplicationService(
    IPixChargeRepository pixChargeRepository,
    IPixGatewayProvider pixGatewayProvider,
    IWorkOrderPaymentLookup workOrderPaymentLookup,
    IOutboundWhatsAppDispatcher? whatsAppDispatcher = null,
    ILogger<PixBillingApplicationService>? logger = null) : IPixBillingLookup
{
    private static readonly CultureInfo PtBrCulture = new("pt-BR");

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

        var gatewayResult = await pixGatewayProvider.CreateImmediateChargeAsync(gatewayRequest, ct);
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

    Task<Result<WorkOrderPixChargeDto>> IPixBillingLookup.GetOrCreateWorkOrderPixChargeAsync(Guid tenantId, Guid workOrderId, CancellationToken ct) =>
        GetOrCreatePixChargeForWorkOrderAsync(tenantId, workOrderId, ct: ct);

    Task<Result<WorkOrderPixChargeDto>> IPixBillingLookup.SendPixChargeToWhatsAppAsync(Guid tenantId, Guid workOrderId, CancellationToken ct) =>
        SendPixChargeToCustomerWhatsAppAsync(tenantId, workOrderId, ct: ct);

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
