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

    private sealed class InMemoryWhatsAppConnectionRepository : IWhatsAppConnectionRepository
    {
        private WhatsAppConnection? _connection;

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
            return Task.CompletedTask;
        }
    }

    private sealed class FakePairingProvider(string providerSessionId, string qrCodeValue) : IWhatsAppPairingProvider
    {
        public Task<(string ProviderSessionId, string QrCodeValue)> GeneratePairingAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult((providerSessionId, qrCodeValue));
    }
}
