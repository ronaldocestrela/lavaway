namespace CarWashSaaS.WhatsApp.Domain;

public enum WhatsAppMessageStatus
{
    Queued,
    Sending,
    Sent,
    Delivered,
    Read,
    Failed,
    Rejected
}
