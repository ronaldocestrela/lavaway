using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Billing.Infrastructure;
using CarWashSaaS.Billing.Infrastructure.Repositories;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Domain;
using CarWashSaaS.WhatsApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CarWashSaaS.IntegrationTests;

[Collection(SqlServerFixture.CollectionName)]
public sealed class WebhookPayloadPurgeIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task BillingWebhookPurge_Should_Discard_Payload_Older_Than_60_Days_Preserving_Recent_And_Metadata()
    {
        var tenantId = Guid.NewGuid();
        var accessor = new CurrentTenantAccessor();
        accessor.SetTenant(tenantId);

        await using var db = new BillingDbContext(fixture.CreateBillingOptions(), accessor);
        var paymentRepo = new ProcessedPaymentWebhookRepository(db);
        var saasRepo = new ProcessedSaasWebhookEventRepository(db);

        // 1. Webhook antigo (> 60 dias)
        var oldWebhook = ProcessedPaymentWebhook.Create(
            tenantId,
            "PagarMe",
            $"evt_old_{Guid.NewGuid():N}",
            "tx_old_111",
            "Processed",
            payloadHash: "hash_old",
            notes: "OS 123",
            rawPayload: "{\"sensitive\":\"credit_card_and_ip_address_payload\"}").Value!;

        // 2. Webhook recente (5 dias atrás)
        var recentWebhook = ProcessedPaymentWebhook.Create(
            tenantId,
            "MercadoPago",
            $"evt_recent_{Guid.NewGuid():N}",
            "tx_recent_222",
            "Processed",
            payloadHash: "hash_recent",
            notes: "OS 456",
            rawPayload: "{\"recent\":\"diagnostics_ok\"}").Value!;

        await paymentRepo.AddAsync(oldWebhook);
        await paymentRepo.AddAsync(recentWebhook);
        await paymentRepo.SaveChangesAsync();

        // Forçar data de recebimento do oldWebhook para 75 dias atrás via raw SQL (já que ReceivedAtUtc é gerado no create)
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE [billing].[ProcessedPaymentWebhooks] SET [ReceivedAtUtc] = {0} WHERE [Id] = {1}",
            DateTimeOffset.UtcNow.AddDays(-75), oldWebhook.Id);

        var purgeService = new BillingWebhookPurgeApplicationService(paymentRepo, saasRepo);

        // Act
        var purgeResult = await purgeService.PurgeOldWebhookPayloadsAsync(retentionDays: 60);

        // Assert
        Assert.True(purgeResult.IsSuccess);
        Assert.True(purgeResult.Value.PaymentWebhooksPurged >= 1);

        // Recarregar do banco
        await using var verifyDb = new BillingDbContext(fixture.CreateBillingOptions(), accessor);
        var reloadedOld = await verifyDb.ProcessedPaymentWebhooks.IgnoreQueryFilters().FirstAsync(w => w.Id == oldWebhook.Id);
        var reloadedRecent = await verifyDb.ProcessedPaymentWebhooks.IgnoreQueryFilters().FirstAsync(w => w.Id == recentWebhook.Id);

        // Webhook antigo higienizado
        Assert.Null(reloadedOld.RawPayload);
        Assert.True(reloadedOld.IsPurged);
        Assert.NotNull(reloadedOld.PurgedAtUtc);
        Assert.Equal("tx_old_111", reloadedOld.TxId);
        Assert.Equal("PagarMe", reloadedOld.Provider);

        // Webhook recente intacto
        Assert.NotNull(reloadedRecent.RawPayload);
        Assert.False(reloadedRecent.IsPurged);
        Assert.Null(reloadedRecent.PurgedAtUtc);
    }

    [Fact]
    public async Task WhatsAppTransientDataPurge_Should_Discard_MediaBase64_Older_Than_60_Days()
    {
        var tenantId = Guid.NewGuid();
        var accessor = new CurrentTenantAccessor();
        accessor.SetTenant(tenantId);

        await using var db = new WhatsAppDbContext(fixture.CreateWhatsAppOptions(), accessor);
        var messageRepo = new OutboundWhatsAppMessageRepository(db);
        var sessionRepo = new ChatbotSessionRepository(db);

        // Mensagem antiga com mídia
        var oldMsg = OutboundWhatsAppMessage.CreateWithMedia(
            tenantId,
            "11988887777",
            "Comprovante Antigo",
            "document",
            "base64_binario_pesado_antigo",
            "application/pdf",
            "recibo.pdf").Value!;
        oldMsg.MarkSent("wamid_old_999");

        // Mensagem recente com mídia
        var recentMsg = OutboundWhatsAppMessage.CreateWithMedia(
            tenantId,
            "11988887777",
            "Comprovante Recente",
            "document",
            "base64_binario_pesado_recente",
            "application/pdf",
            "recibo_recente.pdf").Value!;
        recentMsg.MarkSent("wamid_rec_888");

        await messageRepo.AddAsync(oldMsg);
        await messageRepo.AddAsync(recentMsg);
        await messageRepo.SaveChangesAsync();

        // Forçar CreatedAt para 75 dias atrás na mensagem antiga
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE [whatsapp].[WhatsAppMessages] SET [CreatedAt] = {0} WHERE [Id] = {1}",
            DateTimeOffset.UtcNow.AddDays(-75), oldMsg.Id);

        var purgeService = new WhatsAppTransientDataPurgeApplicationService(messageRepo, sessionRepo);

        // Act
        var purgeResult = await purgeService.PurgeTransientDataAsync(retentionDays: 60);

        // Assert
        Assert.True(purgeResult.IsSuccess);
        Assert.True(purgeResult.Value.MediaMessagesPurged >= 1);

        // Recarregar do banco
        await using var verifyDb = new WhatsAppDbContext(fixture.CreateWhatsAppOptions(), accessor);
        var reloadedOldMsg = await verifyDb.OutboundWhatsAppMessages.IgnoreQueryFilters().FirstAsync(m => m.Id == oldMsg.Id);
        var reloadedRecentMsg = await verifyDb.OutboundWhatsAppMessages.IgnoreQueryFilters().FirstAsync(m => m.Id == recentMsg.Id);

        // Mídia antiga higienizada
        Assert.Null(reloadedOldMsg.MediaUrlOrBase64);
        Assert.Equal("recibo.pdf", reloadedOldMsg.MediaFileName);
        Assert.Equal(WhatsAppMessageStatus.Sent, reloadedOldMsg.Status);

        // Mídia recente preservada
        Assert.NotNull(reloadedRecentMsg.MediaUrlOrBase64);
    }
}
