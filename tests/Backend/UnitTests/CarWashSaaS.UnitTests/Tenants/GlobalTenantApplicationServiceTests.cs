using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Application;
using CarWashSaaS.Tenants.Domain;

namespace CarWashSaaS.UnitTests.Tenants;

public sealed class GlobalTenantApplicationServiceTests
{
    [Fact]
    public async Task GetTenantsAsync_ShouldReturnPagedResults()
    {
        var fakeRepo = new FakeTenantRepository();
        var tenant1 = Tenant.Create("Lavaway Centro").Value!;
        var tenant2 = Tenant.Create("Lavaway Norte").Value!;
        tenant2.Activate();

        await fakeRepo.AddAsync(tenant1);
        await fakeRepo.AddAsync(tenant2);

        var service = new GlobalTenantApplicationService(fakeRepo);
        var result = await service.GetTenantsAsync(new GetGlobalTenantsRequest(Page: 1, PageSize: 10));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalCount);
        Assert.Equal(2, result.Value.Items.Count);
    }

    [Fact]
    public async Task GetTenantsAsync_ShouldFilterByStatus()
    {
        var fakeRepo = new FakeTenantRepository();
        var tenant1 = Tenant.Create("Lavaway Centro").Value!; // Trial
        var tenant2 = Tenant.Create("Lavaway Norte").Value!;
        tenant2.Activate(); // Active

        await fakeRepo.AddAsync(tenant1);
        await fakeRepo.AddAsync(tenant2);

        var service = new GlobalTenantApplicationService(fakeRepo);
        var result = await service.GetTenantsAsync(new GetGlobalTenantsRequest(Status: TenantStatus.Active));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
        Assert.Equal("Lavaway Norte", result.Value.Items.First().Name);
        Assert.Equal(TenantStatus.Active, result.Value.Items.First().Status);
    }

    [Fact]
    public async Task GetTenantByIdAsync_ShouldReturnNotFound_WhenTenantDoesNotExist()
    {
        var fakeRepo = new FakeTenantRepository();
        var service = new GlobalTenantApplicationService(fakeRepo);

        var result = await service.GetTenantByIdAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task UpdateTenantStatusAsync_ShouldChangeStatus_AndPersist()
    {
        var fakeRepo = new FakeTenantRepository();
        var tenant = Tenant.Create("Lavaway Sul").Value!;
        await fakeRepo.AddAsync(tenant);

        var service = new GlobalTenantApplicationService(fakeRepo);
        var request = new UpdateTenantStatusRequest(
            NewStatus: TenantStatus.Delinquent,
            Reason: "Mensalidade pendente");

        var result = await service.UpdateTenantStatusAsync(tenant.Id, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantStatus.Delinquent, result.Value!.Status);
        Assert.Equal("Mensalidade pendente", result.Value.StatusReason);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenTenantExists()
    {
        var fakeRepo = new FakeTenantRepository();
        var tenant = Tenant.Create("Lavaway Oeste").Value!;
        await fakeRepo.AddAsync(tenant);

        var service = new GlobalTenantApplicationService(fakeRepo);

        var existsResult = await service.ExistsAsync(tenant.Id);

        Assert.True(existsResult.IsSuccess);
        Assert.True(existsResult.Value);
    }

    private sealed class FakeTenantRepository : ITenantRepository
    {
        public List<Tenant> Tenants { get; } = [];

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(Tenants.FirstOrDefault(t => t.Id == id));
        }

        public Task<PagedResult<GlobalTenantSummaryDto>> SearchGlobalTenantsAsync(
            GetGlobalTenantsRequest request,
            CancellationToken ct = default)
        {
            var query = Tenants.AsEnumerable();
            if (request.Status.HasValue)
            {
                query = query.Where(t => t.Status == request.Status.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                query = query.Where(t => t.Name.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase));
            }

            var list = query.Select(t => new GlobalTenantSummaryDto(
                t.Id,
                t.Name,
                t.Status,
                t.CreatedAtUtc,
                t.StatusChangedAtUtc,
                t.TrialEndsAtUtc,
                t.StatusReason,
                null, null, null, null, null, null)).ToList();

            return Task.FromResult(new PagedResult<GlobalTenantSummaryDto>(list, list.Count, request.Page, request.PageSize));
        }

        public Task<GlobalTenantSummaryDto?> GetSummaryByIdAsync(Guid id, CancellationToken ct = default)
        {
            var tenant = Tenants.FirstOrDefault(t => t.Id == id);
            if (tenant is null) return Task.FromResult<GlobalTenantSummaryDto?>(null);

            var summary = new GlobalTenantSummaryDto(
                tenant.Id,
                tenant.Name,
                tenant.Status,
                tenant.CreatedAtUtc,
                tenant.StatusChangedAtUtc,
                tenant.TrialEndsAtUtc,
                tenant.StatusReason,
                null, null, null, null, null, null);

            return Task.FromResult<GlobalTenantSummaryDto?>(summary);
        }

        public Task AddAsync(Tenant tenant, CancellationToken ct = default)
        {
            Tenants.Add(tenant);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Tenant tenant, CancellationToken ct = default)
        {
            var index = Tenants.FindIndex(t => t.Id == tenant.Id);
            if (index >= 0) Tenants[index] = tenant;
            return Task.CompletedTask;
        }
    }
}
