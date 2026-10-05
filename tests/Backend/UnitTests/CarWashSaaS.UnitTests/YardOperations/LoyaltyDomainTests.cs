using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class LoyaltyDomainTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _workOrderId = Guid.NewGuid();

    [Fact]
    public void LoyaltyProgram_CreateDefault_ShouldInitializeWithSensibleValues()
    {
        var result = LoyaltyProgram.CreateDefault(_tenantId);

        Assert.True(result.IsSuccess);
        var program = result.Value!;
        Assert.Equal(_tenantId, program.TenantId);
        Assert.True(program.IsEnabled);
        Assert.Equal(10, program.TargetStamps);
        Assert.Equal("Lavagem Completa Grátis", program.RewardTitle);
        Assert.Equal(1, program.ProximityThreshold);
        Assert.True(program.AllServicesEligible);
    }

    [Fact]
    public void LoyaltyProgram_Update_ShouldValidateRules()
    {
        var program = LoyaltyProgram.CreateDefault(_tenantId).Value!;

        var invalidTarget = program.Update(true, 0, "Recompensa", 1, true, null);
        Assert.False(invalidTarget.IsSuccess);
        Assert.Equal(ErrorType.Validation, invalidTarget.Error!.Type);

        var invalidReward = program.Update(true, 10, "", 1, true, null);
        Assert.False(invalidReward.IsSuccess);

        var invalidProximity = program.Update(true, 10, "Recompensa", 10, true, null);
        Assert.False(invalidProximity.IsSuccess);

        var valid = program.Update(true, 5, "Ducha Grátis", 1, false, "Lavagem,Estética");
        Assert.True(valid.IsSuccess);
        Assert.Equal(5, program.TargetStamps);
        Assert.Equal("Ducha Grátis", program.RewardTitle);
        Assert.Equal(1, program.ProximityThreshold);
        Assert.False(program.AllServicesEligible);
        Assert.Equal("Lavagem,Estética", program.EligibleCategoryFilter);
    }

    [Fact]
    public void LoyaltyProgram_IsServiceEligible_ShouldHonorCategoryFilter()
    {
        var program = LoyaltyProgram.CreateDefault(_tenantId).Value!;
        Assert.True(program.IsServiceEligible("Qualquer", "Ducha"));

        program.Update(true, 10, "Recompensa", 1, false, "Lavagem,Estética");
        Assert.True(program.IsServiceEligible("Lavagem", "Lavagem Simples"));
        Assert.True(program.IsServiceEligible("Estética", "Polimento"));
        Assert.False(program.IsServiceEligible("Mecânica", "Troca de Óleo"));
        Assert.False(program.IsServiceEligible(null, "Serviço Geral"));
    }

    [Fact]
    public void CustomerLoyaltyAccount_Create_ShouldInitializeZeroBalance()
    {
        var result = CustomerLoyaltyAccount.Create(_tenantId, _customerId);

        Assert.True(result.IsSuccess);
        var account = result.Value!;
        Assert.Equal(_tenantId, account.TenantId);
        Assert.Equal(_customerId, account.CustomerId);
        Assert.Equal(0, account.Balance);
        Assert.Equal(0, account.TotalEarned);
        Assert.Equal(0, account.TotalRedeemed);
        Assert.Empty(account.Transactions);
    }

    [Fact]
    public void CustomerLoyaltyAccount_CreditStamps_ShouldIncrementBalanceAndRecordTransaction()
    {
        var account = CustomerLoyaltyAccount.Create(_tenantId, _customerId).Value!;

        var txResult = account.CreditStamps(1, _workOrderId, "OS-1001", "Conclusão de serviço");

        Assert.True(txResult.IsSuccess);
        var tx = txResult.Value!;
        Assert.Equal(1, account.Balance);
        Assert.Equal(1, account.TotalEarned);
        Assert.Equal(1, tx.Amount);
        Assert.Equal(1, tx.BalanceAfter);
        Assert.Equal(LoyaltyTransactionType.Accrual, tx.Type);
        Assert.Equal(_workOrderId, tx.WorkOrderId);
        Assert.Single(account.Transactions);
    }

    [Fact]
    public void CustomerLoyaltyAccount_CalculateRemaining_And_IsNearRedemption_ShouldDetectSingleServiceRemaining()
    {
        var account = CustomerLoyaltyAccount.Create(_tenantId, _customerId).Value!;
        const int target = 10;

        // Balance 0
        Assert.Equal(10, account.CalculateRemaining(target));
        Assert.False(account.IsNearRedemption(target, 1));
        Assert.False(account.IsEligibleForReward(target));

        // Credit 8 stamps (Balance = 8) -> 2 remaining -> not near
        account.CreditStamps(8, Guid.NewGuid(), "OS-1");
        Assert.Equal(2, account.CalculateRemaining(target));
        Assert.False(account.IsNearRedemption(target, 1));

        // Credit 1 more stamp (Balance = 9) -> 1 remaining -> NEAR REDEMPTION!
        account.CreditStamps(1, Guid.NewGuid(), "OS-2");
        Assert.Equal(1, account.CalculateRemaining(target));
        Assert.True(account.IsNearRedemption(target, 1));
        Assert.False(account.IsEligibleForReward(target));

        // Credit 1 more stamp (Balance = 10) -> completed target!
        account.CreditStamps(1, Guid.NewGuid(), "OS-3");
        Assert.Equal(0, account.CalculateRemaining(target));
        Assert.False(account.IsNearRedemption(target, 1)); // No longer "near", it is fully eligible!
        Assert.True(account.IsEligibleForReward(target));
    }

    [Fact]
    public void CustomerLoyaltyAccount_RedeemReward_ShouldDeductBalanceWhenSufficient()
    {
        var account = CustomerLoyaltyAccount.Create(_tenantId, _customerId).Value!;
        account.CreditStamps(10, _workOrderId, "OS-1");

        var redeemResult = account.RedeemReward(10, "Lavagem Grátis", "Resgate balcão");

        Assert.True(redeemResult.IsSuccess);
        var tx = redeemResult.Value!;
        Assert.Equal(0, account.Balance);
        Assert.Equal(10, account.TotalRedeemed);
        Assert.Equal(-10, tx.Amount);
        Assert.Equal(0, tx.BalanceAfter);
        Assert.Equal(LoyaltyTransactionType.Redemption, tx.Type);
    }

    [Fact]
    public void CustomerLoyaltyAccount_RedeemReward_ShouldFailWhenInsufficientBalance()
    {
        var account = CustomerLoyaltyAccount.Create(_tenantId, _customerId).Value!;
        account.CreditStamps(9, _workOrderId, "OS-1");

        var redeemResult = account.RedeemReward(10, "Lavagem Grátis");

        Assert.False(redeemResult.IsSuccess);
        Assert.Equal("loyalty.insufficient_balance", redeemResult.Error!.Code);
        Assert.Equal(9, account.Balance);
    }

    [Fact]
    public void CustomerLoyaltyAccount_AdjustBalance_ShouldAllowAdminDeltaAndPreventNegative()
    {
        var account = CustomerLoyaltyAccount.Create(_tenantId, _customerId).Value!;
        account.CreditStamps(5, _workOrderId, "OS-1");

        // Negative adjustment beyond balance should fail
        var invalidAdj = account.AdjustBalance(-6, "Ajuste errado", "Gerente");
        Assert.False(invalidAdj.IsSuccess);
        Assert.Equal("loyalty.balance_cannot_be_negative", invalidAdj.Error!.Code);

        // Valid adjustment
        var validAdj = account.AdjustBalance(-2, "Correção de lançamento duplicado", "Gerente");
        Assert.True(validAdj.IsSuccess);
        Assert.Equal(3, account.Balance);
        Assert.Equal(LoyaltyTransactionType.Adjustment, validAdj.Value!.Type);
    }
}
