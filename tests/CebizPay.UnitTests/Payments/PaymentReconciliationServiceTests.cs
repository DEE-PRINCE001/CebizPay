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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="PaymentReconciliationService"/> verifying external polling and state synchronization.
/// </summary>
public sealed class PaymentReconciliationServiceTests
{
    private readonly IPaymentProviderFactory _providerFactory = Substitute.For<IPaymentProviderFactory>();
    private readonly IPaymentProvider _flutterwaveProvider = Substitute.For<IPaymentProvider>();
    private readonly ILedgerPostingService _ledgerPosting = Substitute.For<ILedgerPostingService>();
    private readonly IOutboxService _outbox = Substitute.For<IOutboxService>();

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private PaymentReconciliationService CreateService(ApplicationDbContext dbContext)
    {
        _providerFactory.GetProvider(PaymentProvider.Flutterwave).Returns(_flutterwaveProvider);

        return new PaymentReconciliationService(
            _providerFactory,
            dbContext,
            _ledgerPosting,
            _outbox,
            NullLogger<PaymentReconciliationService>.Instance);
    }

    [Fact]
    public async Task ReconcilePaymentAttempt_ProviderReturnsSuccess_ShouldCompleteBankTransfer()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var ledgerTxnId = Guid.NewGuid();
        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: "CBZPA-REC-001-1",
            amount: 4000m,
            currency: Currency.NGN);
        attempt.MarkProcessing();
        dbContext.PaymentAttempts.Add(attempt);

        var bankTransfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxnId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "044",
            destinationAccountNumber: "1234567890",
            destinationAccountName: "Carol Danvers",
            amount: 4000m,
            currency: Currency.NGN,
            feeAmount: 40m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "REC-001");
        bankTransfer.MarkProcessing();
        dbContext.BankTransfers.Add(bankTransfer);

        await dbContext.SaveChangesAsync();

        _flutterwaveProvider
            .GetPaymentStatusAsync("CBZPA-REC-001-1", Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.Success("FLW_STATUS_CONFIRMED_99"));

        // Act
        var result = await service.ReconcilePaymentAttemptAsync(attempt.Id);

        // Assert
        Assert.Equal(PaymentProviderResultStatus.Success, result.Status);

        var updatedAttempt = await dbContext.PaymentAttempts.FindAsync(attempt.Id);
        Assert.NotNull(updatedAttempt);
        Assert.Equal(PaymentAttemptStatus.Succeeded, updatedAttempt.Status);

        var updatedTransfer = await dbContext.BankTransfers.FindAsync(bankTransfer.Id);
        Assert.NotNull(updatedTransfer);
        Assert.Equal(BankTransferStatus.Completed, updatedTransfer.Status);
    }

    [Fact]
    public async Task ReconcilePaymentAttempt_ProviderReturnsFailure_ShouldTriggerFinancialReversal()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var ledgerTxnId = Guid.NewGuid();
        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: "CBZPA-REC-002-1",
            amount: 2500m,
            currency: Currency.NGN);
        attempt.MarkProcessing();
        dbContext.PaymentAttempts.Add(attempt);

        var bankTransfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxnId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "044",
            destinationAccountNumber: "1234567890",
            destinationAccountName: "Dan Evans",
            amount: 2500m,
            currency: Currency.NGN,
            feeAmount: 25m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "REC-002");
        bankTransfer.MarkProcessing();
        dbContext.BankTransfers.Add(bankTransfer);

        await dbContext.SaveChangesAsync();

        _flutterwaveProvider
            .GetPaymentStatusAsync("CBZPA-REC-002-1", Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.BusinessFailure("INVALID_ACCOUNT", "Account does not exist"));

        // Act
        var result = await service.ReconcilePaymentAttemptAsync(attempt.Id);

        // Assert
        Assert.Equal(PaymentProviderResultStatus.BusinessFailure, result.Status);

        var updatedAttempt = await dbContext.PaymentAttempts.FindAsync(attempt.Id);
        Assert.NotNull(updatedAttempt);
        Assert.Equal(PaymentAttemptStatus.Failed, updatedAttempt.Status);

        // Verify financial reversal was executed
        await _ledgerPosting.Received(1).PostBankTransferReversalCoreAsync(
            bankTransfer.Id,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcilePaymentAttempt_ProviderReturnsTechnicalFailure_MustNOTTriggerLedgerReversal_AndMustPreserveState()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var ledgerTxnId = Guid.NewGuid();
        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: "CBZPA-REC-003-1",
            amount: 7500m,
            currency: Currency.NGN);
        attempt.MarkProcessing();
        dbContext.PaymentAttempts.Add(attempt);

        var bankTransfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxnId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "044",
            destinationAccountNumber: "1234567890",
            destinationAccountName: "Frank Castle",
            amount: 7500m,
            currency: Currency.NGN,
            feeAmount: 50m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "REC-003");
        bankTransfer.MarkProcessing();
        dbContext.BankTransfers.Add(bankTransfer);

        await dbContext.SaveChangesAsync();

        _flutterwaveProvider
            .GetPaymentStatusAsync("CBZPA-REC-003-1", Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.TechnicalFailure("GATEWAY_TIMEOUT", "Provider gateway 504 Gateway Timeout"));

        // Act
        var result = await service.ReconcilePaymentAttemptAsync(attempt.Id);

        // Assert
        Assert.Equal(PaymentProviderResultStatus.TechnicalFailure, result.Status);

        var updatedAttempt = await dbContext.PaymentAttempts.FindAsync(attempt.Id);
        Assert.NotNull(updatedAttempt);
        // Semantics: must be Unknown, NOT Failed
        Assert.Equal(PaymentAttemptStatus.Unknown, updatedAttempt.Status);

        var updatedTransfer = await dbContext.BankTransfers.FindAsync(bankTransfer.Id);
        Assert.NotNull(updatedTransfer);
        // Bank transfer must NOT be marked Failed
        Assert.NotEqual(BankTransferStatus.Failed, updatedTransfer.Status);

        // CRITICAL: Ledger reversal must NEVER be called for technical failure
        await _ledgerPosting.DidNotReceive().PostBankTransferReversalCoreAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcilePaymentAttempt_ProviderReturnsUnknown_MustNOTTriggerLedgerReversal()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var ledgerTxnId = Guid.NewGuid();
        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: "CBZPA-REC-004-1",
            amount: 3000m,
            currency: Currency.NGN);
        attempt.MarkProcessing();
        dbContext.PaymentAttempts.Add(attempt);

        var bankTransfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxnId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "044",
            destinationAccountNumber: "1234567890",
            destinationAccountName: "Tony Stark",
            amount: 3000m,
            currency: Currency.NGN,
            feeAmount: 30m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "REC-004");
        bankTransfer.MarkProcessing();
        dbContext.BankTransfers.Add(bankTransfer);

        await dbContext.SaveChangesAsync();

        _flutterwaveProvider
            .GetPaymentStatusAsync("CBZPA-REC-004-1", Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.Unknown("TRANSACTION_IN_PROGRESS", "Transaction is still being processed"));

        // Act
        var result = await service.ReconcilePaymentAttemptAsync(attempt.Id);

        // Assert
        Assert.Equal(PaymentProviderResultStatus.Unknown, result.Status);

        var updatedAttempt = await dbContext.PaymentAttempts.FindAsync(attempt.Id);
        Assert.NotNull(updatedAttempt);
        Assert.Equal(PaymentAttemptStatus.Unknown, updatedAttempt.Status);

        // CRITICAL: Ledger reversal must NEVER be called for UNKNOWN
        await _ledgerPosting.DidNotReceive().PostBankTransferReversalCoreAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
