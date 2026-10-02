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
}
