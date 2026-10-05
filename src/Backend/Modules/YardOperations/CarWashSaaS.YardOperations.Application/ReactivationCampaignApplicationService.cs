using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed class ReactivationCampaignApplicationService(
    IReactivationCampaignRepository campaignRepository,
    IFrequencyCappingService frequencyCappingService,
    IUnitOfWork unitOfWork,
    ITenantStoreProfileLookup storeProfileLookup,
    IOutboundWhatsAppDispatcher whatsAppDispatcher)
{
    private static readonly (int Days, string Title, string Template, string? Offer)[] DefaultRules =
    [
        (15, "Manutenção de Brilho (15 dias)", "Olá, {cliente}! Já se passaram 15 dias da sua última visita ao {loja}. Que tal agendar a lavagem do seu {veiculo} para manter a proteção da pintura? 🚗✨", "5% OFF no agendamento"),
        (30, "Saudade do seu carro limpo (30 dias)", "Olá, {cliente}! Faz 1 mês desde que cuidamos do seu {veiculo} no {loja}. Aproveite nossa condição especial desta semana e venha nos visitar! 🧼🚘", "Aromatizante de brinde"),
        (45, "Campanha Especial de Retorno (45 dias)", "Olá, {cliente}! Estamos com saudades! Já faz 45 dias da sua última lavagem no {loja}. Reserve hoje seu horário e ganhe uma condição imperdível. 💎", "10% de desconto na Lavagem Completa")
    ];

    public async Task<Result<IReadOnlyList<ReactivationCampaignRuleDto>>> GetRulesAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyList<ReactivationCampaignRuleDto>>.Failure(new Error("campaign.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        await EnsureDefaultRulesExistAsync(tenantId, ct);
        var rules = await campaignRepository.GetRulesAsync(tenantId, ct);

        var result = new List<ReactivationCampaignRuleDto>();
        foreach (var rule in rules)
        {
            var (eligible, capped, optedOut) = await CalculateAudienceMetricsAsync(tenantId, rule.DaysInactive, ct);
            result.Add(new ReactivationCampaignRuleDto(
                rule.Id,
                rule.TenantId,
                rule.DaysInactive,
                rule.Title,
                rule.MessageTemplate,
                rule.IsEnabled,
                rule.PromotionalOffer,
                eligible,
                capped,
                optedOut,
                rule.UpdatedAtUtc));
        }

        return Result<IReadOnlyList<ReactivationCampaignRuleDto>>.Success(result);
    }

    public async Task<Result<ReactivationCampaignRuleDto>> UpdateRuleAsync(
        Guid tenantId,
        Guid ruleId,
        UpdateReactivationCampaignRuleRequest request,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || ruleId == Guid.Empty)
        {
            return Result<ReactivationCampaignRuleDto>.Failure(new Error("campaign.invalid_input", "Tenant e ID da regra são obrigatórios.", ErrorType.Validation));
        }

        var rule = await campaignRepository.GetRuleByIdAsync(tenantId, ruleId, ct);
        if (rule is null)
        {
            return Result<ReactivationCampaignRuleDto>.Failure(new Error("campaign.rule_not_found", "Regra de campanha não encontrada.", ErrorType.NotFound));
        }

        var updateResult = rule.Update(request.Title, request.MessageTemplate, request.IsEnabled, request.PromotionalOffer);
        if (!updateResult.IsSuccess)
        {
            return Result<ReactivationCampaignRuleDto>.Failure(updateResult.Error!);
        }

        await unitOfWork.SaveChangesAsync(ct);
        var (eligible, capped, optedOut) = await CalculateAudienceMetricsAsync(tenantId, rule.DaysInactive, ct);

        return Result<ReactivationCampaignRuleDto>.Success(new ReactivationCampaignRuleDto(
            rule.Id,
            rule.TenantId,
            rule.DaysInactive,
            rule.Title,
            rule.MessageTemplate,
            rule.IsEnabled,
            rule.PromotionalOffer,
            eligible,
            capped,
            optedOut,
            rule.UpdatedAtUtc));
    }

    public async Task<Result<IReadOnlyList<InactiveCustomerSummaryDto>>> GetInactiveCustomersForTierAsync(
        Guid tenantId,
        int daysInactive,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyList<InactiveCustomerSummaryDto>>.Failure(new Error("campaign.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var (minDays, maxDays) = GetTierWindow(daysInactive);
        var customers = await campaignRepository.GetInactiveCustomersAsync(tenantId, minDays, maxDays, ct);

        var list = new List<InactiveCustomerSummaryDto>();
        foreach (var c in customers)
        {
            var capResult = await frequencyCappingService.CanSendMarketingMessageAsync(tenantId, c.CustomerId, c.CustomerPhone, daysInactive, ct);
            list.Add(new InactiveCustomerSummaryDto(
                c.CustomerId,
                c.CustomerName,
                c.CustomerPhone,
                c.VehiclePlate,
                c.VehicleModel,
                c.LastVisitAtUtc,
                c.DaysInactive,
                capResult.IsAllowed,
                capResult.Reason));
        }

        return Result<IReadOnlyList<InactiveCustomerSummaryDto>>.Success(list);
    }

    public async Task<Result<DispatchCampaignResultDto>> ScanAndDispatchCampaignsAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<DispatchCampaignResultDto>.Failure(new Error("campaign.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        await EnsureDefaultRulesExistAsync(tenantId, ct);
        var rules = await campaignRepository.GetRulesAsync(tenantId, ct);
        var enabledRules = rules.Where(r => r.IsEnabled).ToList();

        var totalEvaluated = 0;
        var totalDispatched = 0;
        var totalSkippedFrequencyCap = 0;
        var totalSkippedOptOut = 0;

        var storeProfileResult = await storeProfileLookup.GetProfileAsync(tenantId, ct);
        var storeName = storeProfileResult.Value?.TradeName ?? "Lavaway";

        foreach (var rule in enabledRules)
        {
            var (minDays, maxDays) = GetTierWindow(rule.DaysInactive);
            var inactiveCustomers = await campaignRepository.GetInactiveCustomersAsync(tenantId, minDays, maxDays, ct);

            foreach (var customer in inactiveCustomers)
            {
                totalEvaluated++;
                var capResult = await frequencyCappingService.CanSendMarketingMessageAsync(
                    tenantId, customer.CustomerId, customer.CustomerPhone, rule.DaysInactive, ct);

                if (!capResult.IsAllowed)
                {
                    if (capResult.RejectionCode == "opted_out")
                    {
                        totalSkippedOptOut++;
                    }
                    else
                    {
                        totalSkippedFrequencyCap++;
                    }
                    continue;
                }

                var message = RenderTemplate(rule.MessageTemplate, customer, storeName, rule.PromotionalOffer);
                var cycleKey = $"reactivation-{rule.DaysInactive}d-{customer.CustomerId:N}-{DateTime.UtcNow:yyyyMM}";

                var dispatchResult = await whatsAppDispatcher.DispatchTextMessageAsync(
                    tenantId,
                    customer.CustomerPhone,
                    message,
                    cycleKey,
                    ct);

                if (dispatchResult.IsSuccess)
                {
                    var logResult = ReactivationCampaignLog.Create(
                        tenantId,
                        rule.Id,
                        customer.CustomerId,
                        customer.CustomerPhone,
                        rule.DaysInactive,
                        cycleKey,
                        DateTimeOffset.UtcNow);

                    if (logResult.IsSuccess)
                    {
                        await campaignRepository.AddLogAsync(logResult.Value!, ct);
                        totalDispatched++;
                    }
                }
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result<DispatchCampaignResultDto>.Success(new DispatchCampaignResultDto(
            totalEvaluated,
            totalDispatched,
            totalSkippedFrequencyCap,
            totalSkippedOptOut));
    }

    public async Task<Result<DispatchCampaignResultDto>> DispatchRuleManuallyAsync(Guid tenantId, Guid ruleId, CancellationToken ct = default)
    {
        var rule = await campaignRepository.GetRuleByIdAsync(tenantId, ruleId, ct);
        if (rule is null)
        {
            return Result<DispatchCampaignResultDto>.Failure(new Error("campaign.rule_not_found", "Regra de campanha não encontrada.", ErrorType.NotFound));
        }

        var (minDays, maxDays) = GetTierWindow(rule.DaysInactive);
        var inactiveCustomers = await campaignRepository.GetInactiveCustomersAsync(tenantId, minDays, maxDays, ct);

        var storeProfileResult = await storeProfileLookup.GetProfileAsync(tenantId, ct);
        var storeName = storeProfileResult.Value?.TradeName ?? "Lavaway";

        var totalEvaluated = 0;
        var totalDispatched = 0;
        var totalSkippedFrequencyCap = 0;
        var totalSkippedOptOut = 0;

        foreach (var customer in inactiveCustomers)
        {
            totalEvaluated++;
            var capResult = await frequencyCappingService.CanSendMarketingMessageAsync(
                tenantId, customer.CustomerId, customer.CustomerPhone, rule.DaysInactive, ct);

            if (!capResult.IsAllowed)
            {
                if (capResult.RejectionCode == "opted_out") totalSkippedOptOut++;
                else totalSkippedFrequencyCap++;
                continue;
            }

            var message = RenderTemplate(rule.MessageTemplate, customer, storeName, rule.PromotionalOffer);
            var cycleKey = $"reactivation-{rule.DaysInactive}d-{customer.CustomerId:N}-{DateTime.UtcNow:yyyyMM}";

            var dispatchResult = await whatsAppDispatcher.DispatchTextMessageAsync(
                tenantId,
                customer.CustomerPhone,
                message,
                cycleKey,
                ct);

            if (dispatchResult.IsSuccess)
            {
                var logResult = ReactivationCampaignLog.Create(
                    tenantId,
                    rule.Id,
                    customer.CustomerId,
                    customer.CustomerPhone,
                    rule.DaysInactive,
                    cycleKey,
                    DateTimeOffset.UtcNow);

                if (logResult.IsSuccess)
                {
                    await campaignRepository.AddLogAsync(logResult.Value!, ct);
                    totalDispatched++;
                }
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result<DispatchCampaignResultDto>.Success(new DispatchCampaignResultDto(
            totalEvaluated,
            totalDispatched,
            totalSkippedFrequencyCap,
            totalSkippedOptOut));
    }

    private static (int MinDays, int? MaxDays) GetTierWindow(int daysInactive) => daysInactive switch
    {
        15 => (15, 29),
        30 => (30, 44),
        _ => (45, null)
    };

    private static string RenderTemplate(string template, CustomerLastVisitInfo customer, string storeName, string? offer)
    {
        var vehicleDesc = !string.IsNullOrWhiteSpace(customer.VehicleModel)
            ? customer.VehicleModel
            : (!string.IsNullOrWhiteSpace(customer.VehiclePlate) ? $"veículo placa {customer.VehiclePlate}" : "veículo");

        return template
            .Replace("{cliente}", customer.CustomerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{veiculo}", vehicleDesc, StringComparison.OrdinalIgnoreCase)
            .Replace("{placa}", customer.VehiclePlate, StringComparison.OrdinalIgnoreCase)
            .Replace("{loja}", storeName, StringComparison.OrdinalIgnoreCase)
            .Replace("{dias}", customer.DaysInactive.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{oferta}", offer ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(int Eligible, int Capped, int OptedOut)> CalculateAudienceMetricsAsync(Guid tenantId, int daysInactive, CancellationToken ct)
    {
        var (minDays, maxDays) = GetTierWindow(daysInactive);
        var customers = await campaignRepository.GetInactiveCustomersAsync(tenantId, minDays, maxDays, ct);

        var eligible = 0;
        var capped = 0;
        var optedOut = 0;

        foreach (var c in customers)
        {
            var cap = await frequencyCappingService.CanSendMarketingMessageAsync(tenantId, c.CustomerId, c.CustomerPhone, daysInactive, ct);
            if (cap.IsAllowed) eligible++;
            else if (cap.RejectionCode == "opted_out") optedOut++;
            else capped++;
        }

        return (eligible, capped, optedOut);
    }

    private async Task EnsureDefaultRulesExistAsync(Guid tenantId, CancellationToken ct)
    {
        var existing = await campaignRepository.GetRulesAsync(tenantId, ct);
        if (existing.Count > 0) return;

        foreach (var (days, title, template, offer) in DefaultRules)
        {
            var ruleResult = ReactivationCampaignRule.Create(tenantId, days, title, template, isEnabled: true, offer);
            if (ruleResult.IsSuccess)
            {
                await campaignRepository.AddRuleAsync(ruleResult.Value!, ct);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
    }
}
