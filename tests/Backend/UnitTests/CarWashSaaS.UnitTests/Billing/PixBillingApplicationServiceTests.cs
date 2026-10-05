using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class PixBillingApplicationServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _workOrderId = Guid.NewGuid();

    [Fact]
    public async Task GetOrCreatePixCharge_WhenWorkOrderExists_ShouldGenerateCharge()
    {
        var env = new FakeBillingEnvironment();
        env.SetPaymentSummary(new WorkOrderPaymentSummaryDto(
            _workOrderId,
            _tenantId,
            Guid.NewGuid(),
            "Carlos Silva",
            "11988887777",
            "BRA2E19",
            "SUV",
            "ReadyForPickup",
            150m,
            DateTimeOffset.UtcNow));

        var service = env.CreateService();
        var result = await service.GetOrCreatePixChargeForWorkOrderAsync(_tenantId, _workOrderId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(150m, result.Value.Amount);
        Assert.Equal(PixChargeStatusConstants.Pending, result.Value.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.CopyPasteKey));
        Assert.Single(env.Charges);
    }

    [Fact]
    public async Task GetOrCreatePixCharge_WhenActiveChargeAlreadyExists_ShouldReturnSameCharge()
    {
        var env = new FakeBillingEnvironment();
        env.SetPaymentSummary(new WorkOrderPaymentSummaryDto(
            _workOrderId,
            _tenantId,
            Guid.NewGuid(),
            "Carlos Silva",
            "11988887777",
            "BRA2E19",
            "SUV",
            "ReadyForPickup",
            150m,
            DateTimeOffset.UtcNow));

        var service = env.CreateService();
        var firstResult = await service.GetOrCreatePixChargeForWorkOrderAsync(_tenantId, _workOrderId);
        var secondResult = await service.GetOrCreatePixChargeForWorkOrderAsync(_tenantId, _workOrderId);

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(firstResult.Value!.Id, secondResult.Value!.Id);
        Assert.Single(env.Charges);
    }

    [Fact]
    public async Task GetOrCreatePixCharge_WhenWorkOrderCancelled_ShouldReturnConflict()
    {
        var env = new FakeBillingEnvironment();
        env.SetPaymentSummary(new WorkOrderPaymentSummaryDto(
            _workOrderId,
            _tenantId,
            Guid.NewGuid(),
            "Carlos Silva",
            "11988887777",
            "BRA2E19",
            "SUV",
            "Cancelled",
            150m,
            DateTimeOffset.UtcNow));

        var service = env.CreateService();
        var result = await service.GetOrCreatePixChargeForWorkOrderAsync(_tenantId, _workOrderId);

        Assert.False(result.IsSuccess);
        Assert.Equal("billing.work_order_cancelled", result.Error?.Code);
    }

    [Fact]
    public async Task SendPixChargeToCustomerWhatsApp_WhenValid_ShouldDispatchMessage()
    {
        var env = new FakeBillingEnvironment();
        env.SetPaymentSummary(new WorkOrderPaymentSummaryDto(
            _workOrderId,
            _tenantId,
            Guid.NewGuid(),
            "Mariana Souza",
            "11977776666",
            "XYZ1A23",
            "HatchSedan",
            "ReadyForPickup",
            90m,
            DateTimeOffset.UtcNow));

        var service = env.CreateService();
        var result = await service.SendPixChargeToCustomerWhatsAppAsync(_tenantId, _workOrderId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.WhatsAppSentAtUtc);
        Assert.Single(env.DispatchedMessages);
        Assert.Contains("90,00", env.DispatchedMessages[0].Message);
        Assert.Equal("11977776666", env.DispatchedMessages[0].Phone);
    }

    [Fact]
    public async Task SendPixChargeToCustomerWhatsApp_WhenPhoneMissing_ShouldReturnValidationError()
    {
        var env = new FakeBillingEnvironment();
        env.SetPaymentSummary(new WorkOrderPaymentSummaryDto(
            _workOrderId,
            _tenantId,
            Guid.NewGuid(),
            "Sem Telefone",
            "",
            "XYZ1A23",
            "HatchSedan",
            "ReadyForPickup",
            90m,
            DateTimeOffset.UtcNow));

        var service = env.CreateService();
        var result = await service.SendPixChargeToCustomerWhatsAppAsync(_tenantId, _workOrderId);

        Assert.False(result.IsSuccess);
        Assert.Equal("billing.customer_phone_missing", result.Error?.Code);
        Assert.Empty(env.DispatchedMessages);
    }

    private sealed class FakeBillingEnvironment : IPixChargeRepository, IPixGatewayProvider, IWorkOrderPaymentLookup, IOutboundWhatsAppDispatcher
    {
        public List<PixCharge> Charges { get; } = [];
        public List<(string Phone, string Message)> DispatchedMessages { get; } = [];
        private WorkOrderPaymentSummaryDto? _summary;

        public void SetPaymentSummary(WorkOrderPaymentSummaryDto summary) => _summary = summary;

        public PixBillingApplicationService CreateService() => new(this, this, this, this);

        // IPixChargeRepository
        public Task<PixCharge?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Charges.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id));

        public Task<PixCharge?> GetActiveByWorkOrderIdAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default) =>
            Task.FromResult(Charges.FirstOrDefault(c => c.TenantId == tenantId && c.WorkOrderId == workOrderId && c.Status == "Pending"));

        public Task<PixCharge?> GetLatestByWorkOrderIdAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default) =>
            Task.FromResult(Charges.Where(c => c.TenantId == tenantId && c.WorkOrderId == workOrderId).OrderByDescending(c => c.CreatedAtUtc).FirstOrDefault());

        public Task<PixCharge?> GetByTxIdAsync(Guid tenantId, string txId, CancellationToken ct = default) =>
            Task.FromResult(Charges.FirstOrDefault(c => c.TenantId == tenantId && c.TxId == txId));

        public Task AddAsync(PixCharge charge, CancellationToken ct = default)
        {
            Charges.Add(charge);
            return Task.CompletedTask;
        }

        public void Update(PixCharge charge) { }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;

        // IPixGatewayProvider
        public string ProviderName => "FakeGateway";

        public Task<Result<PixGatewayChargeResponse>> CreateImmediateChargeAsync(PixGatewayChargeRequest request, CancellationToken ct = default)
        {
            var response = new PixGatewayChargeResponse(
                $"TX_{Guid.NewGuid():N}",
                "data:image/svg+xml;base64,mock",
                "00020126580014br.gov.bcb.pixFAKEPIXKEY6304ABCD",
                DateTimeOffset.UtcNow.Add(request.Expiration));

            return Task.FromResult(Result<PixGatewayChargeResponse>.Success(response));
        }

        // IWorkOrderPaymentLookup
        public Task<Result<WorkOrderPaymentSummaryDto>> GetPaymentSummaryAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default)
        {
            if (_summary is not null && _summary.TenantId == tenantId && _summary.WorkOrderId == workOrderId)
            {
                return Task.FromResult(Result<WorkOrderPaymentSummaryDto>.Success(_summary));
            }

            return Task.FromResult(Result<WorkOrderPaymentSummaryDto>.Failure(
                new Error("work_order.not_found", "OS não encontrada.", ErrorType.NotFound)));
        }

        public Task<Result<WorkOrderPaymentSummaryDto?>> GetActiveWorkOrderForCustomerPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default)
        {
            if (_summary is not null && _summary.TenantId == tenantId && _summary.CustomerPhone == customerPhone)
            {
                return Task.FromResult(Result<WorkOrderPaymentSummaryDto?>.Success(_summary));
            }

            return Task.FromResult(Result<WorkOrderPaymentSummaryDto?>.Success(null));
        }

        // IOutboundWhatsAppDispatcher
        public Task<Result<WhatsAppMessageDto>> DispatchTextMessageAsync(Guid tenantId, string recipientPhone, string messageText, string? idempotencyKey = null, CancellationToken ct = default)
        {
            DispatchedMessages.Add((recipientPhone, messageText));
            var dto = new WhatsAppMessageDto(
                Guid.NewGuid(),
                recipientPhone,
                messageText,
                "Queued",
                null,
                0,
                DateTimeOffset.UtcNow,
                null,
                null);

            return Task.FromResult(Result<WhatsAppMessageDto>.Success(dto));
        }

        public Task<Result<WhatsAppMessageDto>> DispatchMediaMessageAsync(Guid tenantId, string recipientPhone, string caption, string mediaType, string mediaUrlOrBase64, string mediaMimeType, string mediaFileName, string? idempotencyKey = null, CancellationToken ct = default) =>
            DispatchTextMessageAsync(tenantId, recipientPhone, caption, idempotencyKey, ct);

        public Task<Result<IReadOnlyList<WhatsAppMessageDto>>> GetRecentMessagesAsync(Guid tenantId, int count = 20, CancellationToken ct = default) =>
            Task.FromResult<Result<IReadOnlyList<WhatsAppMessageDto>>>(Result<IReadOnlyList<WhatsAppMessageDto>>.Success([]));
    }
}
