using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

/// <summary>
/// State container reativo centralizado para armazenar as regras, limites e permissões
/// do plano de assinatura SaaS contratado pelo estabelecimento ativo.
/// </summary>
public sealed class TenantPlanStateService(SaasBillingApiClient? billingApiClient)
{
    public TenantSubscriptionOverviewDto? Overview { get; private set; }
    public bool IsLoaded { get; private set; }
    public bool IsLoading { get; private set; }

    public event Action? OnChange;

    public bool HasCustomerSubscriptions => Overview?.HasCustomerSubscriptions ?? false;
    public bool HasLoyalty => Overview?.HasLoyalty ?? false;
    public bool HasCommissions => Overview?.HasCommissions ?? false;
    public bool HasAiChatbot => Overview?.HasAiChatbot ?? false;
    public int MaxTeamMembers => Overview?.MaxTeamMembers ?? 0;
    public int MaxBoxes => Overview?.MaxBoxes ?? 0;

    public SaasPlanTier CurrentTier => Overview?.PlanTier ?? SaasPlanTier.Basic;
    public string PlanName => Overview?.PlanName ?? "Básico";
    public bool IsDelinquent => Overview?.Status == TenantSubscriptionStatus.Delinquent;
    public bool IsGracePeriod => Overview?.Status == TenantSubscriptionStatus.GracePeriod;

    public bool HasFeature(SaasFeature feature) => feature switch
    {
        SaasFeature.CustomerSubscriptions => HasCustomerSubscriptions,
        SaasFeature.Loyalty => HasLoyalty,
        SaasFeature.Commissions => HasCommissions,
        SaasFeature.AiChatbot => HasAiChatbot,
        _ => false
    };

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (IsLoaded || IsLoading || billingApiClient is null)
        {
            return;
        }

        await LoadInternalAsync(ct);
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (billingApiClient is null)
        {
            return;
        }

        await LoadInternalAsync(ct);
    }

    public void Reset()
    {
        Overview = null;
        IsLoaded = false;
        IsLoading = false;
        NotifyStateChanged();
    }

    public void SetOverviewForTesting(TenantSubscriptionOverviewDto overview)
    {
        Overview = overview;
        IsLoaded = true;
        IsLoading = false;
        NotifyStateChanged();
    }

    private async Task LoadInternalAsync(CancellationToken ct)
    {
        try
        {
            IsLoading = true;
            NotifyStateChanged();

            var result = await billingApiClient!.GetTenantOverviewAsync(ct);
            if (result.IsSuccess && result.Value is not null)
            {
                Overview = result.Value;
                IsLoaded = true;
            }
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
