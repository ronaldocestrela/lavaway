using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class LoyaltyApplicationServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _workOrderId = Guid.NewGuid();

    private readonly InMemoryLoyaltyProgramRepository _programRepo = new();
    private readonly InMemoryCustomerLoyaltyRepository _loyaltyRepo = new();
    private readonly InMemoryWorkOrderRepository _workOrderRepo = new();
    private readonly InMemoryCustomerRepository _customerRepo = new();
    private readonly InMemoryWhatsAppDispatcher _whatsAppDispatcher = new();
    private readonly FakeStoreProfileLookup _storeProfileLookup = new();

    private readonly LoyaltyApplicationService _service;

    public LoyaltyApplicationServiceTests()
    {
        _service = new LoyaltyApplicationService(
            _programRepo,
            _loyaltyRepo,
            _workOrderRepo,
            _customerRepo,
            _whatsAppDispatcher,
            _storeProfileLookup);
    }

    [Fact]
    public async Task ProcessWorkOrderLoyaltyAccrualAsync_ShouldCreditStampAndNotifyWhenOneServiceRemaining()
    {
        // Program with 10 target stamps, proximity threshold 1
        var program = LoyaltyProgram.CreateDefault(_tenantId).Value!;
        await _programRepo.AddAsync(program);

        // Customer with 8 stamps currently
        var customer = Customer.Create(_tenantId, "Rodrigo Silva", "11988887777").Value!;
        await _customerRepo.AddAsync(customer);

        var account = CustomerLoyaltyAccount.Create(_tenantId, customer.Id).Value!;
        account.CreditStamps(8, Guid.NewGuid(), "OS-ANT");
        await _loyaltyRepo.AddAccountAsync(account);

        // Work order with eligible item
        var item = WorkOrderItem.Create(_tenantId, Guid.NewGuid(), "Lavagem Completa", 70m, 45, 1, "Lavagem").Value!;
        var workOrder = WorkOrder.Create(_tenantId, customer.Id, Guid.NewGuid(), [item]).Value!;
        _workOrderRepo.Add(workOrder);

        // Action
        var result = await _service.ProcessWorkOrderLoyaltyAccrualAsync(_tenantId, workOrder.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var updatedAccount = await _loyaltyRepo.GetByCustomerIdAsync(_tenantId, customer.Id);
        Assert.NotNull(updatedAccount);
        Assert.Equal(9, updatedAccount.Balance);
        Assert.Equal(1, updatedAccount.CalculateRemaining(10));
        Assert.True(updatedAccount.IsNearRedemption(10, 1));

        // WhatsApp notification should be dispatched informing 1 remaining service!
        Assert.Single(_whatsAppDispatcher.DispatchedMessages);
        var msg = _whatsAppDispatcher.DispatchedMessages[0];
        Assert.Equal(customer.Phone, msg.Phone);
        Assert.Contains("Falta apenas 1 serviço", msg.Text);
    }

    [Fact]
    public async Task ProcessWorkOrderLoyaltyAccrualAsync_ShouldBeIdempotentForSameWorkOrder()
    {
        var program = LoyaltyProgram.CreateDefault(_tenantId).Value!;
        await _programRepo.AddAsync(program);

        var customer = Customer.Create(_tenantId, "Rodrigo", "11999998888").Value!;
        await _customerRepo.AddAsync(customer);

        var account = CustomerLoyaltyAccount.Create(_tenantId, customer.Id).Value!;
        await _loyaltyRepo.AddAccountAsync(account);

        var workOrder = WorkOrder.Create(_tenantId, customer.Id, Guid.NewGuid(), [
            WorkOrderItem.Create(_tenantId, Guid.NewGuid(), "Lavagem", 50m, 30, 1, "Lavagem").Value!
        ]).Value!;
        _workOrderRepo.Add(workOrder);

        // First call
        var firstResult = await _service.ProcessWorkOrderLoyaltyAccrualAsync(_tenantId, workOrder.Id);
        Assert.True(firstResult.IsSuccess);
        Assert.Equal(1, account.Balance);
        var initialMsgCount = _whatsAppDispatcher.DispatchedMessages.Count;

        // Second call (idempotent!)
        var secondResult = await _service.ProcessWorkOrderLoyaltyAccrualAsync(_tenantId, workOrder.Id);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(1, account.Balance);
        Assert.Equal(initialMsgCount, _whatsAppDispatcher.DispatchedMessages.Count);
    }

    [Fact]
    public async Task ProcessWorkOrderLoyaltyAccrualAsync_ShouldNotifyWhenTargetReached()
    {
        var program = LoyaltyProgram.CreateDefault(_tenantId).Value!;
        await _programRepo.AddAsync(program);

        var customer = Customer.Create(_tenantId, "Lucas", "11977776666").Value!;
        await _customerRepo.AddAsync(customer);

        var account = CustomerLoyaltyAccount.Create(_tenantId, customer.Id).Value!;
        account.CreditStamps(9, Guid.NewGuid(), "OS-ANT");
        await _loyaltyRepo.AddAccountAsync(account);

        var workOrder = WorkOrder.Create(_tenantId, customer.Id, Guid.NewGuid(), [
            WorkOrderItem.Create(_tenantId, Guid.NewGuid(), "Lavagem", 50m, 30, 1, "Lavagem").Value!
        ]).Value!;
        _workOrderRepo.Add(workOrder);

        var result = await _service.ProcessWorkOrderLoyaltyAccrualAsync(_tenantId, workOrder.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, account.Balance);
        Assert.True(account.IsEligibleForReward(10));

        Assert.Single(_whatsAppDispatcher.DispatchedMessages);
        Assert.Contains("completou seu Cartão Fidelidade", _whatsAppDispatcher.DispatchedMessages[0].Text);
    }

    [Fact]
    public async Task RedeemRewardAsync_ShouldDebitStampsWhenEligible()
    {
        var program = LoyaltyProgram.CreateDefault(_tenantId).Value!;
        await _programRepo.AddAsync(program);

        var customer = Customer.Create(_tenantId, "Carlos", "11955554444").Value!;
        await _customerRepo.AddAsync(customer);

        var account = CustomerLoyaltyAccount.Create(_tenantId, customer.Id).Value!;
        account.CreditStamps(10, Guid.NewGuid(), "OS-1");
        await _loyaltyRepo.AddAccountAsync(account);

        var request = new RedeemLoyaltyRewardRequest(customer.Id, "Resgate balcão");
        var result = await _service.RedeemRewardAsync(_tenantId, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, account.Balance);
        Assert.Equal(10, account.TotalRedeemed);
    }

    [Fact]
    public async Task RedeemRewardAsync_ShouldFailWhenBalanceIsInsufficient()
    {
        var program = LoyaltyProgram.CreateDefault(_tenantId).Value!;
        await _programRepo.AddAsync(program);

        var customer = Customer.Create(_tenantId, "Carlos", "11955554444").Value!;
        await _customerRepo.AddAsync(customer);

        var account = CustomerLoyaltyAccount.Create(_tenantId, customer.Id).Value!;
        account.CreditStamps(9, Guid.NewGuid(), "OS-1");
        await _loyaltyRepo.AddAccountAsync(account);

        var request = new RedeemLoyaltyRewardRequest(customer.Id, "Resgate balcão");
        var result = await _service.RedeemRewardAsync(_tenantId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal("loyalty.insufficient_balance", result.Error!.Code);
    }

    [Fact]
    public async Task AdjustBalanceAsync_ShouldRecordAdjustmentAndPersist()
    {
        var customer = Customer.Create(_tenantId, "Carlos", "11955554444").Value!;
        await _customerRepo.AddAsync(customer);

        var account = CustomerLoyaltyAccount.Create(_tenantId, customer.Id).Value!;
        account.CreditStamps(5, Guid.NewGuid(), "OS-1");
        await _loyaltyRepo.AddAccountAsync(account);

        var request = new ManualLoyaltyAdjustmentRequest(customer.Id, -2, "Correção de lançamento manual");
        var result = await _service.AdjustBalanceAsync(_tenantId, request, "Administrador");

        Assert.True(result.IsSuccess);
        Assert.Equal(3, account.Balance);
    }

    // --- Fakes ---

    private sealed class InMemoryLoyaltyProgramRepository : ILoyaltyProgramRepository
    {
        private LoyaltyProgram? _program;

        public Task<LoyaltyProgram?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(_program?.TenantId == tenantId ? _program : null);

        public Task AddAsync(LoyaltyProgram program, CancellationToken ct = default)
        {
            _program = program;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemoryCustomerLoyaltyRepository : ICustomerLoyaltyRepository
    {
        private readonly List<CustomerLoyaltyAccount> _accounts = [];

        public Task<CustomerLoyaltyAccount?> GetByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default) =>
            Task.FromResult(_accounts.FirstOrDefault(a => a.TenantId == tenantId && a.CustomerId == customerId));

        public Task<CustomerLoyaltyAccount?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(_accounts.FirstOrDefault(a => a.TenantId == tenantId && a.Id == id));

        public Task<bool> HasAccrualForWorkOrderAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default) =>
            Task.FromResult(_accounts
                .Where(a => a.TenantId == tenantId)
                .SelectMany(a => a.Transactions)
                .Any(t => t.Type == LoyaltyTransactionType.Accrual && t.WorkOrderId == workOrderId));

        public Task<IReadOnlyList<CustomerLoyaltyAccount>> ListAccountsAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CustomerLoyaltyAccount>>(_accounts.Where(a => a.TenantId == tenantId).ToList().AsReadOnly());

        public Task<IReadOnlyList<LoyaltyTransaction>> ListTransactionsAsync(Guid tenantId, Guid accountId, CancellationToken ct = default)
        {
            var acc = _accounts.FirstOrDefault(a => a.TenantId == tenantId && a.Id == accountId);
            return Task.FromResult<IReadOnlyList<LoyaltyTransaction>>(acc?.Transactions.ToList() ?? new List<LoyaltyTransaction>());
        }

        public Task AddAccountAsync(CustomerLoyaltyAccount account, CancellationToken ct = default)
        {
            _accounts.Add(account);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemoryWorkOrderRepository : IWorkOrderRepository
    {
        private readonly List<WorkOrder> _orders = [];

        public void Add(WorkOrder order) => _orders.Add(order);

        public Task<WorkOrder?> GetByIdAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default) =>
            Task.FromResult(_orders.FirstOrDefault(o => o.TenantId == tenantId && o.Id == workOrderId));

        public Task AddAsync(WorkOrder workOrder, CancellationToken ct = default)
        {
            _orders.Add(workOrder);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<WorkOrder>> ListActiveAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>(_orders.Where(o => o.TenantId == tenantId).ToList());

        public Task<IReadOnlyCollection<WorkOrder>> ListRecentAsync(Guid tenantId, int limit = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>(_orders.Where(o => o.TenantId == tenantId).Take(limit).ToList());

        public Task<IReadOnlyCollection<WorkOrder>> GetWorkOrdersPendingSurveyAsync(Guid tenantId, DateTimeOffset cutoff, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>([]);

        public Task<WorkOrder?> GetLatestCompletedOrderByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default) =>
            Task.FromResult<WorkOrder?>(null);

        public Task<WorkOrder?> GetActiveOrderByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default) =>
            Task.FromResult<WorkOrder?>(null);

        public Task<IReadOnlyCollection<WorkOrder>> ListSurveysAsync(Guid tenantId, int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>([]);

        public void Update(WorkOrder workOrder) { }
    }

    private sealed class InMemoryCustomerRepository : ICustomerRepository
    {
        private readonly List<Customer> _customers = [];

        public Task<Customer?> GetByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default) =>
            Task.FromResult(_customers.FirstOrDefault(c => c.TenantId == tenantId && c.Id == customerId));

        public Task<Customer?> GetByNormalizedPhoneAsync(Guid tenantId, string normalizedPhone, CancellationToken ct = default) =>
            Task.FromResult(_customers.FirstOrDefault(c => c.TenantId == tenantId && c.NormalizedPhone == normalizedPhone));

        public Task AddAsync(Customer customer, CancellationToken ct = default)
        {
            _customers.Add(customer);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryWhatsAppDispatcher : IOutboundWhatsAppDispatcher
    {
        public List<(string Phone, string Text)> DispatchedMessages { get; } = [];

        public Task<Result<WhatsAppMessageDto>> DispatchTextMessageAsync(
            Guid tenantId,
            string recipientPhone,
            string messageText,
            string? idempotencyKey = null,
            CancellationToken ct = default)
        {
            DispatchedMessages.Add((recipientPhone, messageText));
            return Task.FromResult(Result<WhatsAppMessageDto>.Success(new WhatsAppMessageDto(
                Guid.NewGuid(),
                recipientPhone,
                messageText,
                WhatsAppMessageStatusConstants.Sent,
                null,
                1,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                null)));
        }

        public Task<Result<WhatsAppMessageDto>> DispatchMediaMessageAsync(
            Guid tenantId,
            string recipientPhone,
            string caption,
            string mediaType,
            string mediaUrlOrBase64,
            string mediaMimeType,
            string mediaFileName,
            string? idempotencyKey = null,
            CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Result<IReadOnlyList<WhatsAppMessageDto>>> GetRecentMessagesAsync(Guid tenantId, int count = 20, CancellationToken ct = default) =>
            Task.FromResult(Result<IReadOnlyList<WhatsAppMessageDto>>.Success(Array.Empty<WhatsAppMessageDto>()));
    }

    private sealed class FakeStoreProfileLookup : ITenantStoreProfileLookup
    {
        public Task<Result<StoreProfileDto>> GetProfileAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<StoreProfileDto>.Success(new StoreProfileDto(
                Guid.NewGuid(),
                tenantId,
                "Lava Jato Premium LTDA",
                "Lava Jato Premium",
                "12345678000199",
                "11988887777",
                "Rua das Flores, 123",
                "São Paulo",
                "SP",
                "01001-000",
                null,
                null,
                null)));
    }
}
