using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Domain;
using Xunit;

namespace CarWashSaaS.UnitTests.WhatsApp;

public sealed class PlatformWhatsAppObservabilityServiceTests
{
    private sealed class FakeConnectionRepository : IWhatsAppConnectionRepository
    {
        public readonly List<WhatsAppConnection> Connections = [];

        public Task<WhatsAppConnection?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Connections.FirstOrDefault(c => c.TenantId == tenantId));

        public Task AddAsync(WhatsAppConnection connection, CancellationToken ct = default)
        {
            Connections.Add(connection);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(WhatsAppConnection connection, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<WhatsAppConnection>> ListAllConnectionsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WhatsAppConnection>>(Connections);

        public Task<WhatsAppConnection?> GetByProviderSessionAsync(string providerSessionId, CancellationToken ct = default) =>
            Task.FromResult(Connections.FirstOrDefault(c => c.ProviderSessionId == providerSessionId));
    }

    private sealed class FakeMessageRepository : IOutboundWhatsAppMessageRepository
    {
        public readonly List<OutboundWhatsAppMessage> Messages = [];

        public Task<OutboundWhatsAppMessage?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Messages.FirstOrDefault(m => m.Id == id));

        public Task<OutboundWhatsAppMessage?> GetByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken ct = default) =>
            Task.FromResult(Messages.FirstOrDefault(m => m.TenantId == tenantId && m.IdempotencyKey == idempotencyKey));

        public Task<OutboundWhatsAppMessage?> GetByProviderMessageIdAsync(Guid tenantId, string providerMessageId, CancellationToken ct = default) =>
            Task.FromResult(Messages.FirstOrDefault(m => m.TenantId == tenantId && m.ProviderMessageId == providerMessageId));

        public Task<IReadOnlyList<OutboundWhatsAppMessage>> GetRecentAsync(Guid tenantId, int count = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OutboundWhatsAppMessage>>(Messages.Where(m => m.TenantId == tenantId).Take(count).ToList());

        public Task<IReadOnlyList<OutboundWhatsAppMessage>> ListForPlatformMetricsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, Guid? tenantId = null, CancellationToken ct = default)
        {
            var query = Messages.Where(m => m.CreatedAt >= fromUtc && m.CreatedAt <= toUtc);
            if (tenantId.HasValue)
            {
                query = query.Where(m => m.TenantId == tenantId.Value);
            }

            return Task.FromResult<IReadOnlyList<OutboundWhatsAppMessage>>(query.ToList());
        }

        public Task AddAsync(OutboundWhatsAppMessage message, CancellationToken ct = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeGlobalTenantLookup : IGlobalTenantLookup
    {
        public Task<Result<bool>> ExistsAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<bool>.Success(true));

        public Task<Result<GlobalTenantSummaryDto>> GetSummaryAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<GlobalTenantSummaryDto>.Success(new GlobalTenantSummaryDto(
                tenantId, "Lava Rápido Express", TenantStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, "Lava Rápido Express", "Lava Rápido Express", null, null, null, null)));

        public Task<Result<GlobalTenantSummaryDto>> UpdateTenantStatusAsync(Guid tenantId, UpdateTenantStatusRequest request, CancellationToken ct = default) =>
            Task.FromResult(Result<GlobalTenantSummaryDto>.Success(new GlobalTenantSummaryDto(
                tenantId, "Lava Rápido Express", request.NewStatus, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, request.TrialEndsAtUtc, request.Reason, null, null, null, null, null, null)));
    }

    [Fact]
    public async Task Should_Calculate_WhatsApp_Summary_And_Delivery_Success_Rate()
    {
        var connRepo = new FakeConnectionRepository();
        var msgRepo = new FakeMessageRepository();
        var tenantLookup = new FakeGlobalTenantLookup();

        var tenant1 = Guid.NewGuid();
        var conn1 = WhatsAppConnection.Create(tenant1, "sess1", "qr1").Value!;
        conn1.MarkConnected();
        connRepo.Connections.Add(conn1);

        var tenant2 = Guid.NewGuid();
        var conn2 = WhatsAppConnection.Create(tenant2, "sess2", "qr2").Value!;
        connRepo.Connections.Add(conn2);

        var msgSuccess = OutboundWhatsAppMessage.Create(tenant1, "11999990001", "Mensagem 1").Value!;
        msgSuccess.MarkSent("msg1");
        msgRepo.Messages.Add(msgSuccess);

        var msgFailed = OutboundWhatsAppMessage.Create(tenant1, "11999990002", "Mensagem 2").Value!;
        msgFailed.RecordAttemptFailure("PROVIDER_ERROR", "Número inválido ou sem WhatsApp", 400, isTerminal: true);
        msgRepo.Messages.Add(msgFailed);

        var service = new PlatformWhatsAppObservabilityService(connRepo, msgRepo, tenantLookup);

        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow.AddDays(1);

        var summary = await service.GetWhatsAppMetricsSummaryAsync(from, to);

        Assert.Equal(2, summary.TotalConfiguredInstances);
        Assert.Equal(1, summary.OnlineInstancesCount);
        Assert.Equal(1, summary.OfflineInstancesCount);
        Assert.Equal(2, summary.TotalMessagesDispatchedInPeriod);
        Assert.Equal(1, summary.FailedMessagesInPeriod);
        Assert.Equal(50m, summary.DeliverySuccessRatePercentage);
    }

    [Fact]
    public async Task Should_List_Failed_Messages_With_Masked_Phone_And_Reason()
    {
        var connRepo = new FakeConnectionRepository();
        var msgRepo = new FakeMessageRepository();
        var tenantLookup = new FakeGlobalTenantLookup();

        var tenant1 = Guid.NewGuid();
        var msgFailed = OutboundWhatsAppMessage.Create(tenant1, "5511987654321", "Olá, seu carro está pronto para retirada!").Value!;
        msgFailed.RecordAttemptFailure("PROVIDER_ERROR", "Connection dropped by provider", 500, isTerminal: true);
        msgRepo.Messages.Add(msgFailed);

        var service = new PlatformWhatsAppObservabilityService(connRepo, msgRepo, tenantLookup);

        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow.AddDays(1);

        var failures = await service.ListFailedDispatchesAsync(from, to, tenant1);

        Assert.Single(failures);
        var failure = failures[0];
        Assert.Equal(tenant1, failure.TenantId);
        Assert.Contains("****", failure.RecipientPhoneMasked);
        Assert.Contains("Connection dropped by provider", failure.FailureReason);
        Assert.Equal("Lava Rápido Express", failure.TenantName);
    }
}
