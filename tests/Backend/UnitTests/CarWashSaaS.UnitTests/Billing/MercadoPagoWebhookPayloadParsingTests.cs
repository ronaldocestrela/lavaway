using System.Text.Json;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class MercadoPagoWebhookPayloadParsingTests
{
    private static string? GetJsonElementAsString(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };

    [Fact]
    public void ParsePayload_WhenMercadoPagoHasNumericIdAndDataId_ShouldExtractStringsWithoutThrowing()
    {
        // Real-world Mercado Pago webhook JSON payload where id and data.id are numbers
        var json = """
        {
            "action": "payment.updated",
            "api_version": "v1",
            "data": {
                "id": 106093845946
            },
            "date_created": "2026-10-09T01:00:00Z",
            "id": 9988776655,
            "live_mode": true,
            "type": "payment",
            "user_id": 123456789
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string? dataId = null;
        string? action = null;
        string? eventId = null;

        if (root.TryGetProperty("data", out var dataElem) && dataElem.TryGetProperty("id", out var idElem))
        {
            dataId ??= GetJsonElementAsString(idElem);
        }
        else if (root.TryGetProperty("id", out var rootId))
        {
            dataId ??= GetJsonElementAsString(rootId);
        }

        if (root.TryGetProperty("action", out var actionElem))
        {
            action = GetJsonElementAsString(actionElem) ?? action;
        }
        else if (root.TryGetProperty("type", out var typeElem))
        {
            action = GetJsonElementAsString(typeElem) ?? action;
        }

        if (root.TryGetProperty("id", out var evIdElem))
        {
            eventId = GetJsonElementAsString(evIdElem);
        }

        Assert.Equal("106093845946", dataId);
        Assert.Equal("payment.updated", action);
        Assert.Equal("9988776655", eventId);
    }

    [Fact]
    public void ParsePayload_WhenMercadoPagoHasStringDataIdAndNumericRootId_ShouldExtractCorrectly()
    {
        var json = """
        {
            "action": "payment.created",
            "api_version": "v1",
            "data": {
                "id": "106093845946"
            },
            "id": 123456789,
            "type": "payment"
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string? dataId = null;
        string? eventId = null;

        if (root.TryGetProperty("data", out var dataElem) && dataElem.TryGetProperty("id", out var idElem))
        {
            dataId ??= GetJsonElementAsString(idElem);
        }

        if (root.TryGetProperty("id", out var evIdElem))
        {
            eventId = GetJsonElementAsString(evIdElem);
        }

        Assert.Equal("106093845946", dataId);
        Assert.Equal("123456789", eventId);
    }
}
