using CebizPay.Application.Common.Interfaces.Savings;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Savings.Enums;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Savings.Providers.Cowrywise;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CebizPay.UnitTests.Savings;

public sealed class CowrywiseSavingsProviderTests
{
    private readonly ICowrywiseClient _client = Substitute.For<ICowrywiseClient>();
    private readonly ILogger<CowrywiseSavingsProvider> _logger = Substitute.For<ILogger<CowrywiseSavingsProvider>>();
    private readonly CowrywiseOptions _options = new()
    {
        Enabled = true,
        ClientId = "client_id_123",
        ClientSecret = "client_secret_xyz",
        WebhookSecret = "webhook_secret_test"
    };

    private CowrywiseSavingsProvider CreateSut()
    {
        return new CowrywiseSavingsProvider(_client, Microsoft.Extensions.Options.Options.Create(_options), _logger);
    }

    [Fact]
    public async Task EnsureCustomerAsync_WhenSuccessful_ReturnsCustomerResult()
    {
        // Arrange
        var sut = CreateSut();
        _client.CreateAccountAsync(Arg.Any<CowrywiseCreateAccountRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CowrywiseAccountData
            {
                AccountId = "cw_acc_001",
                WalletId = "cw_wal_001",
                IsVerified = true,
                Status = "active"
            });

        var req = new ExternalCustomerRequest("usr_1", "Jane", "Doe", "jane@example.com", "+2348011112222", "22233344455");

        // Act
        var result = await sut.EnsureCustomerAsync(req);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("cw_acc_001", result.ExternalCustomerId);
        Assert.Equal("cw_wal_001", result.ExternalWalletOrSubAccountId);
        Assert.True(result.IsKycVerified);
    }

    [Fact]
    public async Task CreatePlanAsync_WhenSuccessful_ReturnsPlanResult()
    {
        // Arrange
        var sut = CreateSut();
        var maturity = DateTime.UtcNow.AddDays(90);
        _client.CreateSavingsAsync(Arg.Any<CowrywiseCreateSavingsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CowrywiseSavingsData
            {
                Id = "cw_sav_999",
                Status = "active",
                Rate = 15.0m,
                MaturityDate = maturity
            });

        var req = new ExternalCreatePlanRequest(
            "cw_acc_001",
            "Emergency Fund",
            SavingsPlanType.FixedLock,
            Currency.NGN,
            50000m,
            90,
            maturity,
            null,
            null,
            null,
            "idempotency_1");

        // Act
        var result = await sut.CreatePlanAsync(req);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("cw_sav_999", result.ExternalPlanId);
        Assert.Equal("active", result.Status);
        Assert.Equal(0.15m, result.ConfirmedAnnualRate);
    }

    [Fact]
    public async Task FundPlanAsync_WhenSuccessful_ReturnsFundingResult()
    {
        // Arrange
        var sut = CreateSut();
        _client.FundSavingsAsync("cw_sav_999", Arg.Any<CowrywiseFundSavingsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CowrywiseFundingData
            {
                TransactionId = "cw_tx_123",
                Status = "completed",
                Amount = 50000m
            });

        var req = new ExternalFundingRequest("cw_sav_999", "cw_acc_001", 50000m, Currency.NGN, "SD-ref-1");

        // Act
        var result = await sut.FundPlanAsync(req);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("cw_tx_123", result.ExternalTransactionId);
        Assert.True(result.IsSettled);
        Assert.Equal(50000m, result.SettledAmount);
    }

    [Fact]
    public async Task GetPositionAsync_WhenSuccessful_ReturnsAccuratePosition()
    {
        // Arrange
        var sut = CreateSut();
        _client.GetPositionAsync("cw_sav_999", Arg.Any<CancellationToken>())
            .Returns(new CowrywisePositionData
            {
                Id = "cw_sav_999",
                PrincipalBalance = 50000m,
                AccruedInterest = 1850m,
                TotalYield = 1850m,
                IsMatured = false,
                Status = "active"
            });

        // Act
        var position = await sut.GetPositionAsync("cw_sav_999");

        // Assert
        Assert.NotNull(position);
        Assert.Equal("cw_sav_999", position.ExternalPlanId);
        Assert.Equal(50000m, position.PrincipalBalance);
        Assert.Equal(1850m, position.AccruedInterest);
        Assert.False(position.IsMatured);
    }

    [Fact]
    public async Task LiquidatePlanAsync_WhenSuccessful_ReturnsLiquidationBreakdown()
    {
        // Arrange
        var sut = CreateSut();
        _client.LiquidateSavingsAsync("cw_sav_999", Arg.Any<CowrywiseLiquidationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CowrywiseLiquidationData
            {
                TransactionId = "cw_liq_555",
                GrossAmount = 51850m,
                PenaltyAmount = 1250m,
                ForfeitedInterest = 1850m,
                NetAmount = 48750m,
                Status = "completed"
            });

        var req = new ExternalLiquidationRequest("cw_sav_999", "cw_acc_001", 50000m, true, "SW-ref-1");

        // Act
        var result = await sut.LiquidatePlanAsync(req);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("cw_liq_555", result.ExternalTransactionId);
        Assert.Equal(51850m, result.GrossPayout);
        Assert.Equal(1250m, result.PenaltyAmount);
        Assert.Equal(48750m, result.NetSettledAmount);
        Assert.True(result.IsCompleted);
    }
}
