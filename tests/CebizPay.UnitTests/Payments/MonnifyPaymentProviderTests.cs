using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Payments.Monnify;
using CebizPay.Infrastructure.Payments.Monnify.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="MonnifyPaymentProvider"/> verifying capability mappings and status transformations.
/// </summary>
public sealed class MonnifyPaymentProviderTests
{
    private readonly IMonnifyClient _monnifyClient = Substitute.For<IMonnifyClient>();
    private readonly IOptions<MonnifyOptions> _options = Options.Create(new MonnifyOptions
    {
        ContractCode = "1234567890",
        Enabled = true
    });

    [Fact]
    public async Task CreateVirtualAccountAsync_SuccessfulAllocation_ShouldReturnSuccessResult()
    {
        // Arrange
        _monnifyClient.CreateReservedAccountAsync(Arg.Any<MonnifyCreateReservedAccountRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MonnifyApiResponse<MonnifyCreateReservedAccountResponseBody>
            {
                RequestSuccessful = true,
                ResponseBody = new MonnifyCreateReservedAccountResponseBody
                {
                    AccountReference = "MNFY_REF_001",
                    AccountName = "Alice Doe",
                    Accounts = new List<MonnifyAccountDetails>
                    {
                        new()
                        {
                            AccountNumber = "9988776655",
                            AccountName = "Alice Doe",
                            BankCode = "035",
                            BankName = "Wema Bank"
                        }
                    }
                }
            });

        var provider = new MonnifyPaymentProvider(_monnifyClient, _options, NullLogger<MonnifyPaymentProvider>.Instance);

        var request = new VirtualAccountCreationRequest(
            OwnerIdentifier: "user-123",
            AccountName: "Alice Doe",
            Email: "alice@example.com",
            PhoneNumber: null,
            Currency: Currency.NGN,
            Bvn: null);

        // Act
        var result = await provider.CreateVirtualAccountAsync(request);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("9988776655", result.AccountNumber);
        Assert.Equal("Wema Bank", result.BankName);
        Assert.Equal("035", result.BankCode);
        Assert.Equal("MNFY_REF_001", result.ProviderReference);
    }

    [Fact]
    public async Task CreateVirtualAccountAsync_WhenDisabled_ShouldReturnFailure()
    {
        // Arrange
        var disabledOptions = Options.Create(new MonnifyOptions { Enabled = false });
        var provider = new MonnifyPaymentProvider(_monnifyClient, disabledOptions, NullLogger<MonnifyPaymentProvider>.Instance);

        var request = new VirtualAccountCreationRequest(
            OwnerIdentifier: "user-123",
            AccountName: "Alice Doe",
            Email: "alice@example.com",
            PhoneNumber: null,
            Currency: Currency.NGN,
            Bvn: null);

        // Act
        var result = await provider.CreateVirtualAccountAsync(request);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("disabled", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateVirtualAccountAsync_WhenNoBankAccountsReturned_ShouldReturnFailure()
    {
        // Arrange
        _monnifyClient.CreateReservedAccountAsync(Arg.Any<MonnifyCreateReservedAccountRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MonnifyApiResponse<MonnifyCreateReservedAccountResponseBody>
            {
                RequestSuccessful = true,
                ResponseBody = new MonnifyCreateReservedAccountResponseBody
                {
                    AccountReference = "MNFY_REF_001",
                    Accounts = new List<MonnifyAccountDetails>()
                }
            });

        var provider = new MonnifyPaymentProvider(_monnifyClient, _options, NullLogger<MonnifyPaymentProvider>.Instance);

        var request = new VirtualAccountCreationRequest(
            OwnerIdentifier: "user-123",
            AccountName: "Alice Doe",
            Email: "alice@example.com",
            PhoneNumber: null,
            Currency: Currency.NGN,
            Bvn: null);

        // Act
        var result = await provider.CreateVirtualAccountAsync(request);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("did not return any allocated bank accounts", result.ErrorMessage);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_PaidTransaction_ShouldReturnSuccessResult()
    {
        // Arrange
        _monnifyClient.GetTransactionDetailsAsync("TX_123", Arg.Any<CancellationToken>())
            .Returns(new MonnifyApiResponse<MonnifyTransactionResponseBody>
            {
                RequestSuccessful = true,
                ResponseBody = new MonnifyTransactionResponseBody
                {
                    TransactionReference = "TX_123",
                    PaymentReference = "PAY_123",
                    PaymentStatus = "PAID",
                    AmountPaid = 10000m
                }
            });

        var provider = new MonnifyPaymentProvider(_monnifyClient, _options, NullLogger<MonnifyPaymentProvider>.Instance);

        // Act
        var result = await provider.GetPaymentStatusAsync("TX_123");

        // Assert
        Assert.Equal(PaymentProviderResultStatus.Success, result.Status);
        Assert.Equal("TX_123", result.ProviderReference);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_FailedTransaction_ShouldReturnBusinessFailure()
    {
        // Arrange
        _monnifyClient.GetTransactionDetailsAsync("TX_FAIL", Arg.Any<CancellationToken>())
            .Returns(new MonnifyApiResponse<MonnifyTransactionResponseBody>
            {
                RequestSuccessful = true,
                ResponseBody = new MonnifyTransactionResponseBody
                {
                    TransactionReference = "TX_FAIL",
                    PaymentStatus = "FAILED"
                }
            });

        var provider = new MonnifyPaymentProvider(_monnifyClient, _options, NullLogger<MonnifyPaymentProvider>.Instance);

        // Act
        var result = await provider.GetPaymentStatusAsync("TX_FAIL");

        // Assert
        Assert.Equal(PaymentProviderResultStatus.BusinessFailure, result.Status);
    }
}
