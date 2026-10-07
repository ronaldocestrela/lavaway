using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.UnitTests.Identity;

public sealed class AuditTrailApplicationServiceTests
{
    [Fact]
    public async Task RecordEventAsync_ShouldPersistAuditEvent_AndReturnId()
    {
        var fakeRepo = new FakeAuditEventRepository();
        var service = new AuditTrailApplicationService(fakeRepo);

        var request = new RecordAuditEventRequest(
            ActorId: Guid.NewGuid(),
            ActorEmail: "admin@lavaway.com",
            ActorRole: "SuperAdmin",
            ActorRealm: "Platform",
            Action: PlatformActionConstants.PlatformUserCreated,
            TargetType: PlatformTargetTypeConstants.PlatformUser,
            TargetId: Guid.NewGuid().ToString(),
            DetailsJson: "{\"name\":\"New Op\"}",
            Outcome: "Success");

        var result = await service.RecordEventAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
        Assert.Single(fakeRepo.Events);
        Assert.Equal("admin@lavaway.com", fakeRepo.Events[0].ActorEmail);
    }

    [Fact]
    public async Task RecordEventAsync_ShouldFail_WhenMissingRequiredFields()
    {
        var fakeRepo = new FakeAuditEventRepository();
        var service = new AuditTrailApplicationService(fakeRepo);

        var request = new RecordAuditEventRequest(
            ActorId: Guid.Empty,
            ActorEmail: "",
            ActorRole: "",
            ActorRealm: "Platform",
            Action: "",
            TargetType: "",
            TargetId: "");

        var result = await service.RecordEventAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Empty(fakeRepo.Events);
    }

    [Fact]
    public async Task SearchAuditEventsAsync_ShouldDelegateToRepository_AndMapToDto()
    {
        var fakeRepo = new FakeAuditEventRepository();
        var service = new AuditTrailApplicationService(fakeRepo);

        var actorId = Guid.NewGuid();
        var evt = AdministrativeAuditEvent.Create(
            actorId,
            "auditor@lavaway.com",
            "PlatformAuditor",
            "Platform",
            PlatformActionConstants.AuthLoginSuccess,
            PlatformTargetTypeConstants.PlatformUser,
            actorId.ToString()).Value!;

        await fakeRepo.AddAsync(evt);

        var filter = new AuditQueryFilter(ActorEmail: "auditor@lavaway.com", Page: 1, PageSize: 10);
        var result = await service.SearchAuditEventsAsync(filter);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
        Assert.Equal("auditor@lavaway.com", result.Value.Items.First().ActorEmail);
        Assert.Equal(1, result.Value.TotalCount);
    }

    [Fact]
    public async Task GetAuditEventByIdAsync_ShouldReturnNotFound_WhenEventDoesNotExist()
    {
        var fakeRepo = new FakeAuditEventRepository();
        var service = new AuditTrailApplicationService(fakeRepo);

        var result = await service.GetAuditEventByIdAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    private sealed class FakeAuditEventRepository : IAdministrativeAuditEventRepository
    {
        public List<AdministrativeAuditEvent> Events { get; } = [];

        public Task AddAsync(AdministrativeAuditEvent auditEvent, CancellationToken ct = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<AdministrativeAuditEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(Events.FirstOrDefault(e => e.Id == id));
        }

        public Task<PagedResult<AdministrativeAuditEvent>> SearchAsync(AuditQueryFilter filter, CancellationToken ct = default)
        {
            var query = Events.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(filter.ActorEmail))
            {
                query = query.Where(e => e.ActorEmail.Contains(filter.ActorEmail, StringComparison.OrdinalIgnoreCase));
            }

            var list = query.ToList();
            return Task.FromResult(new PagedResult<AdministrativeAuditEvent>(list, list.Count, filter.Page, filter.PageSize));
        }
    }
}
