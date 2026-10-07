using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Domain;
using CarWashSaaS.WhatsApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.IntegrationTests;

[Collection(SqlServerFixture.CollectionName)]
public sealed class WhatsAppConnectionTenantIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task CreateAndRead_ShouldBeIsolated_PerTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var tenantAAccessor = new CurrentTenantAccessor();
        tenantAAccessor.SetTenant(tenantA);
        await using var tenantAContext = new WhatsAppDbContext(fixture.CreateWhatsAppOptions(), tenantAAccessor);

        var tenantAConnection = WhatsAppConnection.Create(tenantA, "session-tenant-a", "qr-tenant-a").Value!;
        tenantAContext.WhatsAppConnections.Add(tenantAConnection);
        await tenantAContext.SaveChangesAsync();

        var tenantBAccessor = new CurrentTenantAccessor();
        tenantBAccessor.SetTenant(tenantB);
        await using var tenantBContext = new WhatsAppDbContext(fixture.CreateWhatsAppOptions(), tenantBAccessor);

        var tenantBRepository = new WhatsAppConnectionRepository(tenantBContext);
        var tenantBConnection = await tenantBRepository.GetByTenantAsync(tenantA, CancellationToken.None);

        Assert.Null(tenantBConnection);

        var tenantARepository = new WhatsAppConnectionRepository(tenantAContext);
        var persisted = await tenantARepository.GetByTenantAsync(tenantA, CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.Equal(tenantA, persisted!.TenantId);
        Assert.Equal("qr-tenant-a", persisted.QrCodeValue);
    }

    [Fact]
    public async Task Incidents_ShouldBeIsolated_PerTenant_While_PlatformCanListAll()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // 1. Grava conexão e incidente no Tenant A
        var tenantAAccessor = new CurrentTenantAccessor();
        tenantAAccessor.SetTenant(tenantA);
        await using var tenantAContext = new WhatsAppDbContext(fixture.CreateWhatsAppOptions(), tenantAAccessor);

        var connA = WhatsAppConnection.Create(tenantA, "session-a", "qr-a").Value!;
        connA.MarkDisconnected("Provider closed", DateTimeOffset.UtcNow);
        tenantAContext.WhatsAppConnections.Add(connA);

        var incidentA = WhatsAppConnectionIncident.Create(
            tenantA,
            "session-a",
            WhatsAppIncidentType.Disconnected,
            "Queda de rede",
            alertDispatched: true,
            recipientEmail: "admin@lojaa.com").Value!;
        tenantAContext.WhatsAppConnectionIncidents.Add(incidentA);
        await tenantAContext.SaveChangesAsync();

        // 2. Tenant B tenta ler incidentes do Tenant A via query com TenantId
        var tenantBAccessor = new CurrentTenantAccessor();
        tenantBAccessor.SetTenant(tenantB);
        await using var tenantBContext = new WhatsAppDbContext(fixture.CreateWhatsAppOptions(), tenantBAccessor);
        var incidentRepoB = new WhatsAppConnectionIncidentRepository(tenantBContext);

        var tenantBIncidents = await incidentRepoB.ListRecentByTenantAsync(tenantA);
        Assert.Empty(tenantBIncidents);

        // 3. Plataforma (consulta global) lista incidentes de todos os tenants
        var globalIncidents = await incidentRepoB.ListRecentGlobalAsync();
        Assert.Contains(globalIncidents, i => i.TenantId == tenantA && i.ProviderSessionId == "session-a");

        // 4. Plataforma lista conexões de todos os tenants
        var connRepoB = new WhatsAppConnectionRepository(tenantBContext);
        var allConnections = await connRepoB.ListAllConnectionsAsync();
        Assert.Contains(allConnections, c => c.TenantId == tenantA && c.ProviderSessionId == "session-a");
    }
}

