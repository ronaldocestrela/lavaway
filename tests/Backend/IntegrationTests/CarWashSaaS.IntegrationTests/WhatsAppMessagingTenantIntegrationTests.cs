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

    [Fact]
    public async Task ChatbotSessions_ShouldPersistAcrossScopes_AndAdvanceFlow()
    {
        var tenantId = Guid.NewGuid();
        var customerPhone = "11988887777";
        var customerName = "Carlos Silva";

        // Scope 1: Cliente envia 1ª mensagem ("Oi"), sessão é criada e avança para Menu
        var accessorScope1 = new CurrentTenantAccessor();
        accessorScope1.SetTenant(tenantId);
        await using (var contextScope1 = new WhatsAppDbContext(fixture.CreateWhatsAppOptions(), accessorScope1))
        {
            var repoScope1 = new ChatbotSessionRepository(contextScope1);
            var activeSession = await repoScope1.GetActiveByPhoneAsync(tenantId, customerPhone);
            Assert.Null(activeSession);

            var startResult = ChatbotConversationSession.Start(tenantId, customerPhone, customerName);
            Assert.True(startResult.IsSuccess);
            var session = startResult.Value!;
            await repoScope1.AddAsync(session);

            // Simula transição para o Menu
            session.MoveToStep(ChatbotStep.Menu);
            await repoScope1.UpdateAsync(session);
        }

        // Scope 2: Cliente envia 2ª mensagem ("1"), nova requisição consulta sessão existente
        var accessorScope2 = new CurrentTenantAccessor();
        accessorScope2.SetTenant(tenantId);
        await using (var contextScope2 = new WhatsAppDbContext(fixture.CreateWhatsAppOptions(), accessorScope2))
        {
            var repoScope2 = new ChatbotSessionRepository(contextScope2);
            var retrievedSession = await repoScope2.GetActiveByPhoneAsync(tenantId, customerPhone);

            Assert.NotNull(retrievedSession);
            Assert.Equal(tenantId, retrievedSession!.TenantId);
            Assert.Equal("11988887777", retrievedSession.CustomerPhone);
            Assert.Equal("Carlos Silva", retrievedSession.CustomerName);
            Assert.Equal(ChatbotStep.Menu, retrievedSession.CurrentStep);
            Assert.True(retrievedSession.IsActive);

            // Simula avanço para seleção de serviço
            retrievedSession.MoveToStep(ChatbotStep.SelectingService);
            retrievedSession.SelectService(Guid.NewGuid(), "Lavagem Completa");
            await repoScope2.UpdateAsync(retrievedSession);
        }

        // Scope 3: Verifica se a alteração do Scope 2 foi persistida
        var accessorScope3 = new CurrentTenantAccessor();
        accessorScope3.SetTenant(tenantId);
        await using (var contextScope3 = new WhatsAppDbContext(fixture.CreateWhatsAppOptions(), accessorScope3))
        {
            var repoScope3 = new ChatbotSessionRepository(contextScope3);
            var persistedSession = await repoScope3.GetActiveByPhoneAsync(tenantId, customerPhone);

            Assert.NotNull(persistedSession);
            Assert.Equal(ChatbotStep.SelectingService, persistedSession!.CurrentStep);
            Assert.Equal("Lavagem Completa", persistedSession.SelectedServiceName);
        }
    }

    [Fact]
    public async Task ChatbotSessions_ShouldBeStrictlyIsolated_BetweenTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var customerPhone = "11977776666";

        // 1. Tenant A cria uma sessão ativa
        var tenantAAccessor = new CurrentTenantAccessor();
        tenantAAccessor.SetTenant(tenantA);
        await using (var tenantAContext = new WhatsAppDbContext(fixture.CreateWhatsAppOptions(), tenantAAccessor))
        {
            var repoA = new ChatbotSessionRepository(tenantAContext);
            var sessionA = ChatbotConversationSession.Start(tenantA, customerPhone, "Cliente A").Value!;
            sessionA.MoveToStep(ChatbotStep.Menu);
            await repoA.AddAsync(sessionA);
            await repoA.UpdateAsync(sessionA);
        }

        // 2. Tenant B tenta consultar o mesmo telefone
        var tenantBAccessor = new CurrentTenantAccessor();
        tenantBAccessor.SetTenant(tenantB);
        await using (var tenantBContext = new WhatsAppDbContext(fixture.CreateWhatsAppOptions(), tenantBAccessor))
        {
            var repoB = new ChatbotSessionRepository(tenantBContext);
            var readByTenantB = await repoB.GetActiveByPhoneAsync(tenantB, customerPhone);

            // Deve ser estritamente isolado (retornar nulo)
            Assert.Null(readByTenantB);
        }

        // 3. Tenant A consulta e obtém seus dados normalmente
        var tenantAAccessor2 = new CurrentTenantAccessor();
        tenantAAccessor2.SetTenant(tenantA);
        await using (var tenantAContext2 = new WhatsAppDbContext(fixture.CreateWhatsAppOptions(), tenantAAccessor2))
        {
            var repoA2 = new ChatbotSessionRepository(tenantAContext2);
            var readByTenantA = await repoA2.GetActiveByPhoneAsync(tenantA, customerPhone);

            Assert.NotNull(readByTenantA);
            Assert.Equal(tenantA, readByTenantA!.TenantId);
        }
    }
}

