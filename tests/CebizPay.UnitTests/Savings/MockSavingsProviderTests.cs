using CebizPay.Application.Common.Interfaces.Savings;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Savings.Enums;
using CebizPay.Infrastructure.Savings.Providers.Mock;

namespace CebizPay.UnitTests.Savings;

public sealed class MockSavingsProviderTests
{
    [Fact]
    public async Task MockSavingsProvider_FullLifecycleSimulation_Succeeds()
    {
        var provider = new MockSavingsProvider();
        Assert.Equal("Mock", provider.ProviderName);

        // 1. Ensure Customer
        var customerReq = new ExternalCustomerRequest("user_100", "Alice", "Tester", "alice@example.com", "+2348033334444");
        var customer = await provider.EnsureCustomerAsync(customerReq);
        Assert.NotNull(customer.ExternalCustomerId);

        // 2. Product Rates
        var rates = await provider.GetProductRatesAsync(Currency.NGN);
        Assert.NotEmpty(rates);

        // 3. Create Plan
        var planReq = new ExternalCreatePlanRequest(
            customer.ExternalCustomerId,
            "Target Vacation",
            SavingsPlanType.GoalBased,
            Currency.NGN,
            20000m,
            60,
            DateTime.UtcNow.AddDays(60),
            100000m,
            20000m,
            SavingsContributionFrequency.Monthly,
            "idemp_100");
        var plan = await provider.CreatePlanAsync(planReq);
        Assert.NotNull(plan.ExternalPlanId);
        Assert.Equal("active", plan.Status);

        // 4. Fund Plan
        var fundReq = new ExternalFundingRequest(plan.ExternalPlanId, customer.ExternalCustomerId, 20000m, Currency.NGN, "tx_fund_1");
        var fundResult = await provider.FundPlanAsync(fundReq);
        Assert.True(fundResult.IsSettled);

        // 5. Get Position
        var position = await provider.GetPositionAsync(plan.ExternalPlanId);
        Assert.Equal(20000m, position.PrincipalBalance);

        // 6. Liquidate Plan
        var liqReq = new ExternalLiquidationRequest(plan.ExternalPlanId, customer.ExternalCustomerId, 20000m, false, "tx_liq_1");
        var liqResult = await provider.LiquidatePlanAsync(liqReq);
        Assert.True(liqResult.IsCompleted);
        Assert.Equal(20000m, liqResult.NetSettledAmount);
    }
}
