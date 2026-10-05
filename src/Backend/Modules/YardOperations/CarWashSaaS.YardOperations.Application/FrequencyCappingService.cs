using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Application;

public sealed class FrequencyCappingService(
    IReactivationCampaignRepository campaignRepository,
    ICustomerCommunicationPreferenceLookup preferenceLookup) : IFrequencyCappingService
{
    public static readonly TimeSpan CoolingPeriod = TimeSpan.FromDays(7);

    public async Task<FrequencyCappingResult> CanSendMarketingMessageAsync(
        Guid tenantId,
        Guid customerId,
        string customerPhone,
        int daysInactive,
        CancellationToken ct = default)
    {
        // 1. Verificação de Opt-Out
        var prefResult = await preferenceLookup.GetPreferenceAsync(tenantId, customerPhone, ct);
        if (prefResult.IsSuccess && prefResult.Value is not null && !prefResult.Value.IsOptedIn)
        {
            return new FrequencyCappingResult(false, "Cliente optou por não receber mensagens promocionais (Opt-out)", "opted_out");
        }

        // 2. Intervalo mínimo de descanso (7 dias)
        var hasRecent = await campaignRepository.HasRecentCampaignLogAsync(tenantId, customerId, CoolingPeriod, ct);
        if (hasRecent)
        {
            return new FrequencyCappingResult(false, "Cliente recebeu comunicação de marketing nos últimos 7 dias (Limite de Frequência)", "frequency_cooling_period");
        }

        // 3. Verificação de disparo duplicado no mesmo ciclo mensal
        var now = DateTimeOffset.UtcNow;
        var cycleKey = $"reactivation-{daysInactive}d-{customerId:N}-{now:yyyyMM}";
        var alreadyContacted = await campaignRepository.HasCampaignLogForCycleAsync(tenantId, customerId, cycleKey, ct);
        if (alreadyContacted)
        {
            return new FrequencyCappingResult(false, $"Cliente já foi impactado pela régua de {daysInactive} dias neste mês", "already_contacted_in_cycle");
        }

        return new FrequencyCappingResult(true, "Elegível para envio");
    }
}
