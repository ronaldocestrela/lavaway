using System.Globalization;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed class WorkOrderNotificationApplicationService(
    IWorkOrderRepository workOrderRepository,
    ICustomerRepository customerRepository,
    IVehicleRepository vehicleRepository,
    IVehicleInspectionRepository inspectionRepository,
    IWorkOrderReceiptPdfGenerator pdfGenerator,
    ITenantObjectStorage objectStorage,
    IOutboundWhatsAppDispatcher whatsAppDispatcher,
    ITenantStoreProfileLookup storeProfileLookup)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public async Task<Result<WhatsAppMessageDto>> SendReceiptNotificationAsync(
        Guid tenantId,
        Guid workOrderId,
        string? customMessage = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || workOrderId == Guid.Empty)
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("notification.work_order_required", "Tenant e ordem de serviço são obrigatórios.", ErrorType.Validation));
        }

        var workOrder = await workOrderRepository.GetByIdAsync(tenantId, workOrderId, ct);
        if (workOrder is null)
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("work_order.not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, workOrder.CustomerId, ct);
        if (customer is null || string.IsNullOrWhiteSpace(customer.Phone))
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("customer.phone_required", "O cliente não possui telefone de contato cadastrado.", ErrorType.Validation));
        }

        var vehicle = await vehicleRepository.GetByIdAsync(tenantId, workOrder.VehicleId, ct);
        var profileResult = await storeProfileLookup.GetProfileAsync(tenantId, ct);
        var storeProfile = profileResult.IsSuccess ? profileResult.Value : null;
        var inspection = await inspectionRepository.GetByWorkOrderIdAsync(tenantId, workOrderId, ct);

        // 1. Gerar PDF
        var pdfModel = BuildPdfModel(tenantId, workOrder, customer, vehicle, storeProfile, inspection);
        var pdfResult = pdfGenerator.GenerateReceiptPdf(pdfModel);
        if (!pdfResult.IsSuccess)
        {
            return Result<WhatsAppMessageDto>.Failure(pdfResult.Error!);
        }

        var pdfBytes = pdfResult.Value!;
        var fileName = $"comprovante-OS-{workOrder.Id.ToString("D")[..8].ToUpperInvariant()}.pdf";

        // 2. Persistir no Object Storage
        using var stream = new MemoryStream(pdfBytes);
        await objectStorage.PutAsync(tenantId, "work-orders", $"{workOrder.Id:N}/receipt.pdf", stream, "application/pdf", ct);

        // 3. Montar Legenda
        var shortOs = workOrder.Id.ToString("D")[..8].ToUpperInvariant();
        var tradeName = storeProfile?.TradeName ?? "Lava-Jato";
        var plate = vehicle?.Plate ?? "";
        var forecast = workOrder.EstimatedCompletionAtUtc.ToLocalTime().ToString("dd/MM HH:mm", PtBr);
        var totalStr = workOrder.TotalAmount.ToString("C2", PtBr);

        var caption = !string.IsNullOrWhiteSpace(customMessage)
            ? customMessage.Trim()
            : $"🚗 *Comprovante de Entrada — {tradeName}*\n" +
              $"Olá, *{customer.Name}*!\n" +
              $"Recebemos seu veículo (Placa: *{plate}*).\n" +
              $"OS: *#{shortOs}*\n" +
              $"Previsão de conclusão: *{forecast}*\n" +
              $"Valor total: *{totalStr}*\n\n" +
              $"Segue anexo o comprovante digital com o checklist da vistoria.";

        // 4. Despachar via WhatsApp com Idempotência
        var base64Data = $"data:application/pdf;base64,{Convert.ToBase64String(pdfBytes)}";
        var idempotencyKey = $"receipt-{workOrder.Id:N}";

        return await whatsAppDispatcher.DispatchMediaMessageAsync(
            tenantId,
            customer.Phone,
            caption,
            "document",
            base64Data,
            "application/pdf",
            fileName,
            idempotencyKey,
            ct);
    }

    public async Task<Result<WhatsAppMessageDto>> SendReadyForPickupNotificationAsync(
        Guid tenantId,
        Guid workOrderId,
        string? customMessage = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || workOrderId == Guid.Empty)
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("notification.work_order_required", "Tenant e ordem de serviço são obrigatórios.", ErrorType.Validation));
        }

        var workOrder = await workOrderRepository.GetByIdAsync(tenantId, workOrderId, ct);
        if (workOrder is null)
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("work_order.not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, workOrder.CustomerId, ct);
        if (customer is null || string.IsNullOrWhiteSpace(customer.Phone))
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("customer.phone_required", "O cliente não possui telefone cadastrado.", ErrorType.Validation));
        }

        var vehicle = await vehicleRepository.GetByIdAsync(tenantId, workOrder.VehicleId, ct);
        var profileResult = await storeProfileLookup.GetProfileAsync(tenantId, ct);
        var storeProfile = profileResult.IsSuccess ? profileResult.Value : null;

        var tradeName = storeProfile?.TradeName ?? "Lava-Jato";
        var plate = vehicle?.Plate ?? "";
        var sizeDesc = vehicle is not null ? $"({vehicle.Size})" : "";
        var totalStr = workOrder.TotalAmount.ToString("C2", PtBr);
        var address = !string.IsNullOrWhiteSpace(storeProfile?.Street)
            ? $"{storeProfile.Street}, {storeProfile.City}"
            : "em nosso estabelecimento";

        var messageText = !string.IsNullOrWhiteSpace(customMessage)
            ? customMessage.Trim()
            : $"✨ *Seu veículo está pronto para retirada!*\n\n" +
              $"Olá, *{customer.Name}*!\n" +
              $"O serviço no seu veículo {sizeDesc} (Placa *{plate}*) foi finalizado com sucesso no *{tradeName}*.\n\n" +
              $"💰 *Valor total:* {totalStr}\n" +
              $"📍 *Local para retirada:* {address}\n\n" +
              $"Agradecemos a confiança e preferência! Nos vemos em breve.";


        var idempotencyKey = $"ready-{workOrder.Id:N}";

        return await whatsAppDispatcher.DispatchTextMessageAsync(
            tenantId,
            customer.Phone,
            messageText,
            idempotencyKey,
            ct);
    }

    public async Task<Result<WhatsAppMessageDto>> SendComparisonPhotosNotificationAsync(
        Guid tenantId,
        Guid workOrderId,
        IReadOnlyList<Guid>? selectedPhotoIds = null,
        string? customMessage = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || workOrderId == Guid.Empty)
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("notification.work_order_required", "Tenant e ordem de serviço são obrigatórios.", ErrorType.Validation));
        }

        var workOrder = await workOrderRepository.GetByIdAsync(tenantId, workOrderId, ct);
        if (workOrder is null)
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("work_order.not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        if (!workOrder.IsEligibleForPostServicePhotos())
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("work_order.not_eligible_for_photos", "A ordem de serviço não possui serviços elegíveis para fotos pós-serviço.", ErrorType.Validation));
        }

        var photos = workOrder.PostServicePhotos.ToList();
        if (selectedPhotoIds is not null && selectedPhotoIds.Count > 0)
        {
            photos = photos.Where(p => selectedPhotoIds.Contains(p.Id)).ToList();
        }

        if (photos.Count == 0)
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("work_order.no_photos", "Nenhuma foto pós-serviço encontrada para envio.", ErrorType.Validation));
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, workOrder.CustomerId, ct);
        if (customer is null || string.IsNullOrWhiteSpace(customer.Phone))
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("customer.phone_required", "O cliente não possui telefone cadastrado.", ErrorType.Validation));
        }

        var vehicle = await vehicleRepository.GetByIdAsync(tenantId, workOrder.VehicleId, ct);
        var profileResult = await storeProfileLookup.GetProfileAsync(tenantId, ct);
        var storeProfile = profileResult.IsSuccess ? profileResult.Value : null;

        var primaryPhoto = photos.First();
        var stored = await objectStorage.GetAsync(tenantId, "post-service-photos", primaryPhoto.FileName, ct);
        if (stored is null)
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("photo.storage_not_found", "O arquivo da foto não foi encontrado no armazenamento.", ErrorType.NotFound));
        }

        using var ms = new MemoryStream();
        await stored.Content.CopyToAsync(ms, ct);
        var photoBytes = ms.ToArray();
        var base64Data = $"data:{stored.ContentType};base64,{Convert.ToBase64String(photoBytes)}";

        var tradeName = storeProfile?.TradeName ?? "Lava-Jato";
        var plate = vehicle?.Plate ?? "";
        var sizeDesc = vehicle is not null ? $"({vehicle.Size})" : "";

        var caption = !string.IsNullOrWhiteSpace(customMessage)
            ? customMessage.Trim()
            : $"📸 *Resultado do Serviço — Antes & Depois*\n" +
              $"Olá, *{customer.Name}*! Confira o resultado do serviço executado no seu veículo {sizeDesc} ({plate}) no *{tradeName}*! ✨\n\n" +
              $"{primaryPhoto.Title}: {primaryPhoto.Notes ?? "Serviço finalizado com máxima qualidade."}";


        var idempotencyKey = $"comparison-{workOrder.Id:N}-{primaryPhoto.Id:N}";

        return await whatsAppDispatcher.DispatchMediaMessageAsync(
            tenantId,
            customer.Phone,
            caption,
            "image",
            base64Data,
            stored.ContentType,
            primaryPhoto.FileName,
            idempotencyKey,
            ct);
    }

    public async Task<Result<StoredObject>> GetReceiptPdfStreamAsync(
        Guid tenantId,
        Guid workOrderId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || workOrderId == Guid.Empty)
        {
            return Result<StoredObject>.Failure(new Error("receipt.id_required", "Tenant e ordem de serviço são obrigatórios.", ErrorType.Validation));
        }

        // Tentar obter do storage
        var existing = await objectStorage.GetAsync(tenantId, "work-orders", $"{workOrderId:N}/receipt.pdf", ct);
        if (existing is not null)
        {
            return Result<StoredObject>.Success(existing);
        }

        // Se ainda não gerado, gerar agora
        var workOrder = await workOrderRepository.GetByIdAsync(tenantId, workOrderId, ct);
        if (workOrder is null)
        {
            return Result<StoredObject>.Failure(new Error("work_order.not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, workOrder.CustomerId, ct);
        var vehicle = await vehicleRepository.GetByIdAsync(tenantId, workOrder.VehicleId, ct);
        var profileResult = await storeProfileLookup.GetProfileAsync(tenantId, ct);
        var storeProfile = profileResult.IsSuccess ? profileResult.Value : null;
        var inspection = await inspectionRepository.GetByWorkOrderIdAsync(tenantId, workOrderId, ct);

        var pdfModel = BuildPdfModel(tenantId, workOrder, customer, vehicle, storeProfile, inspection);
        var pdfResult = pdfGenerator.GenerateReceiptPdf(pdfModel);
        if (!pdfResult.IsSuccess)
        {
            return Result<StoredObject>.Failure(pdfResult.Error!);
        }

        var pdfBytes = pdfResult.Value!;
        var ms = new MemoryStream(pdfBytes);
        await objectStorage.PutAsync(tenantId, "work-orders", $"{workOrderId:N}/receipt.pdf", ms, "application/pdf", ct);

        ms.Position = 0;
        return Result<StoredObject>.Success(new StoredObject(ms, "application/pdf"));
    }

    public async Task<Result<WorkOrderNotificationSummaryDto>> GetNotificationSummaryAsync(
        Guid tenantId,
        Guid workOrderId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || workOrderId == Guid.Empty)
        {
            return Result<WorkOrderNotificationSummaryDto>.Failure(new Error("notification.work_order_required", "Tenant e ordem de serviço são obrigatórios.", ErrorType.Validation));
        }

        var messagesResult = await whatsAppDispatcher.GetRecentMessagesAsync(tenantId, 100, ct);
        var allMessages = messagesResult.IsSuccess ? messagesResult.Value! : [];

        var receiptKey = $"receipt-{workOrderId:N}";
        var readyKey = $"ready-{workOrderId:N}";
        var comparisonPrefix = $"comparison-{workOrderId:N}";

        // As mensagens recentes podem ter o texto contendo a OS ou IdempotencyKey
        var shortOs = workOrderId.ToString("D")[..8].ToUpperInvariant();

        var relatedMessages = allMessages
            .Where(m => m.MessageText.Contains(shortOs, StringComparison.OrdinalIgnoreCase) ||
                        m.MessageText.Contains(workOrderId.ToString(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        var receiptMsg = relatedMessages.FirstOrDefault(m => m.MediaType == "document" || m.MessageText.Contains("Comprovante", StringComparison.OrdinalIgnoreCase));
        var readyMsg = relatedMessages.FirstOrDefault(m => m.MessageText.Contains("pronto para retirada", StringComparison.OrdinalIgnoreCase));
        var comparisonMsg = relatedMessages.FirstOrDefault(m => m.MediaType == "image" || m.MessageText.Contains("Antes & Depois", StringComparison.OrdinalIgnoreCase));

        return Result<WorkOrderNotificationSummaryDto>.Success(new WorkOrderNotificationSummaryDto(
            WorkOrderId: workOrderId,
            ReceiptSent: receiptMsg is not null,
            ReceiptSentAtUtc: receiptMsg?.SentAtUtc,
            ReceiptStatus: receiptMsg?.Status,
            ReadyNoticeSent: readyMsg is not null,
            ReadyNoticeSentAtUtc: readyMsg?.SentAtUtc,
            ReadyNoticeStatus: readyMsg?.Status,
            ComparisonPhotosSent: comparisonMsg is not null,
            ComparisonPhotosSentAtUtc: comparisonMsg?.SentAtUtc,
            ComparisonPhotosStatus: comparisonMsg?.Status,
            RecentMessages: relatedMessages));
    }

    private static WorkOrderReceiptPdfModel BuildPdfModel(
        Guid tenantId,
        WorkOrder workOrder,
        Customer? customer,
        Vehicle? vehicle,
        StoreProfileDto? storeProfile,
        VehicleInspection? inspection)
    {
        var items = workOrder.Items.Select(i => new WorkOrderReceiptItemModel(
            i.ServiceName,
            i.Quantity,
            i.UnitPrice,
            i.TotalAmount)).ToList();

        WorkOrderReceiptInspectionModel? inspectionModel = null;
        if (inspection is not null)
        {
            var checklist = inspection.ChecklistItems.Select(c => new WorkOrderReceiptChecklistItemModel(
                c.Title,
                c.Status.ToString(),
                c.Observation)).ToList();

            var damages = inspection.Damages.Select(d => new WorkOrderReceiptDamageModel(
                d.Type.ToString(),
                d.View.ToString(),
                d.Severity.ToString(),
                d.Description)).ToList();

            inspectionModel = new WorkOrderReceiptInspectionModel(
                inspection.OdometerKm,
                inspection.FuelLevel,
                checklist,
                damages);
        }

        var storeAddress = storeProfile is not null && !string.IsNullOrWhiteSpace(storeProfile.Street)
            ? $"{storeProfile.Street}, {storeProfile.City} - {storeProfile.State}"
            : null;

        return new WorkOrderReceiptPdfModel(
            tenantId,
            workOrder.Id,
            storeProfile?.TradeName ?? "Lavaway Estética Automotiva",
            storeProfile?.Phone,
            storeAddress,
            customer?.Name ?? "Cliente",
            customer?.Phone ?? "",
            vehicle?.Plate ?? "",
            vehicle is not null ? $"Veiculo ({vehicle.Size})" : "Veiculo",
            vehicle?.Size.ToString() ?? "Padrao",
            workOrder.CreatedAtUtc,
            workOrder.EstimatedCompletionAtUtc,
            workOrder.TotalAmount,
            workOrder.Notes,
            items,
            inspectionModel);
    }
}

