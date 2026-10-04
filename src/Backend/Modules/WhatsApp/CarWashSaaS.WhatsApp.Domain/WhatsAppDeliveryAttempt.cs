namespace CarWashSaaS.WhatsApp.Domain;

public sealed class WhatsAppDeliveryAttempt
{
    private WhatsAppDeliveryAttempt()
    {
    }

    internal WhatsAppDeliveryAttempt(int attemptNumber, bool isSuccess, string? errorCode, string? errorMessage, int? httpStatusCode)
    {
        Id = Guid.CreateVersion7();
        AttemptNumber = attemptNumber;
        AttemptedAtUtc = DateTimeOffset.UtcNow;
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        HttpStatusCode = httpStatusCode;
    }

    public Guid Id { get; private set; }
    public Guid OutboundWhatsAppMessageId { get; private set; }
    public int AttemptNumber { get; private set; }
    public DateTimeOffset AttemptedAtUtc { get; private set; }
    public bool IsSuccess { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int? HttpStatusCode { get; private set; }
}
