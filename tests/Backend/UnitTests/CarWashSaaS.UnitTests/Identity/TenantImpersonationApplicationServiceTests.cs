using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.UnitTests.Identity;

public sealed class TenantImpersonationApplicationServiceTests
{
    [Fact]
    public async Task StartImpersonationAsync_ShouldSucceed_AndRecordAuditEvent_ForSupportOperator()
    {
        var tenantId = Guid.NewGuid();
        var fakeLookup = new FakeGlobalTenantLookup();
        fakeLookup.AddTenant(tenantId, "Lavaway Matriz");

        var fakeAuditRepo = new FakeAuditEventRepository();
        var auditService = new AuditTrailApplicationService(fakeAuditRepo);
        var impersonationService = new TenantImpersonationApplicationService(fakeLookup, auditService);

        var operatorId = Guid.NewGuid();
        var request = new StartImpersonationRequest(
            Reason: "Diagnóstico de erro 500 no Kanban",
            TicketReference: "CHAMADO-1029");

        var result = await impersonationService.StartImpersonationAsync(
            tenantId,
            request,
            operatorId,
            "suporte@lavaway.com",
            PlatformRole.PlatformSupport.ToString(),
            "Platform",
            "127.0.0.1",
            "Chrome/130.0");

        Assert.True(result.IsSuccess);
        Assert.Equal(tenantId, result.Value!.TenantId);
        Assert.Equal("Lavaway Matriz", result.Value.TenantName);
        Assert.Equal("suporte@lavaway.com", result.Value.OperatorEmail);
        Assert.Equal("CHAMADO-1029", result.Value.TicketReference);

        // Verifica gravação imutável de auditoria
        Assert.Single(fakeAuditRepo.Events);
        var auditEvent = fakeAuditRepo.Events[0];
        Assert.Equal(PlatformActionConstants.TenantImpersonated, auditEvent.Action);
        Assert.Equal(operatorId, auditEvent.ActorId);
        Assert.Equal(tenantId.ToString(), auditEvent.TargetId);
        Assert.Equal("Success", auditEvent.Outcome);
        Assert.Contains("CHAMADO-1029", auditEvent.DetailsJson);
    }

    [Fact]
    public async Task StartImpersonationAsync_ShouldFail_WhenReasonIsMissing()
    {
        var tenantId = Guid.NewGuid();
        var fakeLookup = new FakeGlobalTenantLookup();
        fakeLookup.AddTenant(tenantId, "Lavaway Matriz");

        var fakeAuditRepo = new FakeAuditEventRepository();
        var auditService = new AuditTrailApplicationService(fakeAuditRepo);
        var impersonationService = new TenantImpersonationApplicationService(fakeLookup, auditService);

        var request = new StartImpersonationRequest(Reason: "");

        var result = await impersonationService.StartImpersonationAsync(
            tenantId,
            request,
            Guid.NewGuid(),
            "suporte@lavaway.com",
            PlatformRole.PlatformSupport.ToString(),
            "Platform");

        Assert.False(result.IsSuccess);
        Assert.Equal("impersonation.reason.required", result.Error!.Code);
        Assert.Empty(fakeAuditRepo.Events);
    }

    [Fact]
    public async Task StartImpersonationAsync_ShouldFail_WhenOperatorLacksPermission()
    {
        var tenantId = Guid.NewGuid();
        var fakeLookup = new FakeGlobalTenantLookup();
        fakeLookup.AddTenant(tenantId, "Lavaway Matriz");

        var fakeAuditRepo = new FakeAuditEventRepository();
        var auditService = new AuditTrailApplicationService(fakeAuditRepo);
        var impersonationService = new TenantImpersonationApplicationService(fakeLookup, auditService);

        var operatorId = Guid.NewGuid();
        var request = new StartImpersonationRequest(Reason: "Tentativa não autorizada");

        var result = await impersonationService.StartImpersonationAsync(
            tenantId,
            request,
            operatorId,
            "auditor@lavaway.com",
            PlatformRole.PlatformAuditor.ToString(), // Auditor NÃO tem permissão de ImpersonateTenant
            "Platform");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);

        // Deve gravar a tentativa negada na trilha de auditoria
        Assert.Single(fakeAuditRepo.Events);
        Assert.Equal("Failure", fakeAuditRepo.Events[0].Outcome);
    }

    [Fact]
    public async Task StartImpersonationAsync_ShouldFail_WhenTenantNotFound()
    {
        var nonExistentId = Guid.NewGuid();
        var fakeLookup = new FakeGlobalTenantLookup();
        var fakeAuditRepo = new FakeAuditEventRepository();
        var auditService = new AuditTrailApplicationService(fakeAuditRepo);
        var impersonationService = new TenantImpersonationApplicationService(fakeLookup, auditService);

        var request = new StartImpersonationRequest(Reason: "Teste de erro");

        var result = await impersonationService.StartImpersonationAsync(
            nonExistentId,
            request,
            Guid.NewGuid(),
            "suporte@lavaway.com",
            PlatformRole.SuperAdmin.ToString(),
            "Platform");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task EndImpersonationAsync_ShouldRecordImpersonationEndedAuditEvent()
    {
        var tenantId = Guid.NewGuid();
        var fakeLookup = new FakeGlobalTenantLookup();
        var fakeAuditRepo = new FakeAuditEventRepository();
        var auditService = new AuditTrailApplicationService(fakeAuditRepo);
        var impersonationService = new TenantImpersonationApplicationService(fakeLookup, auditService);

        var operatorId = Guid.NewGuid();
        var request = new EndImpersonationRequest(Notes: "Diagnóstico finalizado com êxito.");

        var result = await impersonationService.EndImpersonationAsync(
            tenantId,
            request,
            operatorId,
            "suporte@lavaway.com",
            PlatformRole.PlatformSupport.ToString(),
            "Platform");

        Assert.True(result.IsSuccess);
        Assert.Single(fakeAuditRepo.Events);
        var auditEvent = fakeAuditRepo.Events[0];
        Assert.Equal(PlatformActionConstants.TenantImpersonationEnded, auditEvent.Action);
        Assert.Equal(operatorId, auditEvent.ActorId);
        Assert.Equal(tenantId.ToString(), auditEvent.TargetId);
        Assert.Contains("Diagnóstico finalizado com êxito.", auditEvent.DetailsJson);
    }

    private sealed class FakeGlobalTenantLookup : IGlobalTenantLookup
    {
        private readonly Dictionary<Guid, GlobalTenantSummaryDto> _tenants = [];

        public void AddTenant(Guid id, string name)
        {
            _tenants[id] = new GlobalTenantSummaryDto(
                id,
                name,
                TenantStatus.Active,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                null,
                null,
                null, null, null, null, null, null);
        }

        public Task<Result<bool>> ExistsAsync(Guid tenantId, CancellationToken ct = default)
        {
            return Task.FromResult(Result<bool>.Success(_tenants.ContainsKey(tenantId)));
        }

        public Task<Result<GlobalTenantSummaryDto>> GetSummaryAsync(Guid tenantId, CancellationToken ct = default)
        {
            if (_tenants.TryGetValue(tenantId, out var tenant))
            {
                return Task.FromResult(Result<GlobalTenantSummaryDto>.Success(tenant));
            }

            return Task.FromResult(Result<GlobalTenantSummaryDto>.Failure(new Error("tenant.not_found", "Not found", ErrorType.NotFound)));
        }

        public Task<Result<GlobalTenantSummaryDto>> UpdateTenantStatusAsync(
            Guid tenantId,
            UpdateTenantStatusRequest request,
            CancellationToken ct = default)
        {
            if (_tenants.TryGetValue(tenantId, out var tenant))
            {
                var updated = tenant with { Status = request.NewStatus, StatusReason = request.Reason };
                _tenants[tenantId] = updated;
                return Task.FromResult(Result<GlobalTenantSummaryDto>.Success(updated));
            }

            return Task.FromResult(Result<GlobalTenantSummaryDto>.Failure(new Error("tenant.not_found", "Not found", ErrorType.NotFound)));
        }
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
            return Task.FromResult(new PagedResult<AdministrativeAuditEvent>(Events, Events.Count, filter.Page, filter.PageSize));
        }
    }
}
