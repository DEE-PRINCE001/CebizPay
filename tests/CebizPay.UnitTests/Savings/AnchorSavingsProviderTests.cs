using CebizPay.Application.Common.Interfaces.Savings;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Savings.Enums;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Savings.Providers.Anchor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CebizPay.UnitTests.Savings;

public sealed class AnchorSavingsProviderTests
{
    private readonly IAnchorClient _client = Substitute.For<IAnchorClient>();
    private readonly ILogger<AnchorSavingsProvider> _logger = Substitute.For<ILogger<AnchorSavingsProvider>>();
    private readonly AnchorOptions _options = new()
    {
        Enabled = true,
        ApiKey = "anc_key_123",
        ParentFboAccountId = "anc_fbo_root_999",
        WebhookSecret = "anchor_secret_test"
    };

    private AnchorSavingsProvider CreateSut()
    {
        return new AnchorSavingsProvider(_client, Microsoft.Extensions.Options.Options.Create(_options), _logger);
    }

    [Fact]
    public async Task EnsureCustomerAsync_WhenSuccessful_ReturnsAnchorCustomer()
    {
        // Arrange
        var sut = CreateSut();
        _client.CreateCustomerAsync(Arg.Any<AnchorResourceEnvelope<AnchorResource<AnchorIndividualCustomerAttributes>>>(), Arg.Any<CancellationToken>())
            .Returns(new AnchorResource<AnchorIndividualCustomerAttributes>
            {
                Id = "anc_cst_777",
                Type = "IndividualCustomer",
                Attributes = new AnchorIndividualCustomerAttributes
                {
                    Status = "ACTIVE"
                }
            });

        var req = new ExternalCustomerRequest("usr_1", "John", "Smith", "john@example.com", "+2348022223333", "11122233344");

        // Act
        var result = await sut.EnsureCustomerAsync(req);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("anc_cst_777", result.ExternalCustomerId);
        Assert.True(result.IsKycVerified);
    }

    [Fact]
    public async Task CreatePlanAsync_WhenSuccessful_CreatesSubAccountUnderParentFbo()
    {
        // Arrange
        var sut = CreateSut();
        var maturity = DateTime.UtcNow.AddDays(180);
        _client.CreateSubAccountAsync(Arg.Any<AnchorResourceEnvelope<AnchorResource<AnchorSubAccountAttributes>>>(), Arg.Any<CancellationToken>())
            .Returns(new AnchorResource<AnchorSubAccountAttributes>
            {
                Id = "anc_subacc_101",
                Type = "SubAccount",
                Attributes = new AnchorSubAccountAttributes
                {
                    Status = "ACTIVE"
                }
            });

        var req = new ExternalCreatePlanRequest(
            "anc_cst_777",
            "Fixed Lock 180",
            SavingsPlanType.FixedLock,
            Currency.NGN,
            100000m,
            180,
            maturity,
            null,
            null,
            null,
            "idempotency_2");

        // Act
        var result = await sut.CreatePlanAsync(req);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("anc_subacc_101", result.ExternalPlanId);
        Assert.Equal("ACTIVE", result.Status);
        Assert.Equal(0.14m, result.ConfirmedAnnualRate);
    }

    [Fact]
    public async Task FundPlanAsync_WhenSuccessful_PostsBookTransferFromFboToSubAccount()
    {
        // Arrange
        var sut = CreateSut();
        _client.CreateTransferAsync(Arg.Any<AnchorResourceEnvelope<AnchorResource<AnchorTransferAttributes>>>(), Arg.Any<CancellationToken>())
            .Returns(new AnchorResource<AnchorTransferAttributes>
            {
                Id = "anc_tx_303",
                Type = "Transfer",
                Attributes = new AnchorTransferAttributes
                {
                    Status = "SUCCESSFUL",
                    Amount = 100000m
                }
            });

        var req = new ExternalFundingRequest("anc_subacc_101", "anc_cst_777", 100000m, Currency.NGN, "SD-ref-2");

        // Act
        var result = await sut.FundPlanAsync(req);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("anc_tx_303", result.ExternalTransactionId);
        Assert.True(result.IsSettled);
        Assert.Equal(100000m, result.SettledAmount);
    }

    [Fact]
    public async Task LiquidatePlanAsync_WhenEarlyExit_CalculatesPenaltyAndPostsBookTransferToFbo()
    {
        // Arrange
        var sut = CreateSut();
        _client.CreateTransferAsync(Arg.Any<AnchorResourceEnvelope<AnchorResource<AnchorTransferAttributes>>>(), Arg.Any<CancellationToken>())
            .Returns(new AnchorResource<AnchorTransferAttributes>
            {
                Id = "anc_liq_tx_404",
                Type = "Transfer",
                Attributes = new AnchorTransferAttributes
                {
                    Status = "SUCCESSFUL",
                    Amount = 97500m
                }
            });

        var req = new ExternalLiquidationRequest("anc_subacc_101", "anc_cst_777", 100000m, true, "SW-ref-2");

        // Act
        var result = await sut.LiquidatePlanAsync(req);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("anc_liq_tx_404", result.ExternalTransactionId);
        Assert.Equal(100000m, result.GrossPayout);
        Assert.Equal(2500m, result.PenaltyAmount); // 2.5% penalty
        Assert.Equal(97500m, result.NetSettledAmount);
        Assert.True(result.IsCompleted);
    }
}
