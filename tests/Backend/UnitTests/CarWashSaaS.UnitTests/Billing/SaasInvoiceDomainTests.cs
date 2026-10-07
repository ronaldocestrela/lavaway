using CarWashSaaS.Billing.Domain;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class SaasInvoiceDomainTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void CreatePending_Should_Initialize_Invoice_Correctly()
    {
        var due = DateTimeOffset.UtcNow.AddDays(3);
        var result = SaasInvoice.CreatePending(_tenantId, "inv_123", 149.00m, due, "https://pay.lavaway.com/123", "pix_qr", "pix_copia");

        Assert.True(result.IsSuccess);
        var inv = result.Value!;
        Assert.Equal(_tenantId, inv.TenantId);
        Assert.Equal("inv_123", inv.GatewayInvoiceId);
        Assert.Equal(149.00m, inv.Amount);
        Assert.Equal("Pending", inv.Status);
        Assert.Equal("pix_qr", inv.PixQrCode);
        Assert.Null(inv.PaidAtUtc);
    }

    [Fact]
    public void MarkPaid_Should_Set_Status_And_Timestamp()
    {
        var due = DateTimeOffset.UtcNow.AddDays(3);
        var inv = SaasInvoice.CreatePending(_tenantId, "inv_123", 299.00m, due).Value!;

        var paidAt = DateTimeOffset.UtcNow;
        var r = inv.MarkPaid(paidAt);

        Assert.True(r.IsSuccess);
        Assert.Equal("Paid", inv.Status);
        Assert.Equal(paidAt, inv.PaidAtUtc);
    }

    [Fact]
    public void MarkOverdue_Should_Fail_If_Already_Paid()
    {
        var due = DateTimeOffset.UtcNow.AddDays(3);
        var inv = SaasInvoice.CreatePending(_tenantId, "inv_123", 299.00m, due).Value!;
        inv.MarkPaid();

        var r = inv.MarkOverdue();
        Assert.False(r.IsSuccess);
        Assert.Equal("saas_invoice.already_paid", r.Error!.Code);
    }
}
