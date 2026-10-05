using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Domain;

public sealed class DailyCashClosing : IMustHaveTenant
{
    private DailyCashClosing()
    {
    }

    private DailyCashClosing(
        Guid id,
        Guid tenantId,
        DateOnly closingDate,
        DateTimeOffset closedAtUtc,
        Guid closedByUserId,
        string closedByUserName,
        decimal totalIncome,
        decimal totalPix,
        decimal totalCash,
        decimal totalCreditCard,
        decimal totalDebitCard,
        decimal totalSupplies,
        decimal totalBleeds,
        decimal expectedCashInDrawer,
        decimal? actualCashInDrawer,
        decimal? cashDifference,
        DailyCashClosingStatus status,
        string? notes)
    {
        Id = id;
        TenantId = tenantId;
        ClosingDate = closingDate;
        ClosedAtUtc = closedAtUtc;
        ClosedByUserId = closedByUserId;
        ClosedByUserName = closedByUserName;
        TotalIncome = totalIncome;
        TotalPix = totalPix;
        TotalCash = totalCash;
        TotalCreditCard = totalCreditCard;
        TotalDebitCard = totalDebitCard;
        TotalSupplies = totalSupplies;
        TotalBleeds = totalBleeds;
        ExpectedCashInDrawer = expectedCashInDrawer;
        ActualCashInDrawer = actualCashInDrawer;
        CashDifference = cashDifference;
        Status = status;
        Notes = notes;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public DateOnly ClosingDate { get; private set; }
    public DateTimeOffset ClosedAtUtc { get; private set; }
    public Guid ClosedByUserId { get; private set; }
    public string ClosedByUserName { get; private set; } = string.Empty;

    public decimal TotalIncome { get; private set; }
    public decimal TotalPix { get; private set; }
    public decimal TotalCash { get; private set; }
    public decimal TotalCreditCard { get; private set; }
    public decimal TotalDebitCard { get; private set; }
    public decimal TotalSupplies { get; private set; }
    public decimal TotalBleeds { get; private set; }
    public decimal ExpectedCashInDrawer { get; private set; }
    public decimal? ActualCashInDrawer { get; private set; }
    public decimal? CashDifference { get; private set; }

    public DailyCashClosingStatus Status { get; private set; }
    public string? Notes { get; private set; }

    public static Result<DailyCashClosing> Close(
        Guid tenantId,
        DateOnly closingDate,
        Guid closedByUserId,
        string closedByUserName,
        decimal totalIncome,
        decimal totalPix,
        decimal totalCash,
        decimal totalCreditCard,
        decimal totalDebitCard,
        decimal totalSupplies,
        decimal totalBleeds,
        decimal? actualCashInDrawer = null,
        string? notes = null,
        DateTimeOffset? closedAtUtc = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<DailyCashClosing>.Failure(new Error("daily_cash_closing.tenant.required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (closingDate == default)
        {
            return Result<DailyCashClosing>.Failure(new Error("daily_cash_closing.date.invalid", "Data de fechamento inválida.", ErrorType.Validation));
        }

        var normalizedUserName = string.IsNullOrWhiteSpace(closedByUserName) ? "Operador" : closedByUserName.Trim();

        var expectedCashInDrawer = totalSupplies + totalCash - totalBleeds;
        decimal? cashDifference = actualCashInDrawer.HasValue
            ? actualCashInDrawer.Value - expectedCashInDrawer
            : null;

        var normalizedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (normalizedNotes?.Length > 500)
        {
            return Result<DailyCashClosing>.Failure(new Error("daily_cash_closing.notes.too_long", "Observações não podem exceder 500 caracteres.", ErrorType.Validation));
        }

        return Result<DailyCashClosing>.Success(new DailyCashClosing(
            Guid.CreateVersion7(),
            tenantId,
            closingDate,
            closedAtUtc ?? DateTimeOffset.UtcNow,
            closedByUserId,
            normalizedUserName,
            totalIncome,
            totalPix,
            totalCash,
            totalCreditCard,
            totalDebitCard,
            totalSupplies,
            totalBleeds,
            expectedCashInDrawer,
            actualCashInDrawer,
            cashDifference,
            DailyCashClosingStatus.Closed,
            normalizedNotes));
    }

    public Result<DailyCashClosing> Reopen(string? reason = null)
    {
        if (Status == DailyCashClosingStatus.Reopened)
        {
            return Result<DailyCashClosing>.Failure(new Error("daily_cash_closing.already_reopened", "O caixa já se encontra reaberto.", ErrorType.Conflict));
        }

        Status = DailyCashClosingStatus.Reopened;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Notes = string.IsNullOrWhiteSpace(Notes)
                ? $"Reaberto: {reason.Trim()}"
                : $"{Notes} | Reaberto: {reason.Trim()}";
        }

        return Result<DailyCashClosing>.Success(this);
    }
}
