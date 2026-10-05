using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed class AfterSalesApplicationService(
    IWorkOrderRepository workOrderRepository,
    ICustomerRepository customerRepository,
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork,
    ITenantStoreProfileLookup storeProfileLookup,
    IOutboundWhatsAppDispatcher whatsAppDispatcher,
    ICustomerCommunicationPreferenceLookup preferenceLookup,
    IYardRealtimeNotifier? realtimeNotifier = null) : IAfterSalesLookup
{
    public async Task<Result<WorkOrderDto>> RegisterPickupAsync(
        Guid tenantId,
        Guid workOrderId,
        DateTimeOffset? pickedUpAtUtc = null,
        string? notes = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || workOrderId == Guid.Empty)
        {
            return Result<WorkOrderDto>.Failure(new Error("after_sales.invalid_input", "Tenant e ordem de serviço são obrigatórios.", ErrorType.Validation));
        }

        var workOrder = await workOrderRepository.GetByIdAsync(tenantId, workOrderId, ct);
        if (workOrder is null)
        {
            return Result<WorkOrderDto>.Failure(new Error("work_order.not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        var pickupResult = workOrder.RegisterPickup(pickedUpAtUtc, notes);
        if (!pickupResult.IsSuccess)
        {
            return Result<WorkOrderDto>.Failure(pickupResult.Error!);
        }

        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<WorkOrderDto>.Failure(saveResult.Error!);
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, workOrder.CustomerId, ct);
        var vehicle = await vehicleRepository.GetByIdAsync(tenantId, workOrder.VehicleId, ct);

        var dto = ToWorkOrderDto(workOrder, customer?.Name ?? "Cliente", vehicle?.Plate ?? "", vehicle?.Size.ToString() ?? "HatchSedan");

        if (realtimeNotifier is not null)
        {
            var notification = new WorkOrderMovedNotification(
                workOrder.Id,
                workOrder.Status.ToString(),
                workOrder.Status.ToString(),
                workOrder.AssignedOperatorId,
                workOrder.AssignedOperatorName,
                workOrder.PickedUpAtUtc ?? DateTimeOffset.UtcNow,
                notes ?? "Veículo retirado");
            await realtimeNotifier.NotifyWorkOrderMovedAsync(tenantId, notification, ct);
        }

        return Result<WorkOrderDto>.Success(dto);
    }

    public async Task<Result<int>> ScanAndDispatchPendingSurveysAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<int>.Failure(new Error("after_sales.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var cutoff = DateTimeOffset.UtcNow.AddHours(-1);
        var pendingOrders = await workOrderRepository.GetWorkOrdersPendingSurveyAsync(tenantId, cutoff, ct);
        if (pendingOrders.Count == 0)
        {
            return Result<int>.Success(0);
        }

        var storeProfileResult = await storeProfileLookup.GetProfileAsync(tenantId, ct);
        var storeName = storeProfileResult.Value?.TradeName ?? "Lavaway";

        var dispatchedCount = 0;

        foreach (var order in pendingOrders)
        {
            var customer = await customerRepository.GetByIdAsync(tenantId, order.CustomerId, ct);
            if (customer is null || string.IsNullOrWhiteSpace(customer.Phone))
            {
                continue;
            }

            // Checagem de Opt-Out
            var prefResult = await preferenceLookup.GetPreferenceAsync(tenantId, customer.Phone, ct);
            if (prefResult.IsSuccess && prefResult.Value is not null && !prefResult.Value.IsOptedIn)
            {
                order.MarkSurveySkippedOptOut();
                continue;
            }

            var vehicle = await vehicleRepository.GetByIdAsync(tenantId, order.VehicleId, ct);
            var plate = vehicle?.Plate ?? "Veículo";

            var message = $"👋 Olá, *{customer.Name}*! Seu veículo (*{plate}*) foi entregue há pouco pelo *{storeName}*.\n\n" +
                          "Como foi sua experiência com nosso serviço? Por favor, avalie respondendo de *1 a 5 estrelas*:\n\n" +
                          "1️⃣ - Muito Insatisfeito ⭐\n" +
                          "2️⃣ - Insatisfeito ⭐⭐\n" +
                          "3️⃣ - Regular ⭐⭐⭐\n" +
                          "4️⃣ - Bom ⭐⭐⭐⭐\n" +
                          "5️⃣ - Excelente ⭐⭐⭐⭐⭐\n\n" +
                          "Sua opinião é fundamental para nós!";

            var idempotencyKey = $"survey-{order.Id:N}";
            var dispatchResult = await whatsAppDispatcher.DispatchTextMessageAsync(
                tenantId,
                customer.Phone,
                message,
                idempotencyKey,
                ct);

            if (dispatchResult.IsSuccess)
            {
                order.RecordSatisfactionSurveySent(DateTimeOffset.UtcNow);
                dispatchedCount++;
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result<int>.Success(dispatchedCount);
    }

    public async Task<Result> DispatchSurveyManuallyAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default)
    {
        var order = await workOrderRepository.GetByIdAsync(tenantId, workOrderId, ct);
        if (order is null)
        {
            return Result.Failure(new Error("work_order.not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        if (!order.PickedUpAtUtc.HasValue)
        {
            return Result.Failure(new Error("work_order.not_picked_up", "A retirada do veículo ainda não foi registrada.", ErrorType.Conflict));
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, order.CustomerId, ct);
        if (customer is null || string.IsNullOrWhiteSpace(customer.Phone))
        {
            return Result.Failure(new Error("customer.phone_required", "Telefone do cliente não encontrado.", ErrorType.Validation));
        }

        var prefResult = await preferenceLookup.GetPreferenceAsync(tenantId, customer.Phone, ct);
        if (prefResult.IsSuccess && prefResult.Value is not null && !prefResult.Value.IsOptedIn)
        {
            return Result.Failure(new Error("customer.opted_out", "Cliente optou por não receber comunicações (Opt-out).", ErrorType.Validation));
        }

        var vehicle = await vehicleRepository.GetByIdAsync(tenantId, order.VehicleId, ct);
        var storeProfileResult = await storeProfileLookup.GetProfileAsync(tenantId, ct);
        var storeName = storeProfileResult.Value?.TradeName ?? "Lavaway";

        var message = $"👋 Olá, *{customer.Name}*! Como foi sua experiência com a lavagem do seu veículo (*{vehicle?.Plate}*) no *{storeName}*?\n\n" +
                      "Responda de *1 a 5* com sua nota de satisfação (onde 5 é Excelente)!";

        var idempotencyKey = $"survey-manual-{order.Id:N}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var dispatchResult = await whatsAppDispatcher.DispatchTextMessageAsync(
            tenantId,
            customer.Phone,
            message,
            idempotencyKey,
            ct);

        if (!dispatchResult.IsSuccess)
        {
            return Result.Failure(dispatchResult.Error!);
        }

        order.RecordSatisfactionSurveySent(DateTimeOffset.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SubmitSurveyRatingAsync(
        Guid tenantId,
        string customerPhone,
        int rating,
        string? feedbackComment = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(customerPhone))
        {
            return Result.Failure(new Error("after_sales.invalid_input", "Tenant e telefone são obrigatórios.", ErrorType.Validation));
        }

        var order = await workOrderRepository.GetLatestCompletedOrderByPhoneAsync(tenantId, customerPhone, ct);
        if (order is null)
        {
            return Result.Failure(new Error("after_sales.order_not_found", "Nenhuma ordem de serviço recente encontrada para este cliente.", ErrorType.NotFound));
        }

        var ratingResult = order.RecordSatisfactionRating(rating, feedbackComment, DateTimeOffset.UtcNow);
        if (!ratingResult.IsSuccess)
        {
            return Result.Failure(ratingResult.Error!);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<SatisfactionSurveyDto?>> GetPendingSurveyForCustomerAsync(
        Guid tenantId,
        string customerPhone,
        CancellationToken ct = default)
    {
        var order = await workOrderRepository.GetLatestCompletedOrderByPhoneAsync(tenantId, customerPhone, ct);
        if (order is null || !order.SurveySentAtUtc.HasValue || order.SurveyRating.HasValue)
        {
            return Result<SatisfactionSurveyDto?>.Success(null);
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, order.CustomerId, ct);
        var vehicle = await vehicleRepository.GetByIdAsync(tenantId, order.VehicleId, ct);

        var dto = new SatisfactionSurveyDto(
            order.Id,
            order.CustomerId,
            customer?.Name ?? "Cliente",
            customer?.Phone ?? customerPhone,
            vehicle?.Plate ?? "",
            vehicle?.Size.ToString() ?? "",
            order.PickedUpAtUtc ?? order.CreatedAtUtc,
            order.SurveySentAtUtc,
            order.SurveyRating,
            order.SurveyFeedback,
            order.SurveyRespondedAtUtc,
            order.SurveyRating.HasValue ? "Respondido" : "Aguardando Resposta");

        return Result<SatisfactionSurveyDto?>.Success(dto);
    }

    public async Task<Result<AfterSalesMetricsDto>> GetMetricsAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<AfterSalesMetricsDto>.Failure(new Error("after_sales.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var surveys = await workOrderRepository.ListSurveysAsync(tenantId, limit: 1000, ct);
        var sent = surveys.Count;
        var respondedSurveys = surveys.Where(s => s.SurveyRating.HasValue).ToList();
        var responded = respondedSurveys.Count;

        var rate = sent > 0 ? Math.Round((decimal)responded / sent * 100m, 1) : 0m;
        var avg = responded > 0 ? Math.Round((decimal)respondedSurveys.Average(s => s.SurveyRating!.Value), 1) : 0m;

        var five = respondedSurveys.Count(s => s.SurveyRating == 5);
        var four = respondedSurveys.Count(s => s.SurveyRating == 4);
        var three = respondedSurveys.Count(s => s.SurveyRating == 3);
        var two = respondedSurveys.Count(s => s.SurveyRating == 2);
        var one = respondedSurveys.Count(s => s.SurveyRating == 1);

        return Result<AfterSalesMetricsDto>.Success(new AfterSalesMetricsDto(
            sent,
            responded,
            rate,
            avg,
            five,
            four,
            three,
            two,
            one));
    }

    public async Task<Result<IReadOnlyList<SatisfactionSurveyDto>>> ListSurveysAsync(
        Guid tenantId,
        int limit = 50,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyList<SatisfactionSurveyDto>>.Failure(new Error("after_sales.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var orders = await workOrderRepository.ListSurveysAsync(tenantId, limit, ct);
        var result = new List<SatisfactionSurveyDto>();

        foreach (var order in orders)
        {
            var customer = await customerRepository.GetByIdAsync(tenantId, order.CustomerId, ct);
            var vehicle = await vehicleRepository.GetByIdAsync(tenantId, order.VehicleId, ct);

            result.Add(new SatisfactionSurveyDto(
                order.Id,
                order.CustomerId,
                customer?.Name ?? "Cliente",
                customer?.Phone ?? "",
                vehicle?.Plate ?? "",
                vehicle?.Size.ToString() ?? "",
                order.PickedUpAtUtc ?? order.CreatedAtUtc,
                order.SurveySentAtUtc,
                order.SurveyRating,
                order.SurveyFeedback,
                order.SurveyRespondedAtUtc,
                order.SurveyRating.HasValue ? "Respondido" : "Pendente"));
        }

        return Result<IReadOnlyList<SatisfactionSurveyDto>>.Success(result);
    }

    private static WorkOrderDto ToWorkOrderDto(WorkOrder workOrder, string customerName, string plate, string vehicleSize)
    {
        var items = workOrder.Items.Select(item => new WorkOrderItemDto(
            item.Id,
            item.ServiceId,
            item.ServiceName,
            item.UnitPrice,
            item.EstimatedDurationMinutes,
            item.Quantity,
            item.TotalAmount,
            item.TotalDurationMinutes)).ToList();

        var history = workOrder.StatusHistory.Select(h => new WorkOrderStatusHistoryDto(
            h.Id,
            h.FromStatus?.ToString(),
            h.ToStatus.ToString(),
            h.ChangedAtUtc,
            h.ChangedByOperatorId,
            h.ChangedByOperatorName,
            h.Notes)).ToList();

        return new WorkOrderDto(
            workOrder.Id,
            workOrder.CustomerId,
            customerName,
            workOrder.VehicleId,
            plate,
            vehicleSize,
            workOrder.Status.ToString(),
            workOrder.TotalAmount,
            workOrder.EstimatedDurationMinutes,
            workOrder.CreatedAtUtc,
            workOrder.EstimatedCompletionAtUtc,
            workOrder.Notes,
            items,
            workOrder.AssignedOperatorId,
            workOrder.AssignedOperatorName,
            history,
            workOrder.PickedUpAtUtc,
            workOrder.SurveySentAtUtc,
            workOrder.SurveyRating,
            workOrder.SurveyRespondedAtUtc);
    }
}
