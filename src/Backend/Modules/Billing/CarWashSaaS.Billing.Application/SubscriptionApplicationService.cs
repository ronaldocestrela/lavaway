using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Application;

public sealed class SubscriptionApplicationService(
    ISubscriptionPlanRepository planRepository,
    ICustomerSubscriptionRepository subscriptionRepository,
    IRecurringBillingGatewayProvider gatewayProvider) : ISubscriptionLookup, ISubscriptionUsageService
{
    public async Task<Result<SubscriptionPlanDto>> CreatePlanAsync(
        Guid tenantId,
        CreateSubscriptionPlanRequest request,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<SubscriptionPlanDto>.Failure(new Error("plan.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var planResult = SubscriptionPlan.Create(
            tenantId,
            request.Name,
            request.Description,
            request.MonthlyPrice,
            request.CreditsPerCycle,
            request.AllowedPlatesLimit,
            request.BillingIntervalDays <= 0 ? 30 : request.BillingIntervalDays);

        if (!planResult.IsSuccess)
        {
            return Result<SubscriptionPlanDto>.Failure(planResult.Error!);
        }

        var plan = planResult.Value!;
        await planRepository.AddAsync(plan, ct);
        await planRepository.SaveChangesAsync(ct);

        return Result<SubscriptionPlanDto>.Success(ToDto(plan));
    }

    public async Task<Result<SubscriptionPlanDto>> UpdatePlanAsync(
        Guid tenantId,
        Guid planId,
        UpdateSubscriptionPlanRequest request,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<SubscriptionPlanDto>.Failure(new Error("plan.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var plan = await planRepository.GetByIdAsync(tenantId, planId, ct);
        if (plan is null)
        {
            return Result<SubscriptionPlanDto>.Failure(new Error("plan.not_found", "Plano não encontrado.", ErrorType.NotFound));
        }

        var updateResult = plan.Update(
            request.Name,
            request.Description,
            request.MonthlyPrice,
            request.CreditsPerCycle,
            request.AllowedPlatesLimit,
            request.IsActive);

        if (!updateResult.IsSuccess)
        {
            return Result<SubscriptionPlanDto>.Failure(updateResult.Error!);
        }

        await planRepository.SaveChangesAsync(ct);
        return Result<SubscriptionPlanDto>.Success(ToDto(plan));
    }

    public async Task<Result<IReadOnlyList<SubscriptionPlanDto>>> ListPlansAsync(
        Guid tenantId,
        bool activeOnly = false,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyList<SubscriptionPlanDto>>.Failure(new Error("plan.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var plans = activeOnly
            ? await planRepository.ListActiveAsync(tenantId, ct)
            : await planRepository.ListAllAsync(tenantId, ct);

        var dtos = plans.Select(ToDto).ToList();
        return Result<IReadOnlyList<SubscriptionPlanDto>>.Success(dtos);
    }

    public async Task<Result<SubscriptionPlanDto>> GetPlanByIdAsync(
        Guid tenantId,
        Guid planId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<SubscriptionPlanDto>.Failure(new Error("plan.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var plan = await planRepository.GetByIdAsync(tenantId, planId, ct);
        if (plan is null)
        {
            return Result<SubscriptionPlanDto>.Failure(new Error("plan.not_found", "Plano não encontrado.", ErrorType.NotFound));
        }

        return Result<SubscriptionPlanDto>.Success(ToDto(plan));
    }

    public async Task<Result<CustomerSubscriptionDto>> SubscribeCustomerAsync(
        Guid tenantId,
        SubscribeCustomerRequest request,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CustomerSubscriptionDto>.Failure(new Error("subscription.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var plan = await planRepository.GetByIdAsync(tenantId, request.PlanId, ct);
        if (plan is null)
        {
            return Result<CustomerSubscriptionDto>.Failure(new Error("plan.not_found", "Plano de assinatura não encontrado.", ErrorType.NotFound));
        }

        if (!plan.IsActive)
        {
            return Result<CustomerSubscriptionDto>.Failure(new Error("plan.inactive", "Este plano não está ativo para novas adesões.", ErrorType.Validation));
        }

        // Processa recorrência no gateway
        var gatewayResult = await gatewayProvider.CreateSubscriptionAsync(
            tenantId,
            request.CustomerId,
            request.CustomerName,
            plan.MonthlyPrice,
            request.CardNumber,
            request.CardHolderName,
            request.CardExpiration,
            request.CardCvv,
            ct);

        if (!gatewayResult.IsSuccess)
        {
            return Result<CustomerSubscriptionDto>.Failure(gatewayResult.Error!);
        }

        var gwData = gatewayResult.Value!;
        var now = DateTimeOffset.UtcNow;
        var periodEnd = now.AddDays(plan.BillingIntervalDays <= 0 ? 30 : plan.BillingIntervalDays);

        var subResult = CustomerSubscription.Create(
            tenantId,
            request.CustomerId,
            request.CustomerName,
            request.CustomerPhone,
            plan.Id,
            plan.Name,
            plan.CreditsPerCycle,
            plan.AllowedPlatesLimit,
            request.InitialPlates,
            now,
            periodEnd,
            gwData.GatewaySubscriptionId,
            gwData.CardLastFourDigits,
            gwData.CardBrand);

        if (!subResult.IsSuccess)
        {
            return Result<CustomerSubscriptionDto>.Failure(subResult.Error!);
        }

        var subscription = subResult.Value!;
        await subscriptionRepository.AddAsync(subscription, ct);
        await subscriptionRepository.SaveChangesAsync(ct);

        return Result<CustomerSubscriptionDto>.Success(ToDto(subscription));
    }

    public async Task<Result<CustomerSubscriptionDto>> AddPlateToSubscriptionAsync(
        Guid tenantId,
        Guid subscriptionId,
        string plate,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CustomerSubscriptionDto>.Failure(new Error("subscription.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var subscription = await subscriptionRepository.GetByIdAsync(tenantId, subscriptionId, ct);
        if (subscription is null)
        {
            return Result<CustomerSubscriptionDto>.Failure(new Error("subscription.not_found", "Assinatura não encontrada.", ErrorType.NotFound));
        }

        var plan = await planRepository.GetByIdAsync(tenantId, subscription.PlanId, ct);
        var allowedLimit = plan?.AllowedPlatesLimit ?? 1;

        var addResult = subscription.AddAuthorizedPlate(plate, allowedLimit, DateTimeOffset.UtcNow);
        if (!addResult.IsSuccess)
        {
            return Result<CustomerSubscriptionDto>.Failure(addResult.Error!);
        }

        await subscriptionRepository.SaveChangesAsync(ct);
        return Result<CustomerSubscriptionDto>.Success(ToDto(subscription));
    }

    public async Task<Result<CustomerSubscriptionDto>> RemovePlateFromSubscriptionAsync(
        Guid tenantId,
        Guid subscriptionId,
        string plate,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CustomerSubscriptionDto>.Failure(new Error("subscription.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var subscription = await subscriptionRepository.GetByIdAsync(tenantId, subscriptionId, ct);
        if (subscription is null)
        {
            return Result<CustomerSubscriptionDto>.Failure(new Error("subscription.not_found", "Assinatura não encontrada.", ErrorType.NotFound));
        }

        var removeResult = subscription.RemoveAuthorizedPlate(plate);
        if (!removeResult.IsSuccess)
        {
            return Result<CustomerSubscriptionDto>.Failure(removeResult.Error!);
        }

        await subscriptionRepository.SaveChangesAsync(ct);
        return Result<CustomerSubscriptionDto>.Success(ToDto(subscription));
    }

    public async Task<Result<CustomerSubscriptionDto>> CancelSubscriptionAsync(
        Guid tenantId,
        Guid subscriptionId,
        string reason,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CustomerSubscriptionDto>.Failure(new Error("subscription.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var subscription = await subscriptionRepository.GetByIdAsync(tenantId, subscriptionId, ct);
        if (subscription is null)
        {
            return Result<CustomerSubscriptionDto>.Failure(new Error("subscription.not_found", "Assinatura não encontrada.", ErrorType.NotFound));
        }

        if (!string.IsNullOrWhiteSpace(subscription.GatewaySubscriptionId))
        {
            await gatewayProvider.CancelSubscriptionAsync(tenantId, subscription.GatewaySubscriptionId, ct);
        }

        subscription.Cancel(reason, DateTimeOffset.UtcNow);
        await subscriptionRepository.SaveChangesAsync(ct);

        return Result<CustomerSubscriptionDto>.Success(ToDto(subscription));
    }

    public async Task<Result<IReadOnlyList<CustomerSubscriptionDto>>> ListSubscriptionsAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyList<CustomerSubscriptionDto>>.Failure(new Error("subscription.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var list = await subscriptionRepository.ListAllAsync(tenantId, ct);
        var dtos = list.Select(ToDto).ToList();
        return Result<IReadOnlyList<CustomerSubscriptionDto>>.Success(dtos);
    }

    public async Task<Result<CustomerSubscriptionDto>> GetSubscriptionByIdAsync(
        Guid tenantId,
        Guid subscriptionId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CustomerSubscriptionDto>.Failure(new Error("subscription.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var sub = await subscriptionRepository.GetByIdAsync(tenantId, subscriptionId, ct);
        if (sub is null)
        {
            return Result<CustomerSubscriptionDto>.Failure(new Error("subscription.not_found", "Assinatura não encontrada.", ErrorType.NotFound));
        }

        return Result<CustomerSubscriptionDto>.Success(ToDto(sub));
    }

    public async Task<Result<IReadOnlyList<SubscriptionUsageDto>>> ListUsagesBySubscriptionAsync(
        Guid tenantId,
        Guid subscriptionId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyList<SubscriptionUsageDto>>.Failure(new Error("subscription.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var usages = await subscriptionRepository.ListUsagesBySubscriptionIdAsync(tenantId, subscriptionId, ct);
        var dtos = usages.Select(u => new SubscriptionUsageDto(
            u.Id,
            u.CustomerSubscriptionId,
            u.WorkOrderId,
            u.Plate,
            u.ServiceName,
            u.ConsumedAtUtc,
            u.Notes)).ToList();

        return Result<IReadOnlyList<SubscriptionUsageDto>>.Success(dtos);
    }

    public async Task<Result<SubscriptionDashboardSummaryDto>> GetDashboardSummaryAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<SubscriptionDashboardSummaryDto>.Failure(new Error("subscription.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var activeSubs = await subscriptionRepository.ListAllAsync(tenantId, ct);
        var activeList = activeSubs.Where(s => s.Status == SubscriptionStatusConstants.Active).ToList();

        var activeCount = activeList.Count;
        var totalCredits = activeList.Sum(s => s.AvailableCredits);
        var mrr = await subscriptionRepository.GetEstimatedMonthlyRevenueAsync(tenantId, ct);

        var now = DateTimeOffset.UtcNow;
        var firstDayOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var totalUsagesThisMonth = activeSubs
            .SelectMany(s => s.Usages)
            .Count(u => u.ConsumedAtUtc >= firstDayOfMonth);

        return Result<SubscriptionDashboardSummaryDto>.Success(new SubscriptionDashboardSummaryDto(
            activeCount,
            mrr,
            totalCredits,
            totalUsagesThisMonth));
    }

    public async Task<Result<SubscriptionPlateSummaryDto?>> GetActiveSubscriptionByPlateAsync(
        Guid tenantId,
        string plate,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<SubscriptionPlateSummaryDto?>.Failure(new Error("subscription.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var normalized = SubscriptionVehiclePlate.NormalizePlate(plate);
        if (normalized.Length != 7)
        {
            return Result<SubscriptionPlateSummaryDto?>.Success(null);
        }

        var now = DateTimeOffset.UtcNow;
        var sub = await subscriptionRepository.GetActiveByPlateAsync(tenantId, normalized, now, ct);
        if (sub is null)
        {
            return Result<SubscriptionPlateSummaryDto?>.Success(null);
        }

        var canConsume = sub.CanConsumeCredit(normalized, now).IsSuccess;

        return Result<SubscriptionPlateSummaryDto?>.Success(new SubscriptionPlateSummaryDto(
            sub.Id,
            sub.CustomerId,
            sub.CustomerName,
            sub.PlanName,
            sub.Status,
            normalized,
            sub.TotalCreditsInCycle,
            sub.UsedCreditsInCycle,
            sub.AvailableCredits,
            canConsume,
            sub.CurrentPeriodEndUtc));
    }

    public async Task<Result<IReadOnlyList<CustomerSubscriptionDto>>> GetCustomerSubscriptionsAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyList<CustomerSubscriptionDto>>.Failure(new Error("subscription.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var subs = await subscriptionRepository.ListByCustomerIdAsync(tenantId, customerId, ct);
        var dtos = subs.Select(ToDto).ToList();
        return Result<IReadOnlyList<CustomerSubscriptionDto>>.Success(dtos);
    }

    public async Task<Result<SubscriptionUsageReceiptDto>> ConsumeCreditForWorkOrderAsync(
        Guid tenantId,
        ConsumeSubscriptionCreditRequest request,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<SubscriptionUsageReceiptDto>.Failure(new Error("subscription.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var normalized = SubscriptionVehiclePlate.NormalizePlate(request.Plate);
        if (normalized.Length != 7)
        {
            return Result<SubscriptionUsageReceiptDto>.Failure(new Error("subscription.plate_invalid", "Placa do veículo inválida.", ErrorType.Validation));
        }

        var now = DateTimeOffset.UtcNow;
        var sub = await subscriptionRepository.GetActiveByPlateAsync(tenantId, normalized, now, ct);
        if (sub is null)
        {
            return Result<SubscriptionUsageReceiptDto>.Failure(new Error("subscription.not_found", $"Nenhuma assinatura ativa encontrada para a placa {normalized}.", ErrorType.NotFound));
        }

        var consumeResult = sub.ConsumeCredit(normalized, request.WorkOrderId, request.ServiceName, now, request.Notes);
        if (!consumeResult.IsSuccess)
        {
            return Result<SubscriptionUsageReceiptDto>.Failure(consumeResult.Error!);
        }

        await subscriptionRepository.SaveChangesAsync(ct);
        var usage = consumeResult.Value!;

        return Result<SubscriptionUsageReceiptDto>.Success(new SubscriptionUsageReceiptDto(
            usage.Id,
            sub.Id,
            usage.WorkOrderId,
            usage.Plate,
            sub.AvailableCredits,
            usage.ConsumedAtUtc));
    }

    public async Task<Result> CancelUsageForWorkOrderAsync(
        Guid tenantId,
        Guid workOrderId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure(new Error("subscription.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (workOrderId == Guid.Empty)
        {
            return Result.Failure(new Error("subscription.work_order_required", "Ordem de serviço é obrigatória.", ErrorType.Validation));
        }

        var allSubs = await subscriptionRepository.ListAllAsync(tenantId, ct);
        var subWithUsage = allSubs.FirstOrDefault(s => s.Usages.Any(u => u.WorkOrderId == workOrderId));
        if (subWithUsage is null)
        {
            return Result.Failure(new Error("subscription.usage_not_found", "Nenhum consumo de assinatura localizado para esta ordem de serviço.", ErrorType.NotFound));
        }

        var cancelResult = subWithUsage.CancelUsageForWorkOrder(workOrderId);
        if (!cancelResult.IsSuccess)
        {
            return cancelResult;
        }

        await subscriptionRepository.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ProcessRecurringRenewalWebhookAsync(
        Guid tenantId,
        string gatewaySubscriptionId,
        decimal amount,
        DateTimeOffset periodStartUtc,
        DateTimeOffset periodEndUtc,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(gatewaySubscriptionId))
        {
            return Result.Failure(new Error("subscription.webhook_invalid", "Dados inválidos para renovação.", ErrorType.Validation));
        }

        var allSubs = await subscriptionRepository.ListAllAsync(tenantId, ct);
        var subscription = allSubs.FirstOrDefault(s => string.Equals(s.GatewaySubscriptionId, gatewaySubscriptionId, StringComparison.OrdinalIgnoreCase));
        if (subscription is null)
        {
            return Result.Failure(new Error("subscription.not_found", "Assinatura do gateway não localizada.", ErrorType.NotFound));
        }

        var plan = await planRepository.GetByIdAsync(tenantId, subscription.PlanId, ct);
        var creditsToGrant = plan?.CreditsPerCycle ?? subscription.TotalCreditsInCycle;

        subscription.RenewCycle(periodStartUtc, periodEndUtc, creditsToGrant);
        await subscriptionRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static SubscriptionPlanDto ToDto(SubscriptionPlan plan) => new(
        plan.Id,
        plan.TenantId,
        plan.Name,
        plan.Description,
        plan.MonthlyPrice,
        plan.BillingIntervalDays,
        plan.CreditsPerCycle,
        plan.AllowedPlatesLimit,
        plan.IsActive,
        plan.CreatedUtc);

    private static CustomerSubscriptionDto ToDto(CustomerSubscription sub) => new(
        sub.Id,
        sub.TenantId,
        sub.CustomerId,
        sub.CustomerName,
        sub.CustomerPhone,
        sub.PlanId,
        sub.PlanName,
        sub.Status,
        sub.CurrentPeriodStartUtc,
        sub.CurrentPeriodEndUtc,
        sub.TotalCreditsInCycle,
        sub.UsedCreditsInCycle,
        sub.AvailableCredits,
        sub.AuthorizedPlates.Select(p => p.Plate).ToList(),
        sub.CardLastFourDigits,
        sub.CardBrand,
        sub.GatewaySubscriptionId,
        sub.CreatedUtc);
}
