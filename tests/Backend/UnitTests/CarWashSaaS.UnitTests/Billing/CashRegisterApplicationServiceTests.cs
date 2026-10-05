using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class CashRegisterApplicationServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private sealed class FakeCashTransactionRepository : ICashTransactionRepository
    {
        public readonly List<CashTransaction> Transactions = [];

        public Task AddAsync(CashTransaction transaction, CancellationToken ct = default)
        {
            Transactions.Add(transaction);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CashTransaction>> ListByDateAsync(Guid tenantId, DateOnly date, CancellationToken ct = default)
        {
            var startUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var endUtc = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

            var list = Transactions
                .Where(t => t.TenantId == tenantId && t.OccurredAtUtc >= startUtc && t.OccurredAtUtc <= endUtc)
                .OrderByDescending(t => t.OccurredAtUtc)
                .ToList();

            return Task.FromResult<IReadOnlyList<CashTransaction>>(list);
        }

        public Task<IReadOnlyList<CashTransaction>> ListByPeriodAsync(Guid tenantId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
        {
            var startUtc = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var endUtc = endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

            var list = Transactions
                .Where(t => t.TenantId == tenantId && t.OccurredAtUtc >= startUtc && t.OccurredAtUtc <= endUtc)
                .OrderByDescending(t => t.OccurredAtUtc)
                .ToList();

            return Task.FromResult<IReadOnlyList<CashTransaction>>(list);
        }

        public Task<bool> ExistsForWorkOrderAsync(Guid tenantId, Guid workOrderId, CashTransactionType type, CancellationToken ct = default)
        {
            var exists = Transactions.Any(t => t.TenantId == tenantId && t.WorkOrderId == workOrderId && t.Type == type);
            return Task.FromResult(exists);
        }

        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }

    private sealed class FakeDailyCashClosingRepository : IDailyCashClosingRepository
    {
        public readonly List<DailyCashClosing> Closings = [];

        public Task AddAsync(DailyCashClosing closing, CancellationToken ct = default)
        {
            Closings.Add(closing);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(DailyCashClosing closing, CancellationToken ct = default)
        {
            var idx = Closings.FindIndex(c => c.Id == closing.Id);
            if (idx >= 0) Closings[idx] = closing;
            return Task.CompletedTask;
        }

        public Task<DailyCashClosing?> GetByDateAsync(Guid tenantId, DateOnly date, CancellationToken ct = default)
        {
            var item = Closings.FirstOrDefault(c => c.TenantId == tenantId && c.ClosingDate == date);
            return Task.FromResult(item);
        }

        public Task<IReadOnlyList<DailyCashClosing>> ListRecentAsync(Guid tenantId, int count = 30, CancellationToken ct = default)
        {
            var list = Closings.Where(c => c.TenantId == tenantId).OrderByDescending(c => c.ClosingDate).Take(count).ToList();
            return Task.FromResult<IReadOnlyList<DailyCashClosing>>(list);
        }

        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }

    private sealed class FakeSettlementService : IWorkOrderPaymentSettlementService
    {
        public bool ShouldSucceed { get; set; } = true;
        public int CallCount { get; private set; }

        public Task<Result<WorkOrderPaymentSettlementDto>> SettlePaymentAsync(
            Guid tenantId,
            Guid workOrderId,
            decimal paidAmount,
            string paymentMethod,
            string transactionReference,
            DateTimeOffset paidAtUtc,
            CancellationToken ct = default)
        {
            CallCount++;
            if (!ShouldSucceed)
            {
                return Task.FromResult(Result<WorkOrderPaymentSettlementDto>.Failure(new Error("error", "Erro ao baixar OS", ErrorType.Validation)));
            }

            return Task.FromResult(Result<WorkOrderPaymentSettlementDto>.Success(new WorkOrderPaymentSettlementDto(
                workOrderId,
                tenantId,
                paidAmount,
                paidAmount,
                paymentMethod,
                true,
                paidAtUtc,
                transactionReference)));
        }
    }

    [Fact]
    public async Task RegisterWorkOrderPayment_WithCash_SettlesWorkOrderAndRecordsIncome()
    {
        var cashRepo = new FakeCashTransactionRepository();
        var closingRepo = new FakeDailyCashClosingRepository();
        var settlementService = new FakeSettlementService();
        var service = new CashRegisterApplicationService(cashRepo, closingRepo, settlementService);

        var workOrderId = Guid.NewGuid();
        var request = new RegisterWorkOrderPaymentRequest(
            workOrderId,
            120.00m,
            PaymentMethodConstants.Cash,
            CashReceived: 150.00m,
            Notes: "Troco de R$ 30");

        var result = await service.RegisterWorkOrderPaymentAsync(_tenantId, request, _userId, "Operador Lucas");

        Assert.True(result.IsSuccess);
        var tx = result.Value!;
        Assert.Equal(120.00m, tx.Amount);
        Assert.Equal(PaymentMethodConstants.Cash, tx.PaymentMethod);
        Assert.Equal("Operador Lucas", tx.RegisteredByUserName);
        Assert.Equal(1, settlementService.CallCount);
        Assert.Single(cashRepo.Transactions);
        Assert.Equal(120.00m, cashRepo.Transactions[0].Amount);
    }

    [Fact]
    public async Task RegisterWorkOrderPayment_WithInsufficientCash_ReturnsValidationError()
    {
        var cashRepo = new FakeCashTransactionRepository();
        var closingRepo = new FakeDailyCashClosingRepository();
        var settlementService = new FakeSettlementService();
        var service = new CashRegisterApplicationService(cashRepo, closingRepo, settlementService);

        var workOrderId = Guid.NewGuid();
        var request = new RegisterWorkOrderPaymentRequest(
            workOrderId,
            100.00m,
            PaymentMethodConstants.Cash,
            CashReceived: 80.00m);

        var result = await service.RegisterWorkOrderPaymentAsync(_tenantId, request, _userId, "Operador");

        Assert.False(result.IsSuccess);
        Assert.Equal("cash_register.insufficient_cash", result.Error!.Code);
        Assert.Equal(0, settlementService.CallCount);
        Assert.Empty(cashRepo.Transactions);
    }

    [Fact]
    public async Task RegisterWorkOrderPayment_WithCreditCard_CallsSettlementAndSavesIncome()
    {
        var cashRepo = new FakeCashTransactionRepository();
        var closingRepo = new FakeDailyCashClosingRepository();
        var settlementService = new FakeSettlementService();
        var service = new CashRegisterApplicationService(cashRepo, closingRepo, settlementService);

        var workOrderId = Guid.NewGuid();
        var request = new RegisterWorkOrderPaymentRequest(
            workOrderId,
            250.00m,
            PaymentMethodConstants.CreditCard,
            ReferenceNumber: "NSU998877");

        var result = await service.RegisterWorkOrderPaymentAsync(_tenantId, request, _userId, "Operador");

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentMethodConstants.CreditCard, result.Value!.PaymentMethod);
        Assert.Equal("NSU998877", result.Value!.ExternalReference);
        Assert.Equal(1, settlementService.CallCount);
        Assert.Single(cashRepo.Transactions);
    }

    [Fact]
    public async Task RecordCashMovement_Bleed_SavesBleedTransaction()
    {
        var cashRepo = new FakeCashTransactionRepository();
        var closingRepo = new FakeDailyCashClosingRepository();
        var settlementService = new FakeSettlementService();
        var service = new CashRegisterApplicationService(cashRepo, closingRepo, settlementService);

        var request = new CreateCashMovementRequest(
            CashTransactionTypeConstants.Bleed,
            60.00m,
            "Compra de café e copos descartáveis");

        var result = await service.RecordCashMovementAsync(_tenantId, request, _userId, "Gerente");

        Assert.True(result.IsSuccess);
        Assert.Equal(CashTransactionTypeConstants.Bleed, result.Value!.Type);
        Assert.Equal(60.00m, result.Value!.Amount);
        Assert.Single(cashRepo.Transactions);
        Assert.Equal(CashTransactionType.Bleed, cashRepo.Transactions[0].Type);
    }

    [Fact]
    public async Task RecordCashMovement_Supply_SavesSupplyTransaction()
    {
        var cashRepo = new FakeCashTransactionRepository();
        var closingRepo = new FakeDailyCashClosingRepository();
        var settlementService = new FakeSettlementService();
        var service = new CashRegisterApplicationService(cashRepo, closingRepo, settlementService);

        var request = new CreateCashMovementRequest(
            CashTransactionTypeConstants.Supply,
            150.00m,
            "Aporte de troco");

        var result = await service.RecordCashMovementAsync(_tenantId, request, _userId, "Gerente");

        Assert.True(result.IsSuccess);
        Assert.Equal(CashTransactionTypeConstants.Supply, result.Value!.Type);
        Assert.Equal(150.00m, result.Value!.Amount);
        Assert.Single(cashRepo.Transactions);
        Assert.Equal(CashTransactionType.Supply, cashRepo.Transactions[0].Type);
    }

    [Fact]
    public async Task GetDailySummary_AggregatesTotalsByMethodCorrectly()
    {
        var cashRepo = new FakeCashTransactionRepository();
        var closingRepo = new FakeDailyCashClosingRepository();
        var settlementService = new FakeSettlementService();
        var service = new CashRegisterApplicationService(cashRepo, closingRepo, settlementService);

        var date = new DateOnly(2026, 10, 5);
        var now = date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);

        cashRepo.Transactions.AddRange([
            CashTransaction.CreateIncome(_tenantId, 100m, PaymentMethodConstants.Pix, "Pix 1", now).Value!,
            CashTransaction.CreateIncome(_tenantId, 50m, PaymentMethodConstants.Cash, "Cash 1", now).Value!,
            CashTransaction.CreateIncome(_tenantId, 80m, PaymentMethodConstants.CreditCard, "Card 1", now).Value!,
            CashTransaction.CreateIncome(_tenantId, 30m, PaymentMethodConstants.DebitCard, "Debit 1", now).Value!,
            CashTransaction.CreateSupply(_tenantId, 100m, "Troco", now).Value!,
            CashTransaction.CreateBleed(_tenantId, 20m, "Sangria", now).Value!
        ]);

        var result = await service.GetDailySummaryAsync(_tenantId, date);

        Assert.True(result.IsSuccess);
        var summary = result.Value!;
        Assert.False(summary.IsClosed);
        Assert.Equal(260.00m, summary.TotalIncome);
        Assert.Equal(100.00m, summary.TotalPix);
        Assert.Equal(50.00m, summary.TotalCash);
        Assert.Equal(80.00m, summary.TotalCreditCard);
        Assert.Equal(30.00m, summary.TotalDebitCard);
        Assert.Equal(100.00m, summary.TotalSupplies);
        Assert.Equal(20.00m, summary.TotalBleeds);
        Assert.Equal(130.00m, summary.ExpectedCashInDrawer);
        Assert.Equal(6, summary.TotalTransactionsCount);
    }

    [Fact]
    public async Task CloseDailyCash_WhenNotClosed_ClosesAndSaves()
    {
        var cashRepo = new FakeCashTransactionRepository();
        var closingRepo = new FakeDailyCashClosingRepository();
        var settlementService = new FakeSettlementService();
        var service = new CashRegisterApplicationService(cashRepo, closingRepo, settlementService);

        var date = new DateOnly(2026, 10, 5);
        var request = new CloseDailyCashRequest(date, ActualCashInDrawer: 130.00m, Notes: "Conferido");

        var result = await service.CloseDailyCashAsync(_tenantId, request, _userId, "Gestor");

        Assert.True(result.IsSuccess);
        Assert.Equal("Closed", result.Value!.Status);
        Assert.Equal(130.00m, result.Value!.ActualCashInDrawer);
        Assert.Single(closingRepo.Closings);
    }

    [Fact]
    public async Task CloseDailyCash_WhenAlreadyClosed_ReturnsConflict()
    {
        var cashRepo = new FakeCashTransactionRepository();
        var closingRepo = new FakeDailyCashClosingRepository();
        var settlementService = new FakeSettlementService();
        var service = new CashRegisterApplicationService(cashRepo, closingRepo, settlementService);

        var date = new DateOnly(2026, 10, 5);
        var existing = DailyCashClosing.Close(_tenantId, date, _userId, "Gestor", 100m, 50m, 50m, 0m, 0m, 0m, 0m).Value!;
        closingRepo.Closings.Add(existing);

        var request = new CloseDailyCashRequest(date);
        var result = await service.CloseDailyCashAsync(_tenantId, request, _userId, "Gestor");

        Assert.False(result.IsSuccess);
        Assert.Equal("cash_register.already_closed", result.Error!.Code);
    }
}
