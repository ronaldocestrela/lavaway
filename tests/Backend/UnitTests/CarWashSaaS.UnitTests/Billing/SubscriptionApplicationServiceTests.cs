using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class SubscriptionApplicationServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeSubscriptionPlanRepository _planRepository = new();
    private readonly FakeCustomerSubscriptionRepository _subscriptionRepository = new();
    private readonly FakeRecurringGatewayProvider _gatewayProvider = new();
    private readonly SubscriptionApplicationService _service;

    public SubscriptionApplicationServiceTests()
    {
        _service = new SubscriptionApplicationService(
            _planRepository,
            _subscriptionRepository,
            _gatewayProvider);
    }

    private sealed class FakeSubscriptionPlanRepository : ISubscriptionPlanRepository
    {
        public readonly List<SubscriptionPlan> Plans = [];

        public Task AddAsync(SubscriptionPlan plan, CancellationToken ct = default)
        {
            Plans.Add(plan);
            return Task.CompletedTask;
        }

        public Task<SubscriptionPlan?> GetByIdAsync(Guid tenantId, Guid planId, CancellationToken ct = default)
        {
            var plan = Plans.FirstOrDefault(p => p.TenantId == tenantId && p.Id == planId);
            return Task.FromResult(plan);
        }

        public Task<IReadOnlyList<SubscriptionPlan>> ListAllAsync(Guid tenantId, CancellationToken ct = default)
        {
            var list = Plans.Where(p => p.TenantId == tenantId).ToList();
            return Task.FromResult<IReadOnlyList<SubscriptionPlan>>(list);
        }

        public Task<IReadOnlyList<SubscriptionPlan>> ListActiveAsync(Guid tenantId, CancellationToken ct = default)
        {
            var list = Plans.Where(p => p.TenantId == tenantId && p.IsActive).ToList();
            return Task.FromResult<IReadOnlyList<SubscriptionPlan>>(list);
        }

        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }

    private sealed class FakeCustomerSubscriptionRepository : ICustomerSubscriptionRepository
    {
        public readonly List<CustomerSubscription> Subscriptions = [];

        public Task AddAsync(CustomerSubscription subscription, CancellationToken ct = default)
        {
            Subscriptions.Add(subscription);
            return Task.CompletedTask;
        }

        public Task<CustomerSubscription?> GetByIdAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct = default)
        {
            var sub = Subscriptions.FirstOrDefault(s => s.TenantId == tenantId && s.Id == subscriptionId);
            return Task.FromResult(sub);
        }

        public Task<CustomerSubscription?> GetActiveByPlateAsync(Guid tenantId, string plate, DateTimeOffset nowUtc, CancellationToken ct = default)
        {
            var normalized = SubscriptionVehiclePlate.NormalizePlate(plate);
            var sub = Subscriptions.FirstOrDefault(s =>
                s.TenantId == tenantId &&
                s.Status == SubscriptionStatusConstants.Active &&
                s.CurrentPeriodStartUtc <= nowUtc &&
                s.CurrentPeriodEndUtc >= nowUtc &&
                s.HasPlate(normalized));

            return Task.FromResult(sub);
        }

        public Task<IReadOnlyList<CustomerSubscription>> ListByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
        {
            var list = Subscriptions.Where(s => s.TenantId == tenantId && s.CustomerId == customerId).ToList();
            return Task.FromResult<IReadOnlyList<CustomerSubscription>>(list);
        }

        public Task<IReadOnlyList<CustomerSubscription>> ListAllAsync(Guid tenantId, CancellationToken ct = default)
        {
            var list = Subscriptions.Where(s => s.TenantId == tenantId).ToList();
            return Task.FromResult<IReadOnlyList<CustomerSubscription>>(list);
        }

        public Task<IReadOnlyList<SubscriptionUsage>> ListUsagesBySubscriptionIdAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct = default)
        {
            var sub = Subscriptions.FirstOrDefault(s => s.TenantId == tenantId && s.Id == subscriptionId);
            var list = sub?.Usages.ToList() ?? [];
            return Task.FromResult<IReadOnlyList<SubscriptionUsage>>(list);
        }

        public Task<int> CountActiveAsync(Guid tenantId, CancellationToken ct = default)
        {
            var count = Subscriptions.Count(s => s.TenantId == tenantId && s.Status == SubscriptionStatusConstants.Active);
            return Task.FromResult(count);
        }

        public Task<decimal> GetEstimatedMonthlyRevenueAsync(Guid tenantId, CancellationToken ct = default)
        {
            return Task.FromResult(500m);
        }

        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }

    private sealed class FakeRecurringGatewayProvider : IRecurringBillingGatewayProvider
    {
        public Task<Result<RecurringGatewaySubscriptionData>> CreateSubscriptionAsync(
            Guid tenantId,
            Guid customerId,
            string customerName,
            decimal monthlyAmount,
            string? cardNumber,
            string? cardHolderName,
            string? cardExpiration,
            string? cardCvv,
            CancellationToken ct = default)
        {
            return Task.FromResult(Result<RecurringGatewaySubscriptionData>.Success(
                new RecurringGatewaySubscriptionData("sub_fake_123", "4242", "Visa", true)));
        }

        public Task<Result> CancelSubscriptionAsync(Guid tenantId, string gatewaySubscriptionId, CancellationToken ct = default)
        {
            return Task.FromResult(Result.Success());
        }
    }

    [Fact]
    public async Task CreatePlanAsync_Should_Create_And_Persist_Plan()
    {
        var request = new CreateSubscriptionPlanRequest(
            "Plano Mensal Ducha",
            "4 duchas por mês",
            99.90m,
            CreditsPerCycle: 4,
            AllowedPlatesLimit: 1,
            BillingIntervalDays: 30);

        var result = await _service.CreatePlanAsync(_tenantId, request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Plano Mensal Ducha", result.Value.Name);
        Assert.Equal(99.90m, result.Value.MonthlyPrice);
        Assert.Equal(4, result.Value.CreditsPerCycle);
        Assert.Single(_planRepository.Plans);
    }

    [Fact]
    public async Task SubscribeCustomerAsync_Should_Invoke_Gateway_And_Save_Subscription()
    {
        var plan = SubscriptionPlan.Create(
            _tenantId,
            "Plano Ouro",
            "Desc",
            150m,
            creditsPerCycle: 4,
            allowedPlatesLimit: 2).Value!;

        await _planRepository.AddAsync(plan);

        var request = new SubscribeCustomerRequest(
            CustomerId: Guid.NewGuid(),
            CustomerName: "Maria Oliveira",
            CustomerPhone: "11977776666",
            PlanId: plan.Id,
            InitialPlates: ["ABC1D23"],
            CardNumber: "5555444433334242");

        var result = await _service.SubscribeCustomerAsync(_tenantId, request);

        Assert.True(result.IsSuccess);
        var subDto = result.Value!;
        Assert.Equal("Maria Oliveira", subDto.CustomerName);
        Assert.Equal("Plano Ouro", subDto.PlanName);
        Assert.Equal(4, subDto.AvailableCredits);
        Assert.Equal("sub_fake_123", subDto.GatewaySubscriptionId);
        Assert.Equal("4242", subDto.CardLastFourDigits);
        Assert.Single(subDto.AuthorizedPlates);
        Assert.Single(_subscriptionRepository.Subscriptions);
    }

    [Fact]
    public async Task GetActiveSubscriptionByPlateAsync_Should_Return_Summary_When_Found()
    {
        var now = DateTimeOffset.UtcNow;
        var sub = CustomerSubscription.Create(
            _tenantId,
            Guid.NewGuid(),
            "João Silva",
            "11988887777",
            Guid.NewGuid(),
            "Plano Básico",
            creditsPerCycle: 2,
            allowedPlatesLimit: 1,
            initialPlates: ["ABC1D23"],
            periodStartUtc: now.AddDays(-5),
            periodEndUtc: now.AddDays(25)).Value!;

        await _subscriptionRepository.AddAsync(sub);

        var result = await _service.GetActiveSubscriptionByPlateAsync(_tenantId, "abc-1d23");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("ABC1D23", result.Value.Plate);
        Assert.Equal(2, result.Value.AvailableCredits);
        Assert.True(result.Value.CanConsume);
    }

    [Fact]
    public async Task ConsumeCreditForWorkOrderAsync_Should_Deduct_Credit_And_Return_Receipt()
    {
        var now = DateTimeOffset.UtcNow;
        var sub = CustomerSubscription.Create(
            _tenantId,
            Guid.NewGuid(),
            "João Silva",
            "11988887777",
            Guid.NewGuid(),
            "Plano Básico",
            creditsPerCycle: 3,
            allowedPlatesLimit: 1,
            initialPlates: ["ABC1D23"],
            periodStartUtc: now.AddDays(-5),
            periodEndUtc: now.AddDays(25)).Value!;

        await _subscriptionRepository.AddAsync(sub);

        var workOrderId = Guid.NewGuid();
        var request = new ConsumeSubscriptionCreditRequest(workOrderId, "ABC1D23", "Lavagem Completa", "OS Teste");

        var result = await _service.ConsumeCreditForWorkOrderAsync(_tenantId, request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.RemainingCredits);
        Assert.Equal(workOrderId, result.Value.WorkOrderId);
    }

    [Fact]
    public async Task CancelUsageForWorkOrderAsync_Should_Rollback_Usage()
    {
        var now = DateTimeOffset.UtcNow;
        var sub = CustomerSubscription.Create(
            _tenantId,
            Guid.NewGuid(),
            "João Silva",
            "11988887777",
            Guid.NewGuid(),
            "Plano Básico",
            creditsPerCycle: 2,
            allowedPlatesLimit: 1,
            initialPlates: ["ABC1D23"],
            periodStartUtc: now.AddDays(-5),
            periodEndUtc: now.AddDays(25)).Value!;

        var workOrderId = Guid.NewGuid();
        sub.ConsumeCredit("ABC1D23", workOrderId, "Lavagem", now);
        Assert.Equal(1, sub.AvailableCredits);

        await _subscriptionRepository.AddAsync(sub);

        var result = await _service.CancelUsageForWorkOrderAsync(_tenantId, workOrderId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, sub.AvailableCredits);
    }
}
