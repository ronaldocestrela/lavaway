using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed class LoyaltyApplicationService(
    ILoyaltyProgramRepository programRepository,
    ICustomerLoyaltyRepository loyaltyRepository,
    IWorkOrderRepository workOrderRepository,
    ICustomerRepository customerRepository,
    IOutboundWhatsAppDispatcher whatsAppDispatcher,
    ITenantStoreProfileLookup storeProfileLookup) : ILoyaltyLookup
{
    public async Task<Result<LoyaltyProgramDto>> GetLoyaltyProgramAsync(Guid tenantId, CancellationToken ct = default)
    {
        var program = await GetOrCreateProgramAsync(tenantId, ct);
        return Result<LoyaltyProgramDto>.Success(MapToProgramDto(program));
    }

    public async Task<Result<LoyaltyProgramDto>> UpdateLoyaltyProgramAsync(
        Guid tenantId,
        UpdateLoyaltyProgramRequest request,
        CancellationToken ct = default)
    {
        var program = await GetOrCreateProgramAsync(tenantId, ct);
        var updateResult = program.Update(
            request.IsEnabled,
            request.TargetStamps,
            request.RewardTitle,
            request.ProximityThreshold,
            request.AllServicesEligible,
            request.EligibleCategoryFilter);

        if (!updateResult.IsSuccess)
        {
            return Result<LoyaltyProgramDto>.Failure(updateResult.Error!);
        }

        await programRepository.SaveChangesAsync(ct);
        return Result<LoyaltyProgramDto>.Success(MapToProgramDto(program));
    }

    public async Task<Result<CustomerLoyaltySummaryDto?>> GetLoyaltySummaryByCustomerIdAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken ct = default)
    {
        var customer = await customerRepository.GetByIdAsync(tenantId, customerId, ct);
        if (customer is null)
        {
            return Result<CustomerLoyaltySummaryDto?>.Failure(new Error("customer.not_found", "Cliente não encontrado.", ErrorType.NotFound));
        }

        var program = await GetOrCreateProgramAsync(tenantId, ct);
        var account = await loyaltyRepository.GetByCustomerIdAsync(tenantId, customerId, ct);

        var summary = MapToSummaryDto(customer, account, program);
        return Result<CustomerLoyaltySummaryDto?>.Success(summary);
    }

    public async Task<Result<CustomerLoyaltySummaryDto?>> GetLoyaltySummaryByPhoneAsync(
        Guid tenantId,
        string phone,
        CancellationToken ct = default)
    {
        var normalizedPhone = Customer.NormalizePhone(phone);
        var customer = await customerRepository.GetByNormalizedPhoneAsync(tenantId, normalizedPhone, ct);
        if (customer is null)
        {
            return Result<CustomerLoyaltySummaryDto?>.Success(null);
        }

        var program = await GetOrCreateProgramAsync(tenantId, ct);
        var account = await loyaltyRepository.GetByCustomerIdAsync(tenantId, customer.Id, ct);

        var summary = MapToSummaryDto(customer, account, program);
        return Result<CustomerLoyaltySummaryDto?>.Success(summary);
    }

    public async Task<Result<IReadOnlyList<CustomerLoyaltySummaryDto>>> ListCustomerLoyaltyAccountsAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var program = await GetOrCreateProgramAsync(tenantId, ct);
        var accounts = await loyaltyRepository.ListAccountsAsync(tenantId, ct);

        var summaries = new List<CustomerLoyaltySummaryDto>();
        foreach (var account in accounts)
        {
            var customer = await customerRepository.GetByIdAsync(tenantId, account.CustomerId, ct);
            if (customer is not null)
            {
                summaries.Add(MapToSummaryDto(customer, account, program));
            }
        }

        return Result<IReadOnlyList<CustomerLoyaltySummaryDto>>.Success(summaries.AsReadOnly());
    }

    public async Task<Result<IReadOnlyList<CustomerLoyaltyTransactionDto>>> GetCustomerLoyaltyTransactionsAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken ct = default)
    {
        var account = await loyaltyRepository.GetByCustomerIdAsync(tenantId, customerId, ct);
        if (account is null)
        {
            return Result<IReadOnlyList<CustomerLoyaltyTransactionDto>>.Success(Array.Empty<CustomerLoyaltyTransactionDto>());
        }

        var txs = await loyaltyRepository.ListTransactionsAsync(tenantId, account.Id, ct);
        var dts = txs.Select(t => new CustomerLoyaltyTransactionDto(
            t.Id,
            t.Type.ToString(),
            t.Amount,
            t.BalanceAfter,
            t.WorkOrderId,
            t.WorkOrderNumber,
            t.Description,
            t.CreatedAtUtc)).ToList();

        return Result<IReadOnlyList<CustomerLoyaltyTransactionDto>>.Success(dts.AsReadOnly());
    }

    public async Task<Result<CustomerLoyaltySummaryDto>> ProcessWorkOrderLoyaltyAccrualAsync(
        Guid tenantId,
        Guid workOrderId,
        CancellationToken ct = default)
    {
        var program = await GetOrCreateProgramAsync(tenantId, ct);
        if (!program.IsEnabled)
        {
            return Result<CustomerLoyaltySummaryDto>.Failure(new Error("loyalty.disabled", "Programa de fidelidade está desativado.", ErrorType.Conflict));
        }

        var workOrder = await workOrderRepository.GetByIdAsync(tenantId, workOrderId, ct);
        if (workOrder is null)
        {
            return Result<CustomerLoyaltySummaryDto>.Failure(new Error("work_order.not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, workOrder.CustomerId, ct);
        if (customer is null)
        {
            return Result<CustomerLoyaltySummaryDto>.Failure(new Error("customer.not_found", "Cliente não encontrado.", ErrorType.NotFound));
        }

        // Check if has eligible service
        var hasEligibleService = workOrder.Items.Any(item => program.IsServiceEligible(item.ServiceCategory, item.ServiceName));
        if (!hasEligibleService)
        {
            return Result<CustomerLoyaltySummaryDto>.Failure(new Error("loyalty.no_eligible_services", "Nenhum serviço elegível para fidelidade nesta OS.", ErrorType.Validation));
        }

        // Idempotency: verify if already accrued for this work order
        var alreadyAccrued = await loyaltyRepository.HasAccrualForWorkOrderAsync(tenantId, workOrderId, ct);
        var account = await loyaltyRepository.GetByCustomerIdAsync(tenantId, customer.Id, ct);

        if (alreadyAccrued)
        {
            return Result<CustomerLoyaltySummaryDto>.Success(MapToSummaryDto(customer, account, program));
        }

        if (account is null)
        {
            var createAccResult = CustomerLoyaltyAccount.Create(tenantId, customer.Id);
            if (!createAccResult.IsSuccess)
            {
                return Result<CustomerLoyaltySummaryDto>.Failure(createAccResult.Error!);
            }
            account = createAccResult.Value!;
            await loyaltyRepository.AddAccountAsync(account, ct);
        }

        var woNumber = $"OS #{workOrder.Id.ToString()[..8].ToUpperInvariant()}";
        var creditResult = account.CreditStamps(1, workOrderId, woNumber);
        if (!creditResult.IsSuccess)
        {
            return Result<CustomerLoyaltySummaryDto>.Failure(creditResult.Error!);
        }

        await loyaltyRepository.SaveChangesAsync(ct);

        // Notify client via WhatsApp if near redemption or completed
        await NotifyCustomerOnWhatsAppAsync(tenantId, customer, account, program, ct);

        return Result<CustomerLoyaltySummaryDto>.Success(MapToSummaryDto(customer, account, program));
    }

    public async Task<Result<CustomerLoyaltyTransactionDto>> RedeemRewardAsync(
        Guid tenantId,
        RedeemLoyaltyRewardRequest request,
        CancellationToken ct = default)
    {
        var program = await GetOrCreateProgramAsync(tenantId, ct);
        if (!program.IsEnabled)
        {
            return Result<CustomerLoyaltyTransactionDto>.Failure(new Error("loyalty.disabled", "Programa de fidelidade está desativado.", ErrorType.Conflict));
        }

        var account = await loyaltyRepository.GetByCustomerIdAsync(tenantId, request.CustomerId, ct);
        if (account is null)
        {
            return Result<CustomerLoyaltyTransactionDto>.Failure(new Error("loyalty.account_not_found", "Conta de fidelidade não encontrada.", ErrorType.NotFound));
        }

        var redeemResult = account.RedeemReward(program.TargetStamps, program.RewardTitle, request.Notes);
        if (!redeemResult.IsSuccess)
        {
            return Result<CustomerLoyaltyTransactionDto>.Failure(redeemResult.Error!);
        }

        await loyaltyRepository.SaveChangesAsync(ct);

        var tx = redeemResult.Value!;
        return Result<CustomerLoyaltyTransactionDto>.Success(new CustomerLoyaltyTransactionDto(
            tx.Id,
            tx.Type.ToString(),
            tx.Amount,
            tx.BalanceAfter,
            tx.WorkOrderId,
            tx.WorkOrderNumber,
            tx.Description,
            tx.CreatedAtUtc));
    }

    public async Task<Result<CustomerLoyaltyTransactionDto>> AdjustBalanceAsync(
        Guid tenantId,
        ManualLoyaltyAdjustmentRequest request,
        string operatorName,
        CancellationToken ct = default)
    {
        var account = await loyaltyRepository.GetByCustomerIdAsync(tenantId, request.CustomerId, ct);
        if (account is null)
        {
            var createResult = CustomerLoyaltyAccount.Create(tenantId, request.CustomerId);
            if (!createResult.IsSuccess)
            {
                return Result<CustomerLoyaltyTransactionDto>.Failure(createResult.Error!);
            }
            account = createResult.Value!;
            await loyaltyRepository.AddAccountAsync(account, ct);
        }

        var adjustResult = account.AdjustBalance(request.Delta, request.Reason, operatorName);
        if (!adjustResult.IsSuccess)
        {
            return Result<CustomerLoyaltyTransactionDto>.Failure(adjustResult.Error!);
        }

        await loyaltyRepository.SaveChangesAsync(ct);

        var tx = adjustResult.Value!;
        return Result<CustomerLoyaltyTransactionDto>.Success(new CustomerLoyaltyTransactionDto(
            tx.Id,
            tx.Type.ToString(),
            tx.Amount,
            tx.BalanceAfter,
            tx.WorkOrderId,
            tx.WorkOrderNumber,
            tx.Description,
            tx.CreatedAtUtc));
    }

    private async Task<LoyaltyProgram> GetOrCreateProgramAsync(Guid tenantId, CancellationToken ct)
    {
        var program = await programRepository.GetByTenantIdAsync(tenantId, ct);
        if (program is not null)
        {
            return program;
        }

        var createResult = LoyaltyProgram.CreateDefault(tenantId);
        program = createResult.Value!;
        await programRepository.AddAsync(program, ct);
        await programRepository.SaveChangesAsync(ct);
        return program;
    }

    private async Task NotifyCustomerOnWhatsAppAsync(
        Guid tenantId,
        Customer customer,
        CustomerLoyaltyAccount account,
        LoyaltyProgram program,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(customer.Phone))
        {
            return;
        }

        var storeProfile = await storeProfileLookup.GetProfileAsync(tenantId, ct);
        var storeName = storeProfile.Value?.TradeName ?? "Lavaway";

        var remaining = account.CalculateRemaining(program.TargetStamps);
        var progressBar = BuildProgressBar(account.Balance, program.TargetStamps);

        if (remaining == 1)
        {
            var message = $"🎉 Olá, *{customer.Name}*!\n\n" +
                          $"Você acabou de ganhar *+1 selo* no seu Cartão Fidelidade da *{storeName}*!\n\n" +
                          $"🏷️ *Seu Progresso:* {progressBar} ({account.Balance}/{program.TargetStamps} selos)\n\n" +
                          $"🔥 *Falta apenas 1 serviço* para você resgatar sua recompensa: *{program.RewardTitle}*!\n\n" +
                          $"Esperamos você em breve para completar seu cartão! 🚗✨";

            await whatsAppDispatcher.DispatchTextMessageAsync(tenantId, customer.Phone, message, ct: ct);
        }
        else if (account.IsEligibleForReward(program.TargetStamps))
        {
            var message = $"🏆 Parabéns, *{customer.Name}*!\n\n" +
                          $"Você completou seu Cartão Fidelidade na *{storeName}* com *{account.Balance}/{program.TargetStamps} selos*!\n\n" +
                          $"🎁 *Sua Recompensa:* *{program.RewardTitle}*\n\n" +
                          $"Apresente esta mensagem na nossa recepção na sua próxima visita para resgatar sua recompensa gratuita! 🚗✨";

            await whatsAppDispatcher.DispatchTextMessageAsync(tenantId, customer.Phone, message, ct: ct);
        }
    }

    public static string BuildProgressBar(int current, int target)
    {
        if (target <= 0) return string.Empty;
        var ratio = Math.Clamp((double)current / target, 0.0, 1.0);
        var filledCount = (int)Math.Round(ratio * 10);
        var emptyCount = 10 - filledCount;
        return "[" + new string('■', filledCount) + new string('□', emptyCount) + "]";
    }

    private static LoyaltyProgramDto MapToProgramDto(LoyaltyProgram p) =>
        new(p.IsEnabled, p.TargetStamps, p.RewardTitle, p.ProximityThreshold, p.AllServicesEligible, p.EligibleCategoryFilter, p.UpdatedAtUtc);

    private static CustomerLoyaltySummaryDto MapToSummaryDto(Customer c, CustomerLoyaltyAccount? a, LoyaltyProgram p)
    {
        var balance = a?.Balance ?? 0;
        var remaining = p.TargetStamps > 0 ? Math.Max(0, p.TargetStamps - balance) : 0;
        var isReady = p.TargetStamps > 0 && balance >= p.TargetStamps;
        var isNear = p.TargetStamps > 0 && balance > 0 && balance < p.TargetStamps && remaining <= p.ProximityThreshold;

        return new CustomerLoyaltySummaryDto(
            a?.Id ?? Guid.Empty,
            c.Id,
            c.Name,
            c.Phone,
            balance,
            p.TargetStamps,
            remaining,
            p.RewardTitle,
            isReady,
            isNear,
            a?.LastAccrualAtUtc);
    }
}
