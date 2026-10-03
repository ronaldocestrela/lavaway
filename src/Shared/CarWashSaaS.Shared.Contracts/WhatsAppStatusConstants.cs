namespace CarWashSaaS.Shared.Contracts;

public static class WhatsAppStatusConstants
{
    public const string Disconnected = "disconnected";
    public const string Connecting = "connecting";
    public const string Connected = "connected";

    public static readonly string[] All = [Disconnected, Connecting, Connected];

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) &&
        (status.Equals(Disconnected, StringComparison.OrdinalIgnoreCase) ||
         status.Equals(Connecting, StringComparison.OrdinalIgnoreCase) ||
         status.Equals(Connected, StringComparison.OrdinalIgnoreCase));
}
