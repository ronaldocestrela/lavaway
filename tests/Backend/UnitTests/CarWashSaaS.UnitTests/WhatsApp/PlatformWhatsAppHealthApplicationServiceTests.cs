using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.UnitTests.WhatsApp;

public sealed class PlatformWhatsAppHealthApplicationServiceTests
{
    [Fact]
    public async Task GetOverviewAsync_Should_Calculate_KPIs_And_Filter_By_Status()
    {
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();
        var tenant3 = Guid.NewGuid();

        var conn1 = WhatsAppConnection.Create(tenant1, "session-1", "qr-1").Value!;
        conn1.MarkConnected();

        var conn2 = WhatsAppConnection.Create(tenant2, "session-2", "qr-2").Value!;
        conn2.MarkDisconnected("Signal lost");

        var conn3 = WhatsAppConnection.Create(tenant3, "session-3", "qr-3").Value!;
        // conn3 está Connecting

        var repo = new FakePlatformConnectionRepository(new[] { conn1, conn2, conn3 });
        var connService = new WhatsAppConnectionApplicationService(repo);
        var platformService = new PlatformWhatsAppHealthApplicationService(repo, connService);

        // Busca todas
        var allResult = await platformService.GetOverviewAsync(new GetPlatformWhatsAppInstancesRequest());
        Assert.True(allResult.IsSuccess);
        Assert.Equal(3, allResult.Value!.Summary.TotalInstances);
        Assert.Equal(1, allResult.Value.Summary.ConnectedCount);
        Assert.Equal(1, allResult.Value.Summary.DisconnectedCount);
        Assert.Equal(1, allResult.Value.Summary.ConnectingCount);
        Assert.Equal(1, allResult.Value.Summary.AlertsActiveCount);
        Assert.Equal(3, allResult.Value.Instances.TotalCount);

        // Filtro por "disconnected"
        var disconnectedResult = await platformService.GetOverviewAsync(new GetPlatformWhatsAppInstancesRequest(Status: "disconnected"));
        Assert.True(disconnectedResult.IsSuccess);
        Assert.Single(disconnectedResult.Value!.Instances.Items);
        Assert.Equal(tenant2, disconnectedResult.Value.Instances.Items.First().TenantId);
        Assert.Equal("disconnected", disconnectedResult.Value.Instances.Items.First().Status);
    }

    [Fact]
    public async Task RunProbeAsync_Should_Return_Probe_Result()
    {
        var tenantId = Guid.NewGuid();
        var conn = WhatsAppConnection.Create(tenantId, "session-probe", "qr-probe").Value!;
        conn.MarkConnected();

        var repo = new FakePlatformConnectionRepository(new[] { conn });
        var healthProvider = new FakeHealthCheckProvider(new WhatsAppProviderHealthState(true, "open"));
        var connService = new WhatsAppConnectionApplicationService(repo, healthCheckProvider: healthProvider);
        var platformService = new PlatformWhatsAppHealthApplicationService(repo, connService);

        var result = await platformService.RunProbeAsync(tenantId);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsReachable);
        Assert.Equal("open", result.Value.ProviderState);
    }

    [Fact]
    public async Task TriggerManualAlertAsync_Should_Dispatch_Alert()
    {
        var tenantId = Guid.NewGuid();
        var conn = WhatsAppConnection.Create(tenantId, "session-alert", "qr-alert").Value!;
        conn.MarkDisconnected("Dead session");

        var repo = new FakePlatformConnectionRepository(new[] { conn });
        var alertSender = new FakeAlertSender();
        var contactLookup = new FakeContactLookup(new TenantNotificationContactDto(tenantId, "Loja Sul", "lojasul@test.com", null));
        var connService = new WhatsAppConnectionApplicationService(
            repo,
            alertSender: alertSender,
            contactLookup: contactLookup);
        var platformService = new PlatformWhatsAppHealthApplicationService(repo, connService);

        var result = await platformService.TriggerManualAlertAsync(tenantId, new TriggerWhatsAppAlertRequest("Reconexão urgente necessária"));

        Assert.True(result.IsSuccess);
        Assert.Single(alertSender.SentAlerts);
        Assert.Equal("lojasul@test.com", alertSender.SentAlerts[0].RecipientEmail);
    }

    private sealed class FakePlatformConnectionRepository(IEnumerable<WhatsAppConnection> connections) : IWhatsAppConnectionRepository
    {
        private readonly List<WhatsAppConnection> _list = connections.ToList();

        public Task<WhatsAppConnection?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(_list.FirstOrDefault(c => c.TenantId == tenantId));

        public Task AddAsync(WhatsAppConnection connection, CancellationToken ct = default)
        {
            _list.Add(connection);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(WhatsAppConnection connection, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<WhatsAppConnection>> ListAllConnectionsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WhatsAppConnection>>(_list);

        public Task<WhatsAppConnection?> GetByProviderSessionAsync(string providerSessionId, CancellationToken ct = default)
            => Task.FromResult(_list.FirstOrDefault(c => c.ProviderSessionId == providerSessionId));
    }

    private sealed class FakeHealthCheckProvider(WhatsAppProviderHealthState state) : IWhatsAppHealthCheckProvider
    {
        public Task<Result<WhatsAppProviderHealthState>> CheckHealthAsync(string providerSessionId, CancellationToken ct = default)
            => Task.FromResult(Result<WhatsAppProviderHealthState>.Success(state));
    }

    private sealed class FakeAlertSender : IWhatsAppHealthAlertSender
    {
        public List<(string RecipientEmail, string TenantName, string ReconnectInstructionsUrl, string? Reason)> SentAlerts { get; } = new();

        public Task<Result> SendDisconnectionAlertAsync(
            string recipientEmail,
            string tenantName,
            string reconnectInstructionsUrl,
            string? reason = null,
            CancellationToken ct = default)
        {
            SentAlerts.Add((recipientEmail, tenantName, reconnectInstructionsUrl, reason));
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FakeContactLookup(TenantNotificationContactDto? contact = null) : ITenantNotificationContactLookup
    {
        public Task<Result<TenantNotificationContactDto>> GetContactAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(contact is not null
                ? Result<TenantNotificationContactDto>.Success(contact)
                : Result<TenantNotificationContactDto>.Failure(new Error("contact.not_found", "Not found", ErrorType.NotFound)));
    }
}
