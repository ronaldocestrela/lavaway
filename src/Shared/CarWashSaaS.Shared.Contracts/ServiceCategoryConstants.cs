namespace CarWashSaaS.Shared.Contracts;

public static class ServiceCategoryConstants
{
    public const string Ducha = "Ducha";
    public const string LavagemCompleta = "Lavagem Completa";
    public const string Higienizacao = "Higienização";
    public const string Polimento = "Polimento";
    public const string Vitrificacao = "Vitrificação";

    public static readonly IReadOnlyList<string> DefaultCategories =
    [
        Ducha,
        LavagemCompleta,
        Higienizacao,
        Polimento,
        Vitrificacao
    ];
}
