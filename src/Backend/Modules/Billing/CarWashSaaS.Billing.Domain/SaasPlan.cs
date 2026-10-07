using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Domain;

public sealed class SaasPlan
{
    private SaasPlan()
    {
    }

    private SaasPlan(
        Guid id,
        SaasPlanTier tier,
        string name,
        string description,
        decimal monthlyPrice,
        int maxWorkOrdersPerCycle,
        int maxWhatsAppMessagesPerCycle,
        bool hasCustomerSubscriptions,
        bool hasLoyalty,
        bool hasCommissions,
        bool hasAiChatbot,
        int maxTeamMembers,
        bool isActive)
    {
        Id = id;
        Tier = tier;
        Name = name;
        Description = description;
        MonthlyPrice = monthlyPrice;
        MaxWorkOrdersPerCycle = maxWorkOrdersPerCycle;
        MaxWhatsAppMessagesPerCycle = maxWhatsAppMessagesPerCycle;
        HasCustomerSubscriptions = hasCustomerSubscriptions;
        HasLoyalty = hasLoyalty;
        HasCommissions = hasCommissions;
        HasAiChatbot = hasAiChatbot;
        MaxTeamMembers = maxTeamMembers;
        IsActive = isActive;
    }

    public Guid Id { get; private set; }
    public SaasPlanTier Tier { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal MonthlyPrice { get; private set; }
    public int MaxWorkOrdersPerCycle { get; private set; }
    public int MaxWhatsAppMessagesPerCycle { get; private set; }
    public bool HasCustomerSubscriptions { get; private set; }
    public bool HasLoyalty { get; private set; }
    public bool HasCommissions { get; private set; }
    public bool HasAiChatbot { get; private set; }
    public int MaxTeamMembers { get; private set; }
    public bool IsActive { get; private set; }

    public static Result<SaasPlan> Create(
        SaasPlanTier tier,
        string name,
        string description,
        decimal monthlyPrice,
        int maxWorkOrdersPerCycle,
        int maxWhatsAppMessagesPerCycle,
        bool hasCustomerSubscriptions,
        bool hasLoyalty,
        bool hasCommissions,
        bool hasAiChatbot,
        int maxTeamMembers,
        bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<SaasPlan>.Failure(new Error("saas_plan.name.required", "Nome do plano é obrigatório.", ErrorType.Validation));
        }

        if (monthlyPrice < 0)
        {
            return Result<SaasPlan>.Failure(new Error("saas_plan.price.invalid", "Preço mensal não pode ser negativo.", ErrorType.Validation));
        }

        return Result<SaasPlan>.Success(new SaasPlan(
            Guid.CreateVersion7(),
            tier,
            name.Trim(),
            description.Trim(),
            monthlyPrice,
            maxWorkOrdersPerCycle,
            maxWhatsAppMessagesPerCycle,
            hasCustomerSubscriptions,
            hasLoyalty,
            hasCommissions,
            hasAiChatbot,
            maxTeamMembers,
            isActive));
    }

    public static IReadOnlyList<SaasPlan> GetStandardPlans()
    {
        return
        [
            new SaasPlan(
                Guid.Parse("01925b6a-0001-7000-8000-000000000001"),
                SaasPlanTier.Basic,
                "Básico",
                "Ideal para lava-jatos iniciantes com operação ágil de pátio e controle essencial.",
                149.00m,
                maxWorkOrdersPerCycle: 150,
                maxWhatsAppMessagesPerCycle: 300,
                hasCustomerSubscriptions: false,
                hasLoyalty: false,
                hasCommissions: false,
                hasAiChatbot: false,
                maxTeamMembers: 3,
                isActive: true),

            new SaasPlan(
                Guid.Parse("01925b6a-0002-7000-8000-000000000002"),
                SaasPlanTier.Pro,
                "Pro",
                "Completo para centros de estética e lava-jatos com clube de assinaturas, fidelidade e WhatsApp.",
                299.00m,
                maxWorkOrdersPerCycle: 600,
                maxWhatsAppMessagesPerCycle: 1500,
                hasCustomerSubscriptions: true,
                hasLoyalty: true,
                hasCommissions: true,
                hasAiChatbot: true,
                maxTeamMembers: 10,
                isActive: true),

            new SaasPlan(
                Guid.Parse("01925b6a-0003-7000-8000-000000000003"),
                SaasPlanTier.Enterprise,
                "Enterprise",
                "Sem limites de volume para operações corporativas, redes de filiais e alto fluxo de veículos.",
                599.00m,
                maxWorkOrdersPerCycle: 0, // 0 = ilimitado
                maxWhatsAppMessagesPerCycle: 0, // 0 = ilimitado
                hasCustomerSubscriptions: true,
                hasLoyalty: true,
                hasCommissions: true,
                hasAiChatbot: true,
                maxTeamMembers: 999,
                isActive: true)
        ];
    }
}
