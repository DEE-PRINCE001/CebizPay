using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Payments.Monnify;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="MonnifyPaymentProvider"/> bank transfer initialization and status queries.
/// </summary>
public sealed class MonnifyPaymentProviderTransferTests
{
    private readonly IMonnifyClient _mockClient;
    private readonly IOptions<MonnifyOptions> _validOptions;

    public MonnifyPaymentProviderTransferTests()
    {
        _mockClient = Substitute.For<IMonnifyClient>();
        _validOptions = Options.Create(new MonnifyOptions
        {
            ApiKey = "MK_TEST",
            SecretKey = "SK_TEST",
            ContractCode = "12345",
            SourceAccountNumber = "7820123456",
            Enabled = true
        });
    }

    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task InitializePaymentAsync_ValidAttempt_ShouldPassDestinationAccountNameAndReturnSuccess()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var ledgerTxId = Guid.NewGuid();

        var bankTransfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "058",
            destinationAccountNumber: "0123456789",
            destinationAccountName: "Alice Doe",
            amount: 20000m,
            currency: Currency.NGN,
            feeAmount: 50m,
            feePolicyId: null,
            feePolicyVersion: 1,
            reference: "CBZBT-INIT-001");

        dbContext.BankTransfers.Add(bankTransfer);
        await dbContext.SaveChangesAsync();

        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxId,
            provider: PaymentProvider.Monnify,
            attemptNumber: 1,
            requestReference: "CBZBT-INIT-001-A1-MONNIFY",
            amount: 20000m,
            currency: Currency.NGN);

        _mockClient.InitiateTransferAsync(
                "058",
                "0123456789",
                20000m,
                "NGN",
                "CBZBT-INIT-001-A1-MONNIFY",
                Arg.Any<string>(),
                "Alice Doe",
                "7820123456",
                Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.Success("MNFY_REF_SUCCESS_001"));

        var provider = new MonnifyPaymentProvider(_mockClient, dbContext, _validOptions, NullLogger<MonnifyPaymentProvider>.Instance);

        // Act
        var result = await provider.InitializePaymentAsync(attempt);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PaymentProviderResultStatus.Success, result.Status);
        Assert.Equal("MNFY_REF_SUCCESS_001", result.ProviderReference);
        await _mockClient.Received(1).InitiateTransferAsync(
            "058", "0123456789", 20000m, "NGN", "CBZBT-INIT-001-A1-MONNIFY", Arg.Any<string>(), "Alice Doe", "7820123456", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializePaymentAsync_ProviderDisabled_ShouldReturnTechnicalFailure()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var disabledOptions = Options.Create(new MonnifyOptions { Enabled = false });

        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: Guid.NewGuid(),
            provider: PaymentProvider.Monnify,
            attemptNumber: 1,
            requestReference: "CBZBT-DIS-001-A1-MONNIFY",
            amount: 5000m,
            currency: Currency.NGN);

        var provider = new MonnifyPaymentProvider(_mockClient, dbContext, disabledOptions, NullLogger<MonnifyPaymentProvider>.Instance);

        // Act
        var result = await provider.InitializePaymentAsync(attempt);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PaymentProviderResultStatus.TechnicalFailure, result.Status);
        Assert.Equal("PROVIDER_DISABLED", result.FailureCode);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_TransferStatusSuccess_ShouldReturnSuccess()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        _mockClient.GetTransferStatusAsync("CBZBT-STAT-001", Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.Success("MNFY_TX_STAT_001"));

        var provider = new MonnifyPaymentProvider(_mockClient, dbContext, _validOptions, NullLogger<MonnifyPaymentProvider>.Instance);

        // Act
        var result = await provider.GetPaymentStatusAsync("CBZBT-STAT-001");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PaymentProviderResultStatus.Success, result.Status);
        Assert.Equal("MNFY_TX_STAT_001", result.ProviderReference);
    }
}
