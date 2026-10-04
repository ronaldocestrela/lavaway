namespace CarWashSaaS.Shared.Contracts;

public sealed record SendWhatsAppTestMessageRequest(string RecipientPhone, string MessageText);
