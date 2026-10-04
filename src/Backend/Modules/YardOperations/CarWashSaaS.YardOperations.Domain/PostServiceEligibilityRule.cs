using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public static class PostServiceEligibilityRule
{
    private static readonly string[] EligibleCategoryKeywords =
    [
        "polimento",
        "vitrificação",
        "vitrificacao",
        "higienização",
        "higienizacao"
    ];

    private static readonly string[] EligibleNameKeywords =
    [
        "detalhamento",
        "vitrificação",
        "vitrificacao",
        "banco",
        "bancos",
        "polimento",
        "estofado",
        "estofados",
        "couro",
        "coating",
        "cristalização",
        "cristalizacao",
        "higienização",
        "higienizacao"
    ];

    public static bool IsEligible(string? category, string? serviceName)
    {
        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCat = category.Trim().ToLowerInvariant();
            if (EligibleCategoryKeywords.Any(k => normalizedCat.Contains(k)))
            {
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(serviceName))
        {
            var normalizedName = serviceName.Trim().ToLowerInvariant();
            if (EligibleNameKeywords.Any(k => normalizedName.Contains(k)))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsEligible(WorkOrderItem item) =>
        IsEligible(item.ServiceCategory, item.ServiceName);
}
