using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.UnitTests.WhatsApp;

public sealed class WhatsAppConnectionApplicationServiceTests
{
    [Fact]
    public async Task StartPairingAsync_Should_Use_Provider_Values_When_Creating_Connection()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryWhatsAppConnectionRepository();
        var provider = new FakePairingProvider("provider-session-123", "provider-qr-123");
        var service = new WhatsAppConnectionApplicationService(repository, provider);

        var result = await service.StartPairingAsync(tenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal("provider-session-123", result.Value!.ProviderSessionId);
        Assert.Equal("provider-qr-123", result.Value.QrCodeValue);
        Assert.Equal(WhatsAppConnectionStatus.Connecting, result.Value.Status);
    }

    [Fact]
    public async Task StartPairingAsync_Should_Propagate_Provider_Failure_Without_Creating_Connection()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryWhatsAppConnectionRepository();
        var provider = new FakePairingProvider(fail: true);
        var service = new WhatsAppConnectionApplicationService(repository, provider);

        var result = await service.StartPairingAsync(tenantId);

        Assert.False(result.IsSuccess);
        Assert.Equal("whatsapp.provider.network_error", result.Error!.Code);
        Assert.Null(await repository.GetByTenantAsync(tenantId));
    }

    [Fact]
    public async Task ApplyProviderStatusAsync_Should_Mark_Connection_Connected_When_Provider_Reports_Open()
    {
        var tenantId = Guid.NewGuid();
        var connection = WhatsAppConnection.Create(tenantId, "session-123", "qr-123").Value!;
        var repository = new InMemoryWhatsAppConnectionRepository(connection);
        var service = new WhatsAppConnectionApplicationService(repository);

        var result = await service.ApplyProviderStatusAsync(tenantId, "session-123", "open");

        Assert.True(result.IsSuccess);
        Assert.Equal(WhatsAppConnectionStatus.Connected, result.Value!.Status);
    }

    [Fact]
    public async Task ApplyProviderStatusAsync_Should_Reject_Event_For_Different_Provider_Session()
    {
        var tenantId = Guid.NewGuid();
        var connection = WhatsAppConnection.Create(tenantId, "current-session", "qr-123").Value!;
        var repository = new InMemoryWhatsAppConnectionRepository(connection);
        var service = new WhatsAppConnectionApplicationService(repository);

        var result = await service.ApplyProviderStatusAsync(tenantId, "old-session", "open");

        Assert.False(result.IsSuccess);
        Assert.Equal("whatsapp.provider_session.mismatch", result.Error!.Code);
        Assert.Equal(WhatsAppConnectionStatus.Connecting, connection.Status);
    }

    [Fact]
    public async Task DisconnectAsync_Should_Mark_Connection_Disconnected_When_Active()
    {
        var tenantId = Guid.NewGuid();
        var connection = WhatsAppConnection.Create(tenantId, "session-123", "qr-123").Value!;
        connection.MarkConnected();
        var repository = new InMemoryWhatsAppConnectionRepository(connection);
        var provider = new FakePairingProvider();
        var service = new WhatsAppConnectionApplicationService(repository, provider);

        var result = await service.DisconnectAsync(tenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal("session-123", provider.DisconnectedSessionId);
        Assert.Equal(WhatsAppConnectionStatus.Disconnected, result.Value!.Status);
        Assert.Equal(WhatsAppConnectionStatus.Disconnected, connection.Status);
    }

    [Fact]
    public async Task DisconnectAsync_Should_Keep_Connection_Active_When_Provider_Disconnect_Fails()
    {
        var tenantId = Guid.NewGuid();
        var connection = WhatsAppConnection.Create(tenantId, "session-123", "qr-123").Value!;
        connection.MarkConnected();
        var repository = new InMemoryWhatsAppConnectionRepository(connection);
        var provider = new FakePairingProvider(disconnectFail: true);
        var service = new WhatsAppConnectionApplicationService(repository, provider);

        var result = await service.DisconnectAsync(tenantId);

        Assert.False(result.IsSuccess);
        Assert.Equal("whatsapp.provider.network_error", result.Error!.Code);
        Assert.Equal(WhatsAppConnectionStatus.Connected, connection.Status);
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public async Task DisconnectAsync_Should_Return_NotFound_When_Connection_Does_Not_Exist()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryWhatsAppConnectionRepository();
        var service = new WhatsAppConnectionApplicationService(repository);

        var result = await service.DisconnectAsync(tenantId);

        Assert.False(result.IsSuccess);
        Assert.Equal("whatsapp.not_found", result.Error!.Code);
    }

    [Fact]
    public async Task DisconnectAsync_Should_Reject_Empty_Tenant_Id()
    {
        var repository = new InMemoryWhatsAppConnectionRepository();
        var service = new WhatsAppConnectionApplicationService(repository);

        var result = await service.DisconnectAsync(Guid.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal("whatsapp.tenant.required", result.Error!.Code);
    }

    [Fact]
    public async Task ApplyProviderStatusAsync_Should_Trigger_Alert_And_Record_Incident_When_Connected_Becomes_Disconnected()
    {
        var tenantId = Guid.NewGuid();
        var connection = WhatsAppConnection.Create(tenantId, "session-123", "qr-123").Value!;
        connection.MarkConnected();

        var repository = new InMemoryWhatsAppConnectionRepository(connection);
        var alertSender = new FakeAlertSender();
        var incidentRepository = new FakeIncidentRepository();
        var contactLookup = new FakeContactLookup(new TenantNotificationContactDto(tenantId, "Lava Rápido Central", "admin@central.com", "1199999999"));

        var service = new WhatsAppConnectionApplicationService(
            repository,
            alertSender: alertSender,
            incidentRepository: incidentRepository,
            contactLookup: contactLookup);

        var result = await service.ApplyProviderStatusAsync(tenantId, "session-123", "close");

        Assert.True(result.IsSuccess);
        Assert.Equal(WhatsAppConnectionStatus.Disconnected, result.Value!.Status);
        Assert.True(result.Value.HasActiveAlert);
        Assert.Equal(1, result.Value.AlertCount);
        Assert.NotNull(result.Value.LastAlertSentAtUtc);

        // Verifica que o alerta foi disparado com os dados corretos
        Assert.Single(alertSender.SentAlerts);
        Assert.Equal("admin@central.com", alertSender.SentAlerts[0].RecipientEmail);
        Assert.Equal("Lava Rápido Central", alertSender.SentAlerts[0].TenantName);

        // Verifica que o incidente foi registrado
        Assert.Single(incidentRepository.Incidents);
        Assert.Equal(WhatsAppIncidentType.Disconnected, incidentRepository.Incidents[0].Type);
        Assert.True(incidentRepository.Incidents[0].AlertDispatched);
    }

    [Fact]
    public async Task ApplyProviderStatusAsync_Should_Respect_Cooldown_And_Not_Duplicate_Alert()
    {
        var tenantId = Guid.NewGuid();
        var connection = WhatsAppConnection.Create(tenantId, "session-123", "qr-123").Value!;
        connection.MarkConnected();

        var repository = new InMemoryWhatsAppConnectionRepository(connection);
        var alertSender = new FakeAlertSender();
        var incidentRepository = new FakeIncidentRepository();

        var service = new WhatsAppConnectionApplicationService(
            repository,
            alertSender: alertSender,
            incidentRepository: incidentRepository);

        // Primeira queda
        await service.ApplyProviderStatusAsync(tenantId, "session-123", "close");
        Assert.Single(alertSender.SentAlerts);

        // Segunda notificação de queda 5 minutos depois (sem reconexão prévia)
        await service.ApplyProviderStatusAsync(tenantId, "session-123", "close");

        // Não deve disparar segundo alerta por causa do cooldown
        Assert.Single(alertSender.SentAlerts);
    }

    [Fact]
    public async Task GetHealthDetailsAsync_Should_Return_Detailed_State()
    {
        var tenantId = Guid.NewGuid();
        var connection = WhatsAppConnection.Create(tenantId, "session-123", "qr-123").Value!;
        connection.MarkConnected();
        var repository = new InMemoryWhatsAppConnectionRepository(connection);

        var service = new WhatsAppConnectionApplicationService(repository);
        var result = await service.GetHealthDetailsAsync(tenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal("connected", result.Value!.Status);
        Assert.True(result.Value.IsConnected);
        Assert.False(result.Value.HasActiveAlert);
    }

    [Fact]
    public async Task CheckTenantHealthAsync_Should_Probe_Provider_And_Sync_Status()
    {
        var tenantId = Guid.NewGuid();
        var connection = WhatsAppConnection.Create(tenantId, "session-123", "qr-123").Value!;
        connection.MarkConnected();

        var repository = new InMemoryWhatsAppConnectionRepository(connection);
        var healthProvider = new FakeHealthCheckProvider(new WhatsAppProviderHealthState(true, "open"));

        var service = new WhatsAppConnectionApplicationService(
            repository,
            healthCheckProvider: healthProvider);

        var result = await service.CheckTenantHealthAsync(tenantId);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsReachable);
        Assert.Equal("open", result.Value.ProviderState);
    }

    [Fact]
    public async Task SendManualReconnectAlertAsync_Should_Force_Dispatch_Alert()
    {
        var tenantId = Guid.NewGuid();
        var connection = WhatsAppConnection.Create(tenantId, "session-123", "qr-123").Value!;
        connection.MarkDisconnected("Offline");

        var repository = new InMemoryWhatsAppConnectionRepository(connection);
        var alertSender = new FakeAlertSender();
        var incidentRepository = new FakeIncidentRepository();
        var contactLookup = new FakeContactLookup(new TenantNotificationContactDto(tenantId, "Auto Brilho", "contato@autobrilho.com", null));

        var service = new WhatsAppConnectionApplicationService(
            repository,
            alertSender: alertSender,
            incidentRepository: incidentRepository,
            contactLookup: contactLookup);

        var result = await service.SendManualReconnectAlertAsync(tenantId, "Por favor reconecte seu aparelho");

        Assert.True(result.IsSuccess);
        Assert.Single(alertSender.SentAlerts);
        Assert.Equal("contato@autobrilho.com", alertSender.SentAlerts[0].RecipientEmail);
        Assert.Contains("Por favor reconecte", alertSender.SentAlerts[0].Reason);
    }

    private sealed class InMemoryWhatsAppConnectionRepository : IWhatsAppConnectionRepository
    {
        private WhatsAppConnection? _connection;

        public int UpdateCount { get; private set; }

        public InMemoryWhatsAppConnectionRepository(WhatsAppConnection? connection = null)
        {
            _connection = connection;
        }

        public Task<WhatsAppConnection?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(_connection);

        public Task AddAsync(WhatsAppConnection connection, CancellationToken ct = default)
        {
            _connection = connection;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(WhatsAppConnection connection, CancellationToken ct = default)
        {
            _connection = connection;
            UpdateCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WhatsAppConnection>> ListAllConnectionsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WhatsAppConnection>>(_connection is not null ? new[] { _connection } : Array.Empty<WhatsAppConnection>());

        public Task<WhatsAppConnection?> GetByProviderSessionAsync(string providerSessionId, CancellationToken ct = default)
            => Task.FromResult(_connection?.ProviderSessionId == providerSessionId ? _connection : null);
    }

    private sealed class FakePairingProvider(
        string providerSessionId = "provider-session-123",
        string qrCodeValue = "provider-qr-123",
        bool fail = false,
        bool disconnectFail = false) : IWhatsAppPairingProvider
    {
        public string? DisconnectedSessionId { get; private set; }

        public Task<Result<(string ProviderSessionId, string QrCodeValue)>> GeneratePairingAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(fail
                ? Result<(string ProviderSessionId, string QrCodeValue)>.Failure(
                    new Error("whatsapp.provider.network_error", "Provider is unreachable.", ErrorType.Unavailable))
                : Result<(string ProviderSessionId, string QrCodeValue)>.Success((providerSessionId, qrCodeValue)));

        public Task<Result> DisconnectAsync(string providerSessionId, CancellationToken ct = default)
        {
            DisconnectedSessionId = providerSessionId;
            return Task.FromResult(disconnectFail
                ? Result.Failure(new Error("whatsapp.provider.network_error", "Provider is unreachable.", ErrorType.Unavailable))
                : Result.Success());
        }
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

    private sealed class FakeIncidentRepository : IWhatsAppConnectionIncidentRepository
    {
        public List<WhatsAppConnectionIncident> Incidents { get; } = new();

        public Task AddAsync(WhatsAppConnectionIncident incident, CancellationToken ct = default)
        {
            Incidents.Add(incident);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WhatsAppConnectionIncident>> ListRecentByTenantAsync(Guid tenantId, int count = 20, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WhatsAppConnectionIncident>>(Incidents.Where(i => i.TenantId == tenantId).ToList());

        public Task<IReadOnlyList<WhatsAppConnectionIncident>> ListRecentGlobalAsync(int count = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WhatsAppConnectionIncident>>(Incidents.ToList());
    }

    private sealed class FakeContactLookup(TenantNotificationContactDto? contact = null) : ITenantNotificationContactLookup
    {
        public Task<Result<TenantNotificationContactDto>> GetContactAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(contact is not null
                ? Result<TenantNotificationContactDto>.Success(contact)
                : Result<TenantNotificationContactDto>.Failure(new Error("contact.not_found", "Not found", ErrorType.NotFound)));
    }
}
