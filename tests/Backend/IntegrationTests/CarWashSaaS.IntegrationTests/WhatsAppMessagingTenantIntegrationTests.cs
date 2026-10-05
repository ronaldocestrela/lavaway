using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.WhatsApp.Domain;
using CarWashSaaS.WhatsApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.IntegrationTests;

[Collection(SqlServerFixture.CollectionName)]
public sealed class WhatsAppMessagingTenantIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task OutboundWhatsAppMessages_ShouldBeStrictlyIsolated_BetweenTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // 1. Tenant A cria e salva uma mensagem
        var tenantAAccessor = new CurrentTenantAccessor();
        tenantAAccessor.SetTenant(tenantA);
        await using var tenantAContext = new WhatsAppDbContext(fixture.CreateWhatsAppOptions(), tenantAAccessor);

        var tenantAMessage = OutboundWhatsAppMessage.Create(tenantA, "11988887777", "Mensagem confidencial Tenant A", "idemp-tenant-a-1").Value!;
        tenantAContext.OutboundWhatsAppMessages.Add(tenantAMessage);
        await tenantAContext.SaveChangesAsync();

        // 2. Tenant B tenta consultar a mensagem de Tenant A via repositório
        var tenantBAccessor = new CurrentTenantAccessor();
        tenantBAccessor.SetTenant(tenantB);
        await using var tenantBContext = new WhatsAppDbContext(fixture.CreateWhatsAppOptions(), tenantBAccessor);

        var tenantBRepo = new OutboundWhatsAppMessageRepository(tenantBContext);
        var readById = await tenantBRepo.GetByIdAsync(tenantAMessage.Id);
        var recent = await tenantBRepo.GetRecentAsync(tenantB);

        // 3. Validações compulsorias contra vazamento cross-tenant
        Assert.Null(readById);
        Assert.DoesNotContain(recent, m => m.Id == tenantAMessage.Id);

        // 4. Tenant A consulta e obtém seus dados normalmente
        var tenantARepo = new OutboundWhatsAppMessageRepository(tenantAContext);
        var readByTenantA = await tenantARepo.GetByIdAsync(tenantAMessage.Id);
        Assert.NotNull(readByTenantA);
        Assert.Equal(tenantA, readByTenantA!.TenantId);
        Assert.Equal("5511988887777", readByTenantA.RecipientPhone);
    }

    [Fact]
    public async Task TenantWhatsAppQuota_ShouldBeIsolated_PerTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var tenantAAccessor = new CurrentTenantAccessor();
        tenantAAccessor.SetTenant(tenantA);
        await using var tenantAContext = new WhatsAppDbContext(fixture.CreateWhatsAppOptions(), tenantAAccessor);

        var tenantARepo = new TenantWhatsAppQuotaRepository(tenantAContext);
        var quotaA = await tenantARepo.GetOrCreateAsync(tenantA);
        quotaA.RecordSend();
        await tenantARepo.SaveChangesAsync();

        var tenantBAccessor = new CurrentTenantAccessor();
        tenantBAccessor.SetTenant(tenantB);
        await using var tenantBContext = new WhatsAppDbContext(fixture.CreateWhatsAppOptions(), tenantBAccessor);

        var tenantBRepo = new TenantWhatsAppQuotaRepository(tenantBContext);
        var quotaB = await tenantBRepo.GetOrCreateAsync(tenantB);

        // Quota de B deve estar zerada e independente da de A
        Assert.Equal(0, quotaB.SentToday);
        Assert.Equal(tenantB, quotaB.TenantId);
        Assert.Equal(1, quotaA.SentToday);
        Assert.Equal(tenantA, quotaA.TenantId);
    }
}
