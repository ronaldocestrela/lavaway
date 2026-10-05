using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Application;

public sealed class CashRegisterApplicationService(
    ICashTransactionRepository cashTransactionRepository,
    IDailyCashClosingRepository dailyCashClosingRepository,
    IWorkOrderPaymentSettlementService workOrderPaymentSettlementService,
    ISubscriptionUsageService? subscriptionUsageService = null)
{
    public async Task<Result<CashTransactionDto>> RegisterWorkOrderPaymentAsync(
        Guid tenantId,
        RegisterWorkOrderPaymentRequest request,
        Guid? userId,
        string? userName,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CashTransactionDto>.Failure(new Error("cash_register.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (request.WorkOrderId == Guid.Empty)
        {
            return Result<CashTransactionDto>.Failure(new Error("cash_register.work_order_required", "Ordem de serviço é obrigatória.", ErrorType.Validation));
        }

        if (request.PaidAmount <= 0)
        {
            return Result<CashTransactionDto>.Failure(new Error("cash_register.amount_invalid", "O valor pago deve ser maior que zero.", ErrorType.Validation));
        }

        if (!PaymentMethodConstants.IsValid(request.PaymentMethod))
        {
            return Result<CashTransactionDto>.Failure(new Error("cash_register.payment_method_invalid", $"Forma de pagamento '{request.PaymentMethod}' inválida.", ErrorType.Validation));
        }

        if (string.Equals(request.PaymentMethod, PaymentMethodConstants.Cash, StringComparison.OrdinalIgnoreCase) &&
            request.CashReceived.HasValue && request.CashReceived.Value < request.PaidAmount)
        {
            return Result<CashTransactionDto>.Failure(new Error(
                "cash_register.insufficient_cash",
                $"Valor recebido em dinheiro (R$ {request.CashReceived:N2}) é menor que o valor a pagar (R$ {request.PaidAmount:N2}).",
                ErrorType.Validation));
        }

        var transactionRef = string.IsNullOrWhiteSpace(request.ReferenceNumber)
            ? Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()
            : request.ReferenceNumber.Trim();

        var nowUtc = DateTimeOffset.UtcNow;

        if (string.Equals(request.PaymentMethod, PaymentMethodConstants.SubscriptionCredit, StringComparison.OrdinalIgnoreCase) &&
            subscriptionUsageService is not null)
        {
            var consumeResult = await subscriptionUsageService.ConsumeCreditForWorkOrderAsync(
                tenantId,
                new ConsumeSubscriptionCreditRequest(request.WorkOrderId, request.Plate ?? string.Empty, "Baixa por Crédito de Assinatura", request.Notes),
                ct);

            if (!consumeResult.IsSuccess)
            {
                return Result<CashTransactionDto>.Failure(consumeResult.Error!);
            }

            transactionRef = $"CRED-{consumeResult.Value!.UsageId.ToString()[..8].ToUpperInvariant()}";
        }

        var settlementResult = await workOrderPaymentSettlementService.SettlePaymentAsync(
            tenantId,
            request.WorkOrderId,
            request.PaidAmount,
            request.PaymentMethod,
            transactionRef,
            nowUtc,
            ct);

        if (!settlementResult.IsSuccess)
        {
            return Result<CashTransactionDto>.Failure(settlementResult.Error!);
        }

        var description = $"Baixa OS #{request.WorkOrderId.ToString()[..8]} - {PaymentMethodConstants.ToDisplayName(request.PaymentMethod)}";
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            description = $"{description} ({request.Notes.Trim()})";
        }

        var createResult = CashTransaction.CreateIncome(
            tenantId,
            request.PaidAmount,
            request.PaymentMethod,
            description,
            nowUtc,
            request.WorkOrderId,
            userId,
            userName,
            transactionRef);

        if (!createResult.IsSuccess)
        {
            return Result<CashTransactionDto>.Failure(createResult.Error!);
        }

        var transaction = createResult.Value!;
        await cashTransactionRepository.AddAsync(transaction, ct);
        await cashTransactionRepository.SaveChangesAsync(ct);

        return Result<CashTransactionDto>.Success(MapToDto(transaction));
    }

    public async Task<Result<CashTransactionDto>> RecordCashMovementAsync(
        Guid tenantId,
        CreateCashMovementRequest request,
        Guid? userId,
        string? userName,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CashTransactionDto>.Failure(new Error("cash_register.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (request.Amount <= 0)
        {
            return Result<CashTransactionDto>.Failure(new Error("cash_register.amount_invalid", "O valor da movimentação deve ser maior que zero.", ErrorType.Validation));
        }

        Result<CashTransaction> result;
        if (string.Equals(request.Type, CashTransactionTypeConstants.Bleed, StringComparison.OrdinalIgnoreCase))
        {
            result = CashTransaction.CreateBleed(tenantId, request.Amount, request.Description, registeredByUserId: userId, registeredByUserName: userName);
        }
        else if (string.Equals(request.Type, CashTransactionTypeConstants.Supply, StringComparison.OrdinalIgnoreCase))
        {
            result = CashTransaction.CreateSupply(tenantId, request.Amount, request.Description, registeredByUserId: userId, registeredByUserName: userName);
        }
        else
        {
            return Result<CashTransactionDto>.Failure(new Error("cash_register.type_invalid", $"Tipo de movimentação '{request.Type}' inválido. Use 'Bleed' (Sangria) ou 'Supply' (Aporte).", ErrorType.Validation));
        }

        if (!result.IsSuccess)
        {
            return Result<CashTransactionDto>.Failure(result.Error!);
        }

        var transaction = result.Value!;
        await cashTransactionRepository.AddAsync(transaction, ct);
        await cashTransactionRepository.SaveChangesAsync(ct);

        return Result<CashTransactionDto>.Success(MapToDto(transaction));
    }

    public async Task<Result<DailyCashSummaryDto>> GetDailySummaryAsync(
        Guid tenantId,
        DateOnly date,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<DailyCashSummaryDto>.Failure(new Error("cash_register.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var transactions = await cashTransactionRepository.ListByDateAsync(tenantId, date, ct);
        var closing = await dailyCashClosingRepository.GetByDateAsync(tenantId, date, ct);

        decimal totalIncome = 0m;
        decimal totalPix = 0m;
        decimal totalCash = 0m;
        decimal totalCreditCard = 0m;
        decimal totalDebitCard = 0m;
        decimal totalSupplies = 0m;
        decimal totalBleeds = 0m;

        foreach (var t in transactions)
        {
            if (t.Type == CashTransactionType.Income)
            {
                totalIncome += t.Amount;
                if (string.Equals(t.PaymentMethod, PaymentMethodConstants.Pix, StringComparison.OrdinalIgnoreCase))
                    totalPix += t.Amount;
                else if (string.Equals(t.PaymentMethod, PaymentMethodConstants.Cash, StringComparison.OrdinalIgnoreCase))
                    totalCash += t.Amount;
                else if (string.Equals(t.PaymentMethod, PaymentMethodConstants.CreditCard, StringComparison.OrdinalIgnoreCase))
                    totalCreditCard += t.Amount;
                else if (string.Equals(t.PaymentMethod, PaymentMethodConstants.DebitCard, StringComparison.OrdinalIgnoreCase))
                    totalDebitCard += t.Amount;
            }
            else if (t.Type == CashTransactionType.Supply)
            {
                totalSupplies += t.Amount;
            }
            else if (t.Type == CashTransactionType.Bleed)
            {
                totalBleeds += t.Amount;
            }
        }

        var expectedCashInDrawer = totalSupplies + totalCash - totalBleeds;

        var isClosed = closing is not null && closing.Status == DailyCashClosingStatus.Closed;

        var summary = new DailyCashSummaryDto(
            date,
            isClosed,
            closing?.ClosedAtUtc,
            closing?.ClosedByUserName,
            totalIncome,
            totalPix,
            totalCash,
            totalCreditCard,
            totalDebitCard,
            totalSupplies,
            totalBleeds,
            expectedCashInDrawer,
            closing?.ActualCashInDrawer,
            closing?.CashDifference,
            closing?.Notes,
            transactions.Count,
            transactions.Select(MapToDto).ToList());

        return Result<DailyCashSummaryDto>.Success(summary);
    }

    public async Task<Result<DailyCashClosingDto>> CloseDailyCashAsync(
        Guid tenantId,
        CloseDailyCashRequest request,
        Guid userId,
        string userName,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<DailyCashClosingDto>.Failure(new Error("cash_register.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var existingClosing = await dailyCashClosingRepository.GetByDateAsync(tenantId, request.ClosingDate, ct);
        if (existingClosing is not null && existingClosing.Status == DailyCashClosingStatus.Closed)
        {
            return Result<DailyCashClosingDto>.Failure(new Error("cash_register.already_closed", $"O caixa do dia {request.ClosingDate:dd/MM/yyyy} já foi fechado.", ErrorType.Conflict));
        }

        var transactions = await cashTransactionRepository.ListByDateAsync(tenantId, request.ClosingDate, ct);

        decimal totalIncome = 0m;
        decimal totalPix = 0m;
        decimal totalCash = 0m;
        decimal totalCreditCard = 0m;
        decimal totalDebitCard = 0m;
        decimal totalSupplies = 0m;
        decimal totalBleeds = 0m;

        foreach (var t in transactions)
        {
            if (t.Type == CashTransactionType.Income)
            {
                totalIncome += t.Amount;
                if (string.Equals(t.PaymentMethod, PaymentMethodConstants.Pix, StringComparison.OrdinalIgnoreCase))
                    totalPix += t.Amount;
                else if (string.Equals(t.PaymentMethod, PaymentMethodConstants.Cash, StringComparison.OrdinalIgnoreCase))
                    totalCash += t.Amount;
                else if (string.Equals(t.PaymentMethod, PaymentMethodConstants.CreditCard, StringComparison.OrdinalIgnoreCase))
                    totalCreditCard += t.Amount;
                else if (string.Equals(t.PaymentMethod, PaymentMethodConstants.DebitCard, StringComparison.OrdinalIgnoreCase))
                    totalDebitCard += t.Amount;
            }
            else if (t.Type == CashTransactionType.Supply)
            {
                totalSupplies += t.Amount;
            }
            else if (t.Type == CashTransactionType.Bleed)
            {
                totalBleeds += t.Amount;
            }
        }

        var closeResult = DailyCashClosing.Close(
            tenantId,
            request.ClosingDate,
            userId,
            userName,
            totalIncome,
            totalPix,
            totalCash,
            totalCreditCard,
            totalDebitCard,
            totalSupplies,
            totalBleeds,
            request.ActualCashInDrawer,
            request.Notes);

        if (!closeResult.IsSuccess)
        {
            return Result<DailyCashClosingDto>.Failure(closeResult.Error!);
        }

        var closing = closeResult.Value!;
        if (existingClosing is not null)
        {
            await dailyCashClosingRepository.UpdateAsync(closing, ct);
        }
        else
        {
            await dailyCashClosingRepository.AddAsync(closing, ct);
        }

        await dailyCashClosingRepository.SaveChangesAsync(ct);
        return Result<DailyCashClosingDto>.Success(MapToClosingDto(closing));
    }

    public async Task<Result<IReadOnlyList<DailyCashClosingDto>>> ListRecentClosingsAsync(
        Guid tenantId,
        int count = 30,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyList<DailyCashClosingDto>>.Failure(new Error("cash_register.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var closings = await dailyCashClosingRepository.ListRecentAsync(tenantId, count, ct);
        var dtos = closings.Select(MapToClosingDto).ToList();
        return Result<IReadOnlyList<DailyCashClosingDto>>.Success(dtos);
    }

    private static CashTransactionDto MapToDto(CashTransaction t) => new(
        t.Id,
        t.TenantId,
        t.WorkOrderId,
        t.Type.ToString(),
        t.PaymentMethod,
        t.Amount,
        t.Description,
        t.OccurredAtUtc,
        t.RegisteredByUserId,
        t.RegisteredByUserName,
        t.ExternalReference);

    private static DailyCashClosingDto MapToClosingDto(DailyCashClosing c) => new(
        c.Id,
        c.TenantId,
        c.ClosingDate,
        c.ClosedAtUtc,
        c.ClosedByUserId,
        c.ClosedByUserName,
        c.TotalIncome,
        c.TotalPix,
        c.TotalCash,
        c.TotalCreditCard,
        c.TotalDebitCard,
        c.TotalSupplies,
        c.TotalBleeds,
        c.ExpectedCashInDrawer,
        c.ActualCashInDrawer,
        c.CashDifference,
        c.Status.ToString(),
        c.Notes);
}
