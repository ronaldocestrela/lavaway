using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Application;

public sealed class PlatformSaasBillingApplicationService(
    ITenantSaasSubscriptionRepository subscriptionRepository,
    ISaasPlanRepository planRepository,
    ITenantPlanQuotaLookup quotaLookup,
    IGlobalTenantLookup globalTenantLookup)
{
    public async Task<Result<IReadOnlyList<TenantSubscriptionOverviewDto>>> ListAllTenantSubscriptionsAsync(CancellationToken ct = default)
    {
        var subscriptions = await subscriptionRepository.ListAllAsync(ct);
        var resultList = new List<TenantSubscriptionOverviewDto>();

        foreach (var sub in subscriptions)
        {
            var overviewResult = await quotaLookup.GetSubscriptionOverviewAsync(sub.TenantId, ct);
            if (overviewResult.IsSuccess)
            {
                resultList.Add(overviewResult.Value!);
            }
        }

        return Result<IReadOnlyList<TenantSubscriptionOverviewDto>>.Success(resultList);
    }

    public async Task<Result<TenantSubscriptionOverviewDto>> AdminOverridePlanAsync(
        Guid tenantId,
        AdminOverridePlanRequest request,
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

        var targetPlan = await planRepository.GetByTierAsync(request.TargetTier, ct);
        if (targetPlan is null)
        {
            return Result<TenantSubscriptionOverviewDto>.Failure(new Error("saas_plan.not_found", "Plano solicitado não encontrado.", ErrorType.NotFound));
        }

        subscription.ChangePlan(targetPlan);

        if (request.NewStatus.HasValue)
        {
            switch (request.NewStatus.Value)
            {
                case TenantSubscriptionStatus.Active:
                    subscription.Activate(targetPlan);
                    await globalTenantLookup.UpdateTenantStatusAsync(tenantId, new UpdateTenantStatusRequest(TenantStatus.Active, request.Reason), ct);
                    break;
                case TenantSubscriptionStatus.Delinquent:
                    subscription.Suspend(request.Reason);
                    await globalTenantLookup.UpdateTenantStatusAsync(tenantId, new UpdateTenantStatusRequest(TenantStatus.Delinquent, request.Reason), ct);
                    break;
                case TenantSubscriptionStatus.Canceled:
                    subscription.Cancel(request.Reason);
                    await globalTenantLookup.UpdateTenantStatusAsync(tenantId, new UpdateTenantStatusRequest(TenantStatus.Canceled, request.Reason), ct);
                    break;
            }
        }

        subscriptionRepository.Update(subscription);
        await subscriptionRepository.SaveChangesAsync(ct);

        return await quotaLookup.GetSubscriptionOverviewAsync(tenantId, ct);
    }
}
