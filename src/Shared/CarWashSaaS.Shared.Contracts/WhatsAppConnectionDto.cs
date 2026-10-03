namespace CarWashSaaS.Shared.Contracts;

public sealed record WhatsAppConnectionDto(
    string Status,
    string? QrCode = null,
    DateTimeOffset? UpdatedAt = null)
{
    public bool IsConnected => string.Equals(Status, WhatsAppStatusConstants.Connected, StringComparison.OrdinalIgnoreCase);
    public bool IsConnecting => string.Equals(Status, WhatsAppStatusConstants.Connecting, StringComparison.OrdinalIgnoreCase);
    public bool IsDisconnected => string.Equals(Status, WhatsAppStatusConstants.Disconnected, StringComparison.OrdinalIgnoreCase);
}
