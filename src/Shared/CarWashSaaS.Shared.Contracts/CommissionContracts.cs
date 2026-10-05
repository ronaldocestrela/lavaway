namespace CarWashSaaS.Shared.Contracts;

public sealed record CommissionItemDetailDto(
    Guid WorkOrderId,
    string CustomerName,
    string LicensePlate,
    string ServiceName,
    decimal ServiceAmount,
    decimal CommissionPercentage,
    decimal CommissionAmount,
    DateTimeOffset CompletedAtUtc);

public sealed record TeamMemberCommissionSummaryDto(
    Guid TeamMemberId,
    string FullName,
    string Role,
    int ServicesCount,
    decimal TotalRevenue,
    decimal TotalCommission,
    IReadOnlyList<CommissionItemDetailDto> Items);

public sealed record CommissionReportDto(
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalRevenue,
    decimal TotalCommission,
    int TotalServicesCount,
    IReadOnlyList<TeamMemberCommissionSummaryDto> Collaborators);

public interface ICommissionCalculationLookup
{
    Task<Result<CommissionReportDto>> GetCommissionReportAsync(
        Guid tenantId,
        DateOnly startDate,
        DateOnly endDate,
        Guid? teamMemberId = null,
        CancellationToken ct = default);
}
