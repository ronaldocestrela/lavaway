namespace CarWashSaaS.Shared.Contracts;

public static class CashTransactionTypeConstants
{
    public const string Income = "Income";
    public const string Bleed = "Bleed";
    public const string Supply = "Supply";

    public static string ToDisplayName(string type) => type switch
    {
        Income => "Receita / Entrada",
        Bleed => "Sangria / Retirada",
        Supply => "Aporte / Suprimento",
        _ => type
    };
}

public sealed record RegisterWorkOrderPaymentRequest(
    Guid WorkOrderId,
    decimal PaidAmount,
    string PaymentMethod,
    decimal? CashReceived = null,
    string? ReferenceNumber = null,
    string? Notes = null);

public sealed record CreateCashMovementRequest(
    string Type,
    decimal Amount,
    string Description);

public sealed record CloseDailyCashRequest(
    DateOnly ClosingDate,
    decimal? ActualCashInDrawer = null,
    string? Notes = null);

public sealed record CashTransactionDto(
    Guid Id,
    Guid TenantId,
    Guid? WorkOrderId,
    string Type,
    string PaymentMethod,
    decimal Amount,
    string Description,
    DateTimeOffset OccurredAtUtc,
    Guid? RegisteredByUserId,
    string? RegisteredByUserName,
    string? ExternalReference);

public sealed record DailyCashSummaryDto(
    DateOnly Date,
    bool IsClosed,
    DateTimeOffset? ClosedAtUtc,
    string? ClosedByUserName,
    decimal TotalIncome,
    decimal TotalPix,
    decimal TotalCash,
    decimal TotalCreditCard,
    decimal TotalDebitCard,
    decimal TotalSupplies,
    decimal TotalBleeds,
    decimal ExpectedCashInDrawer,
    decimal? ActualCashInDrawer,
    decimal? CashDifference,
    string? Notes,
    int TotalTransactionsCount,
    IReadOnlyList<CashTransactionDto> Transactions);

public sealed record DailyCashClosingDto(
    Guid Id,
    Guid TenantId,
    DateOnly ClosingDate,
    DateTimeOffset ClosedAtUtc,
    Guid ClosedByUserId,
    string ClosedByUserName,
    decimal TotalIncome,
    decimal TotalPix,
    decimal TotalCash,
    decimal TotalCreditCard,
    decimal TotalDebitCard,
    decimal TotalSupplies,
    decimal TotalBleeds,
    decimal ExpectedCashInDrawer,
    decimal? ActualCashInDrawer,
    decimal? CashDifference,
    string Status,
    string? Notes);
