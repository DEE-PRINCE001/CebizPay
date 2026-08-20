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
/// Unit tests for <see cref="PaymentFailoverService"/> testing locked failover rules, provider dispatch, and state guarantees.
/// </summary>
public sealed class PaymentFailoverServiceTests
{
    private readonly IPaymentProviderFactory _providerFactory = Substitute.For<IPaymentProviderFactory>();
    private readonly IPaymentProvider _paystackProvider = Substitute.For<IPaymentProvider>();
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
        _providerFactory.GetProvider(PaymentProvider.Paystack).Returns(_paystackProvider);

        return new PaymentFailoverService(
            _providerFactory,
            dbContext,
            _ledgerPosting,
            _outbox,
            NullLogger<PaymentFailoverService>.Instance);
    }

    [Fact]
    public async Task FailoverAsync_FlutterwaveTechnicalFailure_ShouldInitiatePaystackAttempt2()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var ledgerTxnId = Guid.NewGuid();

        // Attempt #1: Flutterwave failed with technical error
        var attempt1 = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: "CBZPA-TR-100-1",
            amount: 7500m,
            currency: Currency.NGN);
        attempt1.MarkProcessing();
        attempt1.MarkFailed("GATEWAY_502_BAD_GATEWAY", "Gateway returned 502 Bad Gateway");
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

        // Paystack fallback succeeds
        _paystackProvider
            .InitializePaymentAsync(Arg.Any<PaymentAttempt>(), Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.Success("PSTK_TRF_123456"));

        // Act
        var result = await service.FailoverAsync(ledgerTxnId);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(PaymentProvider.Paystack, result.FallbackProvider);
        Assert.Equal(PaymentProviderResultStatus.Success, result.ResultStatus);

        // Verify Attempt #2 was created and persisted
        var attempts = await dbContext.PaymentAttempts
            .Where(p => p.LedgerTransactionId == ledgerTxnId)
            .OrderBy(p => p.AttemptNumber)
            .ToListAsync();

        Assert.Equal(2, attempts.Count);
        Assert.Equal(1, attempts[0].AttemptNumber);
        Assert.Equal(PaymentProvider.Flutterwave, attempts[0].Provider);
        Assert.Equal(PaymentAttemptStatus.Failed, attempts[0].Status);

        Assert.Equal(2, attempts[1].AttemptNumber);
        Assert.Equal(PaymentProvider.Paystack, attempts[1].Provider);
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempts[1].Status);
        Assert.Equal("PSTK_TRF_123456", attempts[1].ProviderReference);

        var updatedTransfer = await dbContext.BankTransfers.FindAsync(bankTransfer.Id);
        Assert.NotNull(updatedTransfer);
        Assert.Equal(BankTransferStatus.Completed, updatedTransfer.Status);
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
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: "CBZPA-TR-101-1",
            amount: 5000m,
            currency: Currency.NGN);
        attempt1.MarkProcessing();
        attempt1.MarkSucceeded("FLW_REF_9999");
        dbContext.PaymentAttempts.Add(attempt1);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.FailoverAsync(ledgerTxnId);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("already succeeded", result.ErrorMessage);

        // Ensure no attempt #2 was created
        var attemptCount = await dbContext.PaymentAttempts.CountAsync(p => p.LedgerTransactionId == ledgerTxnId);
        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task FailoverAsync_PrimaryBusinessFailure_ShouldRejectAutomaticFailover()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var ledgerTxnId = Guid.NewGuid();

        var attempt1 = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: "CBZPA-TR-102-1",
            amount: 5000m,
            currency: Currency.NGN);
        attempt1.MarkProcessing();
        attempt1.MarkFailed("BUSINESS_REJECTION", "Invalid recipient account number");
        dbContext.PaymentAttempts.Add(attempt1);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.FailoverAsync(ledgerTxnId);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Business Rejection", result.ErrorMessage);

        // Ensure no attempt #2 was created
        var attemptCount = await dbContext.PaymentAttempts.CountAsync(p => p.LedgerTransactionId == ledgerTxnId);
        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task FailoverAsync_PrimaryUnknownOutcome_ShouldRejectFailoverUntilReconciled()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var ledgerTxnId = Guid.NewGuid();

        var attempt1 = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: "CBZPA-TR-103-1",
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
        Assert.Contains("Reconcile primary provider before failover", result.ErrorMessage);
    }

    [Fact]
    public async Task FailoverAsync_PaystackFallbackAlsoFails_ShouldTriggerFinancialReversal()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var ledgerTxnId = Guid.NewGuid();

        var attempt1 = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: "CBZPA-TR-104-1",
            amount: 3000m,
            currency: Currency.NGN);
        attempt1.MarkProcessing();
        attempt1.MarkFailed("GATEWAY_503_SERVICE_UNAVAILABLE", "Service unavailable");
        dbContext.PaymentAttempts.Add(attempt1);

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
            reference: "TR-104");
        bankTransfer.MarkProcessing();
        dbContext.BankTransfers.Add(bankTransfer);

        await dbContext.SaveChangesAsync();

        // Paystack fallback also fails
        _paystackProvider
            .InitializePaymentAsync(Arg.Any<PaymentAttempt>(), Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.TechnicalFailure("PSTK_TIMEOUT", "Paystack transfer request timed out"));

        // Act
        var result = await service.FailoverAsync(ledgerTxnId);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(PaymentProviderResultStatus.TechnicalFailure, result.ResultStatus);

        // Verify financial reversal was executed
        await _ledgerPosting.Received(1).PostBankTransferReversalCoreAsync(
            bankTransfer.Id,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
