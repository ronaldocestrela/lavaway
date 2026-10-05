using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Domain;

public sealed class CustomerSubscription : IMustHaveTenant
{
    private readonly List<SubscriptionVehiclePlate> _authorizedPlates = [];
    private readonly List<SubscriptionUsage> _usages = [];

    private CustomerSubscription()
    {
    }

    private CustomerSubscription(
        Guid id,
        Guid tenantId,
        Guid customerId,
        string customerName,
        string customerPhone,
        Guid planId,
        string planName,
        string status,
        DateTimeOffset currentPeriodStartUtc,
        DateTimeOffset currentPeriodEndUtc,
        int totalCreditsInCycle,
        int usedCreditsInCycle,
        string? gatewaySubscriptionId,
        string? cardLastFourDigits,
        string? cardBrand,
        DateTimeOffset createdUtc)
    {
        Id = id;
        TenantId = tenantId;
        CustomerId = customerId;
        CustomerName = customerName;
        CustomerPhone = customerPhone;
        PlanId = planId;
        PlanName = planName;
        Status = status;
        CurrentPeriodStartUtc = currentPeriodStartUtc;
        CurrentPeriodEndUtc = currentPeriodEndUtc;
        TotalCreditsInCycle = totalCreditsInCycle;
        UsedCreditsInCycle = usedCreditsInCycle;
        GatewaySubscriptionId = gatewaySubscriptionId;
        CardLastFourDigits = cardLastFourDigits;
        CardBrand = cardBrand;
        CreatedUtc = createdUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public Guid CustomerId { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public string CustomerPhone { get; private set; } = string.Empty;
    public Guid PlanId { get; private set; }
    public string PlanName { get; private set; } = string.Empty;
    public string Status { get; private set; } = SubscriptionStatusConstants.Active;
    public DateTimeOffset CurrentPeriodStartUtc { get; private set; }
    public DateTimeOffset CurrentPeriodEndUtc { get; private set; }
    public int TotalCreditsInCycle { get; private set; }
    public int UsedCreditsInCycle { get; private set; }
    public string? GatewaySubscriptionId { get; private set; }
    public string? CardLastFourDigits { get; private set; }
    public string? CardBrand { get; private set; }
    public DateTimeOffset CreatedUtc { get; private set; }
    public DateTimeOffset? CanceledAtUtc { get; private set; }
    public string? CancelReason { get; private set; }

    public IReadOnlyCollection<SubscriptionVehiclePlate> AuthorizedPlates => _authorizedPlates.AsReadOnly();
    public IReadOnlyCollection<SubscriptionUsage> Usages => _usages.AsReadOnly();

    public int AvailableCredits => Math.Max(0, TotalCreditsInCycle - UsedCreditsInCycle);

    public bool IsActivePeriod(DateTimeOffset nowUtc) =>
        Status == SubscriptionStatusConstants.Active &&
        nowUtc >= CurrentPeriodStartUtc &&
        nowUtc <= CurrentPeriodEndUtc;

    public static Result<CustomerSubscription> Create(
        Guid tenantId,
        Guid customerId,
        string customerName,
        string customerPhone,
        Guid planId,
        string planName,
        int creditsPerCycle,
        int allowedPlatesLimit,
        IReadOnlyList<string> initialPlates,
        DateTimeOffset periodStartUtc,
        DateTimeOffset periodEndUtc,
        string? gatewaySubscriptionId = null,
        string? cardLastFourDigits = null,
        string? cardBrand = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CustomerSubscription>.Failure(new Error("subscription.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (customerId == Guid.Empty)
        {
            return Result<CustomerSubscription>.Failure(new Error("subscription.customer_required", "Cliente é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(customerName))
        {
            return Result<CustomerSubscription>.Failure(new Error("subscription.customer_name_required", "Nome do cliente é obrigatório.", ErrorType.Validation));
        }

        if (planId == Guid.Empty)
        {
            return Result<CustomerSubscription>.Failure(new Error("subscription.plan_required", "Plano é obrigatório.", ErrorType.Validation));
        }

        if (creditsPerCycle <= 0)
        {
            return Result<CustomerSubscription>.Failure(new Error("subscription.credits_invalid", "Créditos por ciclo devem ser maiores que zero.", ErrorType.Validation));
        }

        if (allowedPlatesLimit <= 0)
        {
            return Result<CustomerSubscription>.Failure(new Error("subscription.plates_limit_invalid", "Limite de placas deve ser de no mínimo 1 veículo.", ErrorType.Validation));
        }

        if (periodEndUtc <= periodStartUtc)
        {
            return Result<CustomerSubscription>.Failure(new Error("subscription.period_invalid", "O fim do período deve ser posterior ao início.", ErrorType.Validation));
        }

        var subscription = new CustomerSubscription(
            Guid.CreateVersion7(),
            tenantId,
            customerId,
            customerName.Trim(),
            customerPhone?.Trim() ?? string.Empty,
            planId,
            planName.Trim(),
            SubscriptionStatusConstants.Active,
            periodStartUtc,
            periodEndUtc,
            creditsPerCycle,
            0,
            gatewaySubscriptionId,
            cardLastFourDigits,
            cardBrand,
            DateTimeOffset.UtcNow);

        if (initialPlates is not null && initialPlates.Count > 0)
        {
            foreach (var plate in initialPlates)
            {
                var addResult = subscription.AddAuthorizedPlate(plate, allowedPlatesLimit, DateTimeOffset.UtcNow);
                if (!addResult.IsSuccess)
                {
                    return Result<CustomerSubscription>.Failure(addResult.Error!);
                }
            }
        }

        return Result<CustomerSubscription>.Success(subscription);
    }

    public Result AddAuthorizedPlate(string plate, int allowedPlatesLimit, DateTimeOffset nowUtc)
    {
        var normalized = SubscriptionVehiclePlate.NormalizePlate(plate);
        if (normalized.Length != 7)
        {
            return Result.Failure(new Error("subscription.plate_invalid", "Placa do veículo deve conter 7 caracteres alfanuméricos.", ErrorType.Validation));
        }

        if (_authorizedPlates.Any(p => p.Plate.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(new Error("subscription.plate_duplicate", $"A placa {normalized} já está autorizada nesta assinatura.", ErrorType.Conflict));
        }

        if (_authorizedPlates.Count >= allowedPlatesLimit)
        {
            return Result.Failure(new Error("subscription.plate_limit_reached", $"Limite máximo de {allowedPlatesLimit} placa(s) autorizada(s) atingido para este plano.", ErrorType.Validation));
        }

        var plateResult = SubscriptionVehiclePlate.Create(TenantId, Id, normalized, nowUtc);
        if (!plateResult.IsSuccess)
        {
            return Result.Failure(plateResult.Error!);
        }

        _authorizedPlates.Add(plateResult.Value!);
        return Result.Success();
    }

    public Result RemoveAuthorizedPlate(string plate)
    {
        var normalized = SubscriptionVehiclePlate.NormalizePlate(plate);
        var existing = _authorizedPlates.FirstOrDefault(p => p.Plate.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return Result.Failure(new Error("subscription.plate_not_found", $"A placa {normalized} não foi encontrada entre as autorizadas.", ErrorType.NotFound));
        }

        _authorizedPlates.Remove(existing);
        return Result.Success();
    }

    public bool HasPlate(string plate)
    {
        var normalized = SubscriptionVehiclePlate.NormalizePlate(plate);
        return _authorizedPlates.Any(p => p.Plate.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public Result<bool> CanConsumeCredit(string plate, DateTimeOffset nowUtc)
    {
        if (Status != SubscriptionStatusConstants.Active)
        {
            return Result<bool>.Failure(new Error("subscription.inactive", $"A assinatura está com status '{SubscriptionStatusConstants.ToDisplayName(Status)}' e não permite consumo.", ErrorType.Validation));
        }

        if (nowUtc < CurrentPeriodStartUtc || nowUtc > CurrentPeriodEndUtc)
        {
            return Result<bool>.Failure(new Error("subscription.period_expired", "A vigência do ciclo atual da assinatura expirou.", ErrorType.Validation));
        }

        var normalized = SubscriptionVehiclePlate.NormalizePlate(plate);
        if (!HasPlate(normalized))
        {
            return Result<bool>.Failure(new Error("subscription.plate_unauthorized", $"A placa {normalized} não está vinculada a esta assinatura.", ErrorType.Validation));
        }

        if (AvailableCredits <= 0)
        {
            return Result<bool>.Failure(new Error("subscription.credits_exhausted", "Todos os créditos de serviço do ciclo atual já foram consumidos.", ErrorType.Validation));
        }

        return Result<bool>.Success(true);
    }

    public Result<SubscriptionUsage> ConsumeCredit(
        string plate,
        Guid? workOrderId,
        string? serviceName,
        DateTimeOffset nowUtc,
        string? notes = null)
    {
        var checkResult = CanConsumeCredit(plate, nowUtc);
        if (!checkResult.IsSuccess)
        {
            return Result<SubscriptionUsage>.Failure(checkResult.Error!);
        }

        if (workOrderId.HasValue && workOrderId.Value != Guid.Empty &&
            _usages.Any(u => u.WorkOrderId == workOrderId.Value))
        {
            return Result<SubscriptionUsage>.Failure(new Error(
                "subscription.work_order_already_used",
                "Crédito de assinatura já foi consumido para esta ordem de serviço.",
                ErrorType.Conflict));
        }

        var normalized = SubscriptionVehiclePlate.NormalizePlate(plate);
        var usageResult = SubscriptionUsage.Create(TenantId, Id, workOrderId, normalized, serviceName, nowUtc, notes);
        if (!usageResult.IsSuccess)
        {
            return Result<SubscriptionUsage>.Failure(usageResult.Error!);
        }

        var usage = usageResult.Value!;
        _usages.Add(usage);
        UsedCreditsInCycle++;

        return Result<SubscriptionUsage>.Success(usage);
    }

    public Result CancelUsageForWorkOrder(Guid workOrderId)
    {
        if (workOrderId == Guid.Empty)
        {
            return Result.Failure(new Error("subscription.work_order_required", "Ordem de serviço é obrigatória.", ErrorType.Validation));
        }

        var usage = _usages.FirstOrDefault(u => u.WorkOrderId == workOrderId);
        if (usage is null)
        {
            return Result.Failure(new Error("subscription.usage_not_found", "Nenhum consumo de crédito localizado para esta ordem de serviço.", ErrorType.NotFound));
        }

        _usages.Remove(usage);
        UsedCreditsInCycle = Math.Max(0, UsedCreditsInCycle - 1);

        return Result.Success();
    }

    public void RenewCycle(DateTimeOffset newStartUtc, DateTimeOffset newEndUtc, int creditsToGrant)
    {
        CurrentPeriodStartUtc = newStartUtc;
        CurrentPeriodEndUtc = newEndUtc;
        TotalCreditsInCycle = creditsToGrant;
        UsedCreditsInCycle = 0;
        Status = SubscriptionStatusConstants.Active;
    }

    public void MarkPastDue()
    {
        Status = SubscriptionStatusConstants.PastDue;
    }

    public void Cancel(string reason, DateTimeOffset canceledAtUtc)
    {
        Status = SubscriptionStatusConstants.Canceled;
        CancelReason = string.IsNullOrWhiteSpace(reason) ? "Cancelado pelo usuário" : reason.Trim();
        CanceledAtUtc = canceledAtUtc;
    }
}
