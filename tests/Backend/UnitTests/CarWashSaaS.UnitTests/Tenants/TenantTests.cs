using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Domain;

namespace CarWashSaaS.UnitTests.Tenants;

public sealed class TenantTests
{
    [Fact]
    public void Create_ShouldTrimNameAndGenerateId()
    {
        var result = Tenant.Create("  Lavaway Centro  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Lavaway Centro", result.Value!.Name);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldRejectBlankName(string name)
    {
        var result = Tenant.Create(name);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public void Create_ShouldDefaultToTrialStatus()
    {
        var result = Tenant.Create("Lavaway Express");

        Assert.True(result.IsSuccess);
        var tenant = result.Value!;
        Assert.Equal(TenantStatus.Trial, tenant.Status);
        Assert.NotNull(tenant.TrialEndsAtUtc);
        Assert.True(tenant.TrialEndsAtUtc > DateTimeOffset.UtcNow);
        Assert.Equal(tenant.CreatedAtUtc, tenant.StatusChangedAtUtc);
    }

    [Fact]
    public void Activate_ShouldSetStatusToActive()
    {
        var tenant = Tenant.Create("Lavaway VIP").Value!;
        var activateResult = tenant.Activate("Pagamento da assinatura confirmado");

        Assert.True(activateResult.IsSuccess);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal("Pagamento da assinatura confirmado", tenant.StatusReason);
        Assert.True(tenant.StatusChangedAtUtc >= tenant.CreatedAtUtc);
    }

    [Fact]
    public void MarkDelinquent_ShouldSetStatusToDelinquent()
    {
        var tenant = Tenant.Create("Lavaway Premium").Value!;
        tenant.Activate();

        var delinquentResult = tenant.MarkDelinquent("Fatura em atraso há mais de 10 dias");

        Assert.True(delinquentResult.IsSuccess);
        Assert.Equal(TenantStatus.Delinquent, tenant.Status);
        Assert.Equal("Fatura em atraso há mais de 10 dias", tenant.StatusReason);
    }

    [Fact]
    public void Cancel_ShouldSetStatusToCanceled()
    {
        var tenant = Tenant.Create("Lavaway Cancelado").Value!;

        var cancelResult = tenant.Cancel("Solicitação do lojista");

        Assert.True(cancelResult.IsSuccess);
        Assert.Equal(TenantStatus.Canceled, tenant.Status);
        Assert.Equal("Solicitação do lojista", tenant.StatusReason);
    }

    [Fact]
    public void StartTrial_ShouldUpdateTrialEndsAt()
    {
        var tenant = Tenant.Create("Lavaway Novo").Value!;
        var futureDate = DateTimeOffset.UtcNow.AddDays(30);

        var trialResult = tenant.StartTrial(futureDate, "Período promocional estendido");

        Assert.True(trialResult.IsSuccess);
        Assert.Equal(TenantStatus.Trial, tenant.Status);
        Assert.Equal(futureDate, tenant.TrialEndsAtUtc);
        Assert.Equal("Período promocional estendido", tenant.StatusReason);
    }

    [Fact]
    public void ChangeStatus_ShouldAllowAnyExplicitStatusTransition()
    {
        var tenant = Tenant.Create("Lavaway Flex").Value!;

        var result = tenant.ChangeStatus(TenantStatus.Active, "Ativação manual pelo suporte");

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal("Ativação manual pelo suporte", tenant.StatusReason);
    }
}
