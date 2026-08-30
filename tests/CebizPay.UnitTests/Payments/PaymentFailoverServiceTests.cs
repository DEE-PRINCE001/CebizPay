using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Payments.Common;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="PaymentFailoverService"/> testing locked failover rules:
/// MONNIFY -> FLUTTERWAVE -> PAYSTACK (Technical Failure only).
/// UNKNOWN requires reconciliation first; BUSINESS FAILURE prohibits failover.
/// </summary>
public sealed class PaymentFailoverServiceTests
{
    private readonly IPaymentProviderFactory _providerFactory = Substitute.For<IPaymentProviderFactory>();
    private readonly IPaymentProvider _monnifyProvider = Substitute.For<IPaymentProvider>();
    private readonly IPaymentProvider _flutterwaveProvider = Substitute.For<IPaymentProvider>();
    private readonly IPaymentProvider _paystackProvider = Substitute.For<IPaymentProvider>();
    private readonly IPaymentRoutingService _routingService = new PaymentRoutingService();
    private readonly ILedgerPostingService _ledgerPosting = Substitute.For<ILedgerPostingService>();
    private readonly IOutboxService _outbox = Substitute.For<IOutboxService>();

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private PaymentFailoverService CreateService(ApplicationDbContext dbContext)
    {
        _providerFactory.GetProvider(PaymentProvider.Monnify).Returns(_monnifyProvider);
        _providerFactory.GetProvider(PaymentProvider.Flutterwave).Returns(_flutterwaveProvider);
        _providerFactory.GetProvider(PaymentProvider.Paystack).Returns(_paystackProvider);

        return new PaymentFailoverService(
            _providerFactory,
            _routingService,
            dbContext,
            _ledgerPosting,
            _outbox,
            NullLogger<PaymentFailoverService>.Instance);
    }

    [Fact]
    public async Task FailoverAsync_MonnifyTechnicalFailure_ShouldInitiateFlutterwaveAttempt2()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var ledgerTxnId = Guid.NewGuid();

        // Attempt #1: Monnify failed with technical error
        var attempt1 = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Monnify,
            attemptNumber: 1,
            requestReference: "CBZBT-TR-100-A1-MONNIFY",
            amount: 7500m,
            currency: Currency.NGN);
        attempt1.MarkProcessing();
        attempt1.MarkFailed("HTTP_500", "Monnify internal server error");
        dbContext.PaymentAttempts.Add(attempt1);

        var bankTransfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxnId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "044",
            destinationAccountNumber: "1234567890",
            destinationAccountName: "Alice Doe",
            amount: 7500m,
            currency: Currency.NGN,
            feeAmount: 50m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "TR-100");
        bankTransfer.MarkProcessing();
        dbContext.BankTransfers.Add(bankTransfer);

        await dbContext.SaveChangesAsync();

        // Flutterwave fallback succeeds
        _flutterwaveProvider
            .InitializePaymentAsync(Arg.Any<PaymentAttempt>(), Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.Success("FLW_TRF_123456"));

        // Act
        var result = await service.FailoverAsync(ledgerTxnId);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(PaymentProvider.Flutterwave, result.FallbackProvider);
        Assert.Equal(PaymentProviderResultStatus.Success, result.ResultStatus);

        // Verify Attempt #2 was created and persisted
        var attempts = await dbContext.PaymentAttempts
            .Where(p => p.LedgerTransactionId == ledgerTxnId)
            .OrderBy(p => p.AttemptNumber)
            .ToListAsync();

        Assert.Equal(2, attempts.Count);
        Assert.Equal(1, attempts[0].AttemptNumber);
        Assert.Equal(PaymentProvider.Monnify, attempts[0].Provider);
        Assert.Equal(PaymentAttemptStatus.Failed, attempts[0].Status);

        Assert.Equal(2, attempts[1].AttemptNumber);
        Assert.Equal(PaymentProvider.Flutterwave, attempts[1].Provider);
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempts[1].Status);
        Assert.Equal("FLW_TRF_123456", attempts[1].ProviderReference);

        var updatedTransfer = await dbContext.BankTransfers.FindAsync(bankTransfer.Id);
        Assert.NotNull(updatedTransfer);
        Assert.Equal(BankTransferStatus.Completed, updatedTransfer.Status);
    }

    [Fact]
    public async Task FailoverAsync_FlutterwaveTechnicalFailure_ShouldInitiatePaystackAttempt3()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var ledgerTxnId = Guid.NewGuid();

        // Attempt #1: Monnify failed with technical error
        var attempt1 = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Monnify,
            attemptNumber: 1,
            requestReference: "CBZBT-TR-101-A1-MONNIFY",
            amount: 5000m,
            currency: Currency.NGN);
        attempt1.MarkProcessing();
        attempt1.MarkFailed("HTTP_503", "Monnify service unavailable");
        dbContext.PaymentAttempts.Add(attempt1);

        // Attempt #2: Flutterwave failed with technical error
        var attempt2 = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 2,
            requestReference: "CBZBT-TR-101-A2-FLUTTERWAVE",
            amount: 5000m,
            currency: Currency.NGN);
        attempt2.MarkProcessing();
        attempt2.MarkFailed("GATEWAY_502_BAD_GATEWAY", "Flutterwave 502 bad gateway");
        dbContext.PaymentAttempts.Add(attempt2);

        var bankTransfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxnId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "058",
            destinationAccountNumber: "0123456789",
            destinationAccountName: "Bob Doe",
            amount: 5000m,
            currency: Currency.NGN,
            feeAmount: 50m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "TR-101");
        bankTransfer.MarkProcessing();
        dbContext.BankTransfers.Add(bankTransfer);

        await dbContext.SaveChangesAsync();

        // Paystack fallback succeeds
        _paystackProvider
            .InitializePaymentAsync(Arg.Any<PaymentAttempt>(), Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.Success("PSTK_TRF_987654"));

        // Act
        var result = await service.FailoverAsync(ledgerTxnId);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(PaymentProvider.Paystack, result.FallbackProvider);
        Assert.Equal(PaymentProviderResultStatus.Success, result.ResultStatus);

        var attempts = await dbContext.PaymentAttempts
            .Where(p => p.LedgerTransactionId == ledgerTxnId)
            .OrderBy(p => p.AttemptNumber)
            .ToListAsync();

        Assert.Equal(3, attempts.Count);
        Assert.Equal(3, attempts[2].AttemptNumber);
        Assert.Equal(PaymentProvider.Paystack, attempts[2].Provider);
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempts[2].Status);
    }

    [Fact]
    public async Task FailoverAsync_PrimaryAlreadySucceeded_ShouldRejectFailover()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var ledgerTxnId = Guid.NewGuid();

        var attempt1 = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Monnify,
            attemptNumber: 1,
            requestReference: "CBZBT-TR-102-A1-MONNIFY",
            amount: 5000m,
            currency: Currency.NGN);
        attempt1.MarkProcessing();
        attempt1.MarkSucceeded("MNFY_REF_9999");
        dbContext.PaymentAttempts.Add(attempt1);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.FailoverAsync(ledgerTxnId);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("already succeeded", result.ErrorMessage);

        var attemptCount = await dbContext.PaymentAttempts.CountAsync(p => p.LedgerTransactionId == ledgerTxnId);
        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task FailoverAsync_BusinessFailure_ShouldRejectAutomaticFailover()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var ledgerTxnId = Guid.NewGuid();

        var attempt1 = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Monnify,
            attemptNumber: 1,
            requestReference: "CBZBT-TR-103-A1-MONNIFY",
            amount: 5000m,
            currency: Currency.NGN);
        attempt1.MarkProcessing();
        attempt1.MarkFailed("INVALID_ACCOUNT", "Destination account does not exist");
        dbContext.PaymentAttempts.Add(attempt1);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.FailoverAsync(ledgerTxnId);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Business Rejection", result.ErrorMessage);

        var attemptCount = await dbContext.PaymentAttempts.CountAsync(p => p.LedgerTransactionId == ledgerTxnId);
        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task FailoverAsync_UnknownOutcome_ShouldRejectFailoverUntilReconciled()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var ledgerTxnId = Guid.NewGuid();

        var attempt1 = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Monnify,
            attemptNumber: 1,
            requestReference: "CBZBT-TR-104-A1-MONNIFY",
            amount: 5000m,
            currency: Currency.NGN);
        attempt1.MarkProcessing();
        attempt1.MarkUnknown("Gateway timeout after 5000ms");
        dbContext.PaymentAttempts.Add(attempt1);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.FailoverAsync(ledgerTxnId);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Reconcile current provider before failover", result.ErrorMessage);
    }

    [Fact]
    public async Task FailoverAsync_PaystackFinalFallbackFails_ShouldTriggerFinancialReversal()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var ledgerTxnId = Guid.NewGuid();

        // Attempt #1: Monnify
        var attempt1 = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Monnify,
            attemptNumber: 1,
            requestReference: "CBZBT-TR-105-A1-MONNIFY",
            amount: 3000m,
            currency: Currency.NGN);
        attempt1.MarkProcessing();
        attempt1.MarkFailed("HTTP_503", "Service unavailable");
        dbContext.PaymentAttempts.Add(attempt1);

        // Attempt #2: Flutterwave
        var attempt2 = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 2,
            requestReference: "CBZBT-TR-105-A2-FLUTTERWAVE",
            amount: 3000m,
            currency: Currency.NGN);
        attempt2.MarkProcessing();
        attempt2.MarkFailed("HTTP_500", "Gateway error");
        dbContext.PaymentAttempts.Add(attempt2);

        var bankTransfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxnId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "044",
            destinationAccountNumber: "1234567890",
            destinationAccountName: "Bob Doe",
            amount: 3000m,
            currency: Currency.NGN,
            feeAmount: 50m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "TR-105");
        bankTransfer.MarkProcessing();
        dbContext.BankTransfers.Add(bankTransfer);

        await dbContext.SaveChangesAsync();

        // Paystack fallback fails
        _paystackProvider
            .InitializePaymentAsync(Arg.Any<PaymentAttempt>(), Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.TechnicalFailure("PSTK_TIMEOUT", "Paystack transfer request timed out"));

        // Act
        var result = await service.FailoverAsync(ledgerTxnId);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(PaymentProviderResultStatus.TechnicalFailure, result.ResultStatus);

        // Verify financial reversal was executed because no further fallbacks exist
        await _ledgerPosting.Received(1).PostBankTransferReversalCoreAsync(
            bankTransfer.Id,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
