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
        public readonly Dictionary<Guid, TenantSaasSubscription> Subscriptions = new();

        public Task<TenantSaasSubscription?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Subscriptions.GetValueOrDefault(tenantId));

        public Task<TenantSaasSubscription?> GetByGatewaySubscriptionIdAsync(string gatewaySubscriptionId, CancellationToken ct = default) =>
            Task.FromResult(Subscriptions.Values.FirstOrDefault(s => s.GatewaySubscriptionId == gatewaySubscriptionId));

        public Task<TenantSaasSubscription?> GetByGatewayCustomerIdAsync(string gatewayCustomerId, CancellationToken ct = default) =>
            Task.FromResult(Subscriptions.Values.FirstOrDefault(s => s.GatewayCustomerId == gatewayCustomerId));

        public Task<IReadOnlyList<TenantSaasSubscription>> ListAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TenantSaasSubscription>>(Subscriptions.Values.ToList());

        public Task AddAsync(TenantSaasSubscription subscription, CancellationToken ct = default)
        {
            Subscriptions[subscription.TenantId] = subscription;
            return Task.CompletedTask;
        }

        public void Update(TenantSaasSubscription subscription)
        {
            Subscriptions[subscription.TenantId] = subscription;
        }

        public void SetTenantContext(Guid tenantId) { }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeQuotaLookup(FakeSubscriptionRepository? subRepo = null, FakePlanRepository? planRepo = null) : ITenantPlanQuotaLookup
    {
        public Task<Result<TenantQuotaStatusDto>> CheckWorkOrderQuotaAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<TenantQuotaStatusDto>.Success(new(true, SaasPlanTier.Basic, TenantSubscriptionStatus.Active, 0, 100, 0, 100)));

        public Task<Result> ConsumeWorkOrderQuotaAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result.Success());

        public Task<Result<TenantQuotaStatusDto>> CheckWhatsAppQuotaAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<TenantQuotaStatusDto>.Success(new(true, SaasPlanTier.Basic, TenantSubscriptionStatus.Active, 0, 100, 0, 100)));

        public Task<Result> ConsumeWhatsAppQuotaAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result.Success());

        public Task<Result<TenantSubscriptionOverviewDto>> GetSubscriptionOverviewAsync(Guid tenantId, CancellationToken ct = default)
        {
            if (subRepo != null && subRepo.Subscriptions.TryGetValue(tenantId, out var sub))
            {
                var plan = planRepo?.Plans.FirstOrDefault(p => p.Tier == sub.PlanTier) ?? SaasPlan.GetStandardPlans().First(p => p.Tier == sub.PlanTier);
                return Task.FromResult(Result<TenantSubscriptionOverviewDto>.Success(new TenantSubscriptionOverviewDto(
                    sub.TenantId,
                    "Lava-Jato Teste",
                    sub.PlanTier,
                    plan.Name,
                    sub.MonthlyPrice,
                    sub.Status,
                    sub.CurrentPeriodStartUtc,
                    sub.CurrentPeriodEndUtc,
                    sub.GracePeriodEndsAtUtc,
                    sub.NextBillingDateUtc,
                    0,
                    plan.MaxWorkOrdersPerCycle,
                    0,
                    plan.MaxWhatsAppMessagesPerCycle,
                    sub.GatewaySubscriptionId,
                    null,
                    [],
                    plan.HasCustomerSubscriptions,
                    plan.HasLoyalty,
                    plan.HasCommissions,
                    plan.HasAiChatbot,
                    plan.MaxTeamMembers,
                    plan.MaxBoxes)));
            }

            return Task.FromResult(Result<TenantSubscriptionOverviewDto>.Failure(new("not_found", "Not found", ErrorType.NotFound)));
        }

        public Task<Result<bool>> HasFeatureAsync(Guid tenantId, SaasFeature feature, CancellationToken ct = default) =>
            Task.FromResult(Result<bool>.Success(true));

        public Task<Result> ValidateTeamMemberLimitAsync(Guid tenantId, int currentTeamMemberCount, CancellationToken ct = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> ValidateBoxesLimitAsync(Guid tenantId, int requestedBoxes, CancellationToken ct = default) =>
            Task.FromResult(Result.Success());
    }

    private sealed class FakeGlobalTenantLookup : IGlobalTenantLookup
    {
        public readonly Dictionary<Guid, (TenantStatus Status, string? Reason)> UpdatedStatuses = new();

        public Task<Result<bool>> ExistsAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<bool>.Success(true));

        public Task<Result<GlobalTenantSummaryDto>> GetSummaryAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<GlobalTenantSummaryDto>.Failure(new("not_found", "Not found", ErrorType.NotFound)));

        public Task<Result<GlobalTenantSummaryDto>> UpdateTenantStatusAsync(Guid tenantId, UpdateTenantStatusRequest request, CancellationToken ct = default)
        {
            UpdatedStatuses[tenantId] = (request.NewStatus, request.Reason);
            return Task.FromResult(Result<GlobalTenantSummaryDto>.Success(new GlobalTenantSummaryDto(
                tenantId, "Lava-Jato", request.NewStatus, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, request.TrialEndsAtUtc, request.Reason, null, null, null, null, null, null)));
        }
    }

    private sealed class FakeGatewayProvider : ISaasBillingGatewayProvider
    {
        public string? ChangedSubscriptionId { get; private set; }
        public SaasPlan? ChangedTargetPlan { get; private set; }

        public Task<Result<GatewaySubscriptionResult>> CreateSubscriptionAsync(
            Guid tenantId, string customerName, string customerEmail, SaasPlan plan, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Result<GatewayCheckoutSessionResult>> CreateSubscriptionCheckoutSessionAsync(
            Guid tenantId, string customerName, string customerEmail, SaasPlan plan, string successUrl, string cancelUrl, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Result<GatewayCheckoutSessionDetails>> GetCheckoutSessionAsync(string sessionId, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Result<GatewayPortalSessionResult>> CreateCustomerPortalSessionAsync(string gatewayCustomerId, string returnUrl, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Result<bool>> ChangeSubscriptionPlanAsync(string gatewaySubscriptionId, SaasPlan newPlan, CancellationToken ct = default)
        {
            ChangedSubscriptionId = gatewaySubscriptionId;
            ChangedTargetPlan = newPlan;
            return Task.FromResult(Result<bool>.Success(true));
        }

        public Task<Result<GatewayInvoiceResult>> GenerateInvoiceAsync(
            Guid tenantId, string gatewayCustomerId, decimal amount, DateTimeOffset dueDateUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public bool VerifyWebhookSignature(string payload, string signatureHeader, string secret) => true;
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

    [Fact]
    public async Task AdminOverridePlanAsync_WithValidTenant_ShouldChangePlanSuccessfully()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var planRepo = new FakePlanRepository();
        var subRepo = new FakeSubscriptionRepository();
        var quotaLookup = new FakeQuotaLookup(subRepo, planRepo);
        var tenantLookup = new FakeGlobalTenantLookup();
        var gatewayProvider = new FakeGatewayProvider();

        var initialPlan = planRepo.Plans.First(p => p.Tier == SaasPlanTier.Basic);
        var sub = TenantSaasSubscription.CreateActive(tenantId, initialPlan, "cust_123", "sub_123").Value!;
        await subRepo.AddAsync(sub);

        var service = new PlatformSaasBillingApplicationService(
            subRepo,
            planRepo,
            quotaLookup,
            tenantLookup,
            gatewayProvider);

        var request = new AdminOverridePlanRequest(
            TargetTier: SaasPlanTier.Enterprise,
            NewStatus: null,
            Reason: "Upgrade concedido pela diretoria");

        // Act
        var result = await service.AdminOverridePlanAsync(tenantId, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SaasPlanTier.Enterprise, result.Value!.PlanTier);
        Assert.Equal("Enterprise", result.Value.PlanName);
        Assert.Equal(599.00m, result.Value.MonthlyPrice);

        // Verifica sincronização com o gateway
        Assert.Equal("sub_123", gatewayProvider.ChangedSubscriptionId);
        Assert.NotNull(gatewayProvider.ChangedTargetPlan);
        Assert.Equal(SaasPlanTier.Enterprise, gatewayProvider.ChangedTargetPlan.Tier);

        // Verifica se persistiu no repositório
        var inRepo = await subRepo.GetByTenantIdAsync(tenantId);
        Assert.NotNull(inRepo);
        Assert.Equal(SaasPlanTier.Enterprise, inRepo.PlanTier);
        Assert.Equal(599.00m, inRepo.MonthlyPrice);
    }

    [Fact]
    public async Task AdminOverridePlanAsync_WithNewStatusActive_ShouldActivateAndNotifyTenantLookup()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var planRepo = new FakePlanRepository();
        var subRepo = new FakeSubscriptionRepository();
        var quotaLookup = new FakeQuotaLookup(subRepo, planRepo);
        var tenantLookup = new FakeGlobalTenantLookup();

        var initialPlan = planRepo.Plans.First(p => p.Tier == SaasPlanTier.Basic);
        var sub = TenantSaasSubscription.CreateActive(tenantId, initialPlan, null, null).Value!;
        sub.Suspend("Inadimplência prévia");
        await subRepo.AddAsync(sub);

        var service = new PlatformSaasBillingApplicationService(
            subRepo,
            planRepo,
            quotaLookup,
            tenantLookup);

        var request = new AdminOverridePlanRequest(
            TargetTier: SaasPlanTier.Pro,
            NewStatus: TenantSubscriptionStatus.Active,
            Reason: "Regularização manual via Pix");

        // Act
        var result = await service.AdminOverridePlanAsync(tenantId, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SaasPlanTier.Pro, result.Value!.PlanTier);
        Assert.Equal(TenantSubscriptionStatus.Active, result.Value.Status);

        // Verifica se o lookup global foi notificado da ativação
        Assert.True(tenantLookup.UpdatedStatuses.ContainsKey(tenantId));
        var (status, reason) = tenantLookup.UpdatedStatuses[tenantId];
        Assert.Equal(TenantStatus.Active, status);
        Assert.Equal("Regularização manual via Pix", reason);
    }

    [Fact]
    public async Task AdminOverridePlanAsync_WhenSubscriptionIsCanceled_ShouldReturnConflict()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var planRepo = new FakePlanRepository();
        var subRepo = new FakeSubscriptionRepository();
        var quotaLookup = new FakeQuotaLookup(subRepo, planRepo);
        var tenantLookup = new FakeGlobalTenantLookup();

        var initialPlan = planRepo.Plans.First(p => p.Tier == SaasPlanTier.Basic);
        var sub = TenantSaasSubscription.CreateActive(tenantId, initialPlan, null, null).Value!;
        sub.Cancel("Cancelamento solicitado");
        await subRepo.AddAsync(sub);

        var service = new PlatformSaasBillingApplicationService(
            subRepo,
            planRepo,
            quotaLookup,
            tenantLookup);

        var request = new AdminOverridePlanRequest(
            TargetTier: SaasPlanTier.Pro,
            NewStatus: null,
            Reason: "Tentativa em cancelado");

        // Act
        var result = await service.AdminOverridePlanAsync(tenantId, request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("saas_subscription.canceled", result.Error.Code);
    }

    [Fact]
    public async Task AdminOverridePlanAsync_WhenEmptyTenantId_ShouldReturnValidationFailure()
    {
        // Arrange
        var planRepo = new FakePlanRepository();
        var service = new PlatformSaasBillingApplicationService(
            new FakeSubscriptionRepository(),
            planRepo,
            new FakeQuotaLookup(),
            new FakeGlobalTenantLookup());

        var request = new AdminOverridePlanRequest(SaasPlanTier.Pro);

        // Act
        var result = await service.AdminOverridePlanAsync(Guid.Empty, request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("tenant.id.required", result.Error.Code);
    }

    [Fact]
    public async Task AdminOverridePlanAsync_WhenTargetPlanNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var planRepo = new FakePlanRepository();
        planRepo.Plans.Clear();
        var service = new PlatformSaasBillingApplicationService(
            new FakeSubscriptionRepository(),
            planRepo,
            new FakeQuotaLookup(),
            new FakeGlobalTenantLookup());

        var request = new AdminOverridePlanRequest(SaasPlanTier.Enterprise);

        // Act
        var result = await service.AdminOverridePlanAsync(tenantId, request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("saas_plan.not_found", result.Error.Code);
    }
}
