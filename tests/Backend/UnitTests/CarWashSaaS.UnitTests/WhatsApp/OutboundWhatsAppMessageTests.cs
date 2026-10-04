using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.UnitTests.WhatsApp;

public sealed class OutboundWhatsAppMessageTests
{
    [Fact]
    public void Create_Should_Succeed_With_Valid_Data()
    {
        var tenantId = Guid.NewGuid();

        var result = OutboundWhatsAppMessage.Create(tenantId, "(11) 98765-4321", "Olá, seu veículo está pronto!", "idemp-001");

        Assert.True(result.IsSuccess);
        var message = result.Value!;
        Assert.Equal(tenantId, message.TenantId);
        Assert.Equal("11987654321", message.RecipientPhone);
        Assert.Equal("Olá, seu veículo está pronto!", message.Body);
        Assert.Equal("idemp-001", message.IdempotencyKey);
        Assert.Equal(WhatsAppMessageStatus.Queued, message.Status);
    }

    [Fact]
    public void Create_Should_Generate_IdempotencyKey_When_Not_Provided()
    {
        var result = OutboundWhatsAppMessage.Create(Guid.NewGuid(), "11987654321", "Teste sem chave");

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Value!.IdempotencyKey));
    }

    [Fact]
    public void Create_Should_Reject_Empty_Tenant()
    {
        var result = OutboundWhatsAppMessage.Create(Guid.Empty, "11987654321", "Teste");

        Assert.False(result.IsSuccess);
        Assert.Equal("whatsapp.tenant.required", result.Error!.Code);
    }

    [Fact]
    public void Create_Should_Reject_Invalid_Phone()
    {
        var result = OutboundWhatsAppMessage.Create(Guid.NewGuid(), "123", "Teste");

        Assert.False(result.IsSuccess);
        Assert.Equal("whatsapp.recipient.invalid", result.Error!.Code);
    }

    [Fact]
    public void Create_Should_Reject_Empty_Body()
    {
        var result = OutboundWhatsAppMessage.Create(Guid.NewGuid(), "11987654321", "   ");

        Assert.False(result.IsSuccess);
        Assert.Equal("whatsapp.body.invalid", result.Error!.Code);
    }

    [Fact]
    public void MarkSending_Should_Transition_To_Sending()
    {
        var message = OutboundWhatsAppMessage.Create(Guid.NewGuid(), "11987654321", "Teste").Value!;

        var result = message.MarkSending();

        Assert.True(result.IsSuccess);
        Assert.Equal(WhatsAppMessageStatus.Sending, message.Status);
    }

    [Fact]
    public void MarkSent_Should_Transition_To_Sent_And_Record_Attempt()
    {
        var message = OutboundWhatsAppMessage.Create(Guid.NewGuid(), "11987654321", "Teste").Value!;
        message.MarkSending();

        var result = message.MarkSent("evolution-msg-999");

        Assert.True(result.IsSuccess);
        Assert.Equal(WhatsAppMessageStatus.Sent, message.Status);
        Assert.Equal("evolution-msg-999", message.ProviderMessageId);
        Assert.NotNull(message.SentAtUtc);
        Assert.Single(message.DeliveryAttempts);
        Assert.True(message.DeliveryAttempts.First().IsSuccess);
    }

    [Fact]
    public void MarkDelivered_And_MarkRead_Should_Progress_Status()
    {
        var message = OutboundWhatsAppMessage.Create(Guid.NewGuid(), "11987654321", "Teste").Value!;
        message.MarkSent("msg-1");

        var deliveryResult = message.MarkDelivered();
        Assert.True(deliveryResult.IsSuccess);
        Assert.Equal(WhatsAppMessageStatus.Delivered, message.Status);
        Assert.NotNull(message.DeliveredAtUtc);

        var readResult = message.MarkRead();
        Assert.True(readResult.IsSuccess);
        Assert.Equal(WhatsAppMessageStatus.Read, message.Status);
        Assert.NotNull(message.ReadAtUtc);
    }

    [Fact]
    public void RecordAttemptFailure_Should_Handle_Transient_And_Terminal_Failures()
    {
        var message = OutboundWhatsAppMessage.Create(Guid.NewGuid(), "11987654321", "Teste").Value!;

        // Falha transitória
        message.RecordAttemptFailure("timeout", "Falha de rede temporária", 504, isTerminal: false);
        Assert.Equal(WhatsAppMessageStatus.Queued, message.Status);
        Assert.Equal(1, message.AttemptCount);

        // Falha terminal
        message.RecordAttemptFailure("invalid_number", "Número não existe no WhatsApp", 400, isTerminal: true);
        Assert.Equal(WhatsAppMessageStatus.Failed, message.Status);
        Assert.Equal(2, message.AttemptCount);
        Assert.Contains("invalid_number", message.FailureReason);
        Assert.Equal(2, message.DeliveryAttempts.Count);
    }

    [Fact]
    public void MarkRejected_Should_Set_Status_To_Rejected()
    {
        var message = OutboundWhatsAppMessage.Create(Guid.NewGuid(), "11987654321", "Teste").Value!;

        var result = message.MarkRejected("OptedOut: cliente recusou mensagens");

        Assert.True(result.IsSuccess);
        Assert.Equal(WhatsAppMessageStatus.Rejected, message.Status);
        Assert.Equal("OptedOut: cliente recusou mensagens", message.FailureReason);
    }
}
