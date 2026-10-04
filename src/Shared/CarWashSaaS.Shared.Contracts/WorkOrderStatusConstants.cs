namespace CarWashSaaS.Shared.Contracts;

public static class WorkOrderStatusConstants
{
    public const string Waiting = "Waiting";
    public const string InWashing = "InWashing";
    public const string Finishing = "Finishing";
    public const string QualityControl = "QualityControl";
    public const string ReadyForPickup = "ReadyForPickup";

    public static readonly IReadOnlyList<string> OrderedStatuses =
    [
        Waiting,
        InWashing,
        Finishing,
        QualityControl,
        ReadyForPickup
    ];

    public static string ToDisplayName(string status) => status switch
    {
        Waiting => "Aguardando",
        InWashing => "Em Lavagem",
        Finishing => "Secagem / Acabamento",
        QualityControl => "Controle de Qualidade",
        ReadyForPickup => "Pronto para Retirada",
        _ => status
    };
}
