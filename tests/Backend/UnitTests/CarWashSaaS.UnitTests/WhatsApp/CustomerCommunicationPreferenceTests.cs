using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.UnitTests.WhatsApp;

public sealed class CustomerCommunicationPreferenceTests
{
    [Fact]
    public void Create_Should_Initialize_OptIn_By_Default()
    {
        var tenantId = Guid.NewGuid();

        var result = CustomerCommunicationPreference.Create(tenantId, "(11) 97777-6666");

        Assert.True(result.IsSuccess);
        var pref = result.Value!;
        Assert.Equal(tenantId, pref.TenantId);
        Assert.Equal("11977776666", pref.NormalizedPhone);
        Assert.True(pref.IsOptedIn);
        Assert.Null(pref.OptedOutAtUtc);
    }

    [Fact]
    public void OptOut_Should_Set_Flag_And_Timestamp()
    {
        var pref = CustomerCommunicationPreference.Create(Guid.NewGuid(), "11977776666").Value!;

        var result = pref.OptOut("Cliente enviou PARAR via WhatsApp");

        Assert.True(result.IsSuccess);
        Assert.False(pref.IsOptedIn);
        Assert.NotNull(pref.OptedOutAtUtc);
        Assert.Equal("Cliente enviou PARAR via WhatsApp", pref.Reason);
    }

    [Fact]
    public void OptIn_Should_Reactivate_Consent()
    {
        var pref = CustomerCommunicationPreference.Create(Guid.NewGuid(), "11977776666").Value!;
        pref.OptOut("Cancelado");

        var result = pref.OptIn();

        Assert.True(result.IsSuccess);
        Assert.True(pref.IsOptedIn);
        Assert.Null(pref.OptedOutAtUtc);
    }
}
