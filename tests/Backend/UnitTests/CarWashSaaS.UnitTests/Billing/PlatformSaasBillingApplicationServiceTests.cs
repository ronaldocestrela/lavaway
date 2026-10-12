using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class PlatformSaasBillingApplicationServiceTests
{
    private sealed class FakePlanRepository : ISaasPlanRepository
    {
        public readonly List<SaasPlan> Plans = [.. SaasPlan.GetStandardPlans()];

        public Task<IReadOnlyList<SaasPlan>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SaasPlan>>(Plans.Where(p => p.IsActive).ToList());

        public Task<IReadOnlyList<SaasPlan>> ListAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SaasPlan>>(Plans.ToList());

        public Task<SaasPlan?> GetByTierAsync(SaasPlanTier tier, CancellationToken ct = default) =>
            Task.FromResult(Plans.FirstOrDefault(p => p.Tier == tier));

        public Task AddAsync(SaasPlan plan, CancellationToken ct = default)
        {
            Plans.Add(plan);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SaasPlan plan, CancellationToken ct = default)
        {
            var idx = Plans.FindIndex(p => p.Tier == plan.Tier);
            if (idx >= 0) Plans[idx] = plan;
            return Task.CompletedTask;
        }

        public Task EnsureSeedDataAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeSubscriptionRepository : ITenantSaasSubscriptionRepository
    {
        public Task<TenantSaasSubscription?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<TenantSaasSubscription?>(null);

        public Task<TenantSaasSubscription?> GetByGatewaySubscriptionIdAsync(string gatewaySubscriptionId, CancellationToken ct = default) =>
            Task.FromResult<TenantSaasSubscription?>(null);

        public Task<TenantSaasSubscription?> GetByGatewayCustomerIdAsync(string gatewayCustomerId, CancellationToken ct = default) =>
            Task.FromResult<TenantSaasSubscription?>(null);

        public Task<IReadOnlyList<TenantSaasSubscription>> ListAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TenantSaasSubscription>>([]);

        public Task AddAsync(TenantSaasSubscription subscription, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(TenantSaasSubscription subscription) { }
        public void SetTenantContext(Guid tenantId) { }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeQuotaLookup : ITenantPlanQuotaLookup
    {
        public Task<Result<TenantQuotaStatusDto>> CheckWorkOrderQuotaAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<TenantQuotaStatusDto>.Success(new(true, SaasPlanTier.Basic, TenantSubscriptionStatus.Active, 0, 100, 0, 100)));

        public Task<Result> ConsumeWorkOrderQuotaAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result.Success());

        public Task<Result<TenantQuotaStatusDto>> CheckWhatsAppQuotaAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<TenantQuotaStatusDto>.Success(new(true, SaasPlanTier.Basic, TenantSubscriptionStatus.Active, 0, 100, 0, 100)));

        public Task<Result> ConsumeWhatsAppQuotaAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result.Success());

        public Task<Result<TenantSubscriptionOverviewDto>> GetSubscriptionOverviewAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<TenantSubscriptionOverviewDto>.Failure(new("not_found", "Not found", ErrorType.NotFound)));
    }

    private sealed class FakeGlobalTenantLookup : IGlobalTenantLookup
    {
        public Task<Result<bool>> ExistsAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<bool>.Success(true));

        public Task<Result<GlobalTenantSummaryDto>> GetSummaryAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<GlobalTenantSummaryDto>.Failure(new("not_found", "Not found", ErrorType.NotFound)));

        public Task<Result<GlobalTenantSummaryDto>> UpdateTenantStatusAsync(Guid tenantId, UpdateTenantStatusRequest request, CancellationToken ct = default) =>
            Task.FromResult(Result<GlobalTenantSummaryDto>.Failure(new("not_found", "Not found", ErrorType.NotFound)));
    }

    [Fact]
    public async Task ListAllPlansAsync_ShouldReturnAllPlansIncludingBoxesAndModules()
    {
        // Arrange
        var planRepo = new FakePlanRepository();
        var service = new PlatformSaasBillingApplicationService(
            new FakeSubscriptionRepository(),
            planRepo,
            new FakeQuotaLookup(),
            new FakeGlobalTenantLookup());

        // Act
        var result = await service.ListAllPlansAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Count);

        var basic = result.Value.First(p => p.Tier == SaasPlanTier.Basic);
        Assert.Equal("Básico", basic.Name);
        Assert.Equal(2, basic.MaxBoxes);
        Assert.Equal(3, basic.MaxTeamMembers);
        Assert.False(basic.HasCustomerSubscriptions);
    }

    [Fact]
    public async Task UpdatePlanAsync_WithValidData_ShouldUpdatePlanSuccessfully()
    {
        // Arrange
        var planRepo = new FakePlanRepository();
        var service = new PlatformSaasBillingApplicationService(
            new FakeSubscriptionRepository(),
            planRepo,
            new FakeQuotaLookup(),
            new FakeGlobalTenantLookup());

        var updateRequest = new UpdateSaasPlanRequest(
            Name: "Básico Plus",
            Description: "Descrição Customizada",
            MonthlyPrice: 189.90m,
            MaxWorkOrdersPerCycle: 200,
            MaxWhatsAppMessagesPerCycle: 400,
            MaxTeamMembers: 4,
            MaxBoxes: 3,
            HasCustomerSubscriptions: true,
            HasLoyalty: false,
            HasCommissions: true,
            HasAiChatbot: false,
            IsActive: true);

        // Act
        var result = await service.UpdatePlanAsync(SaasPlanTier.Basic, updateRequest);

        // Assert
        Assert.True(result.IsSuccess);
        var updated = result.Value!;
        Assert.Equal("Básico Plus", updated.Name);
        Assert.Equal(189.90m, updated.MonthlyPrice);
        Assert.Equal(4, updated.MaxTeamMembers);
        Assert.Equal(3, updated.MaxBoxes);
        Assert.True(updated.HasCustomerSubscriptions);
        Assert.True(updated.HasCommissions);

        // Verifica se persistiu no repositório
        var inRepo = await planRepo.GetByTierAsync(SaasPlanTier.Basic);
        Assert.NotNull(inRepo);
        Assert.Equal("Básico Plus", inRepo.Name);
        Assert.Equal(3, inRepo.MaxBoxes);
    }

    [Fact]
    public async Task UpdatePlanAsync_WithNonExistentTier_ShouldReturnNotFound()
    {
        // Arrange
        var planRepo = new FakePlanRepository();
        planRepo.Plans.Clear(); // remove todos
        var service = new PlatformSaasBillingApplicationService(
            new FakeSubscriptionRepository(),
            planRepo,
            new FakeQuotaLookup(),
            new FakeGlobalTenantLookup());

        var updateRequest = new UpdateSaasPlanRequest("X", "Y", 100m, 10, 10, 2, 1, false, false, false, false, true);

        // Act
        var result = await service.UpdatePlanAsync(SaasPlanTier.Basic, updateRequest);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task UpdatePlanAsync_WithInvalidData_ShouldReturnValidationError()
    {
        // Arrange
        var planRepo = new FakePlanRepository();
        var service = new PlatformSaasBillingApplicationService(
            new FakeSubscriptionRepository(),
            planRepo,
            new FakeQuotaLookup(),
            new FakeGlobalTenantLookup());

        var invalidRequest = new UpdateSaasPlanRequest(
            Name: "",
            Description: "Sem nome",
            MonthlyPrice: -50m,
            MaxWorkOrdersPerCycle: 10,
            MaxWhatsAppMessagesPerCycle: 10,
            MaxTeamMembers: 2,
            MaxBoxes: -1,
            HasCustomerSubscriptions: false,
            HasLoyalty: false,
            HasCommissions: false,
            HasAiChatbot: false,
            IsActive: true);

        // Act
        var result = await service.UpdatePlanAsync(SaasPlanTier.Basic, invalidRequest);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }
}
