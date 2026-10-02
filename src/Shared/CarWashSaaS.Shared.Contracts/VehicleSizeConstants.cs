namespace CarWashSaaS.Shared.Contracts;

public static class VehicleSizeConstants
{
    public const string HatchSedan = "HatchSedan";
    public const string Suv = "Suv";
    public const string PickupVan = "PickupVan";
    public const string Motorcycle = "Motorcycle";

    public static readonly IReadOnlyList<string> All =
    [
        HatchSedan,
        Suv,
        PickupVan,
        Motorcycle
    ];

    public static string GetDisplayName(string? size) => size?.Trim() switch
    {
        HatchSedan => "Hatch / Sedan",
        Suv => "SUV",
        PickupVan => "Picape / Van",
        Motorcycle => "Moto",
        _ => size ?? string.Empty
    };
}
