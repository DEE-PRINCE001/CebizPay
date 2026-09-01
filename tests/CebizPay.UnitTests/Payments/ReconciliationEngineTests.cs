using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Auditing;
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

public sealed class ReconciliationEngineTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPaymentProviderFactory _mockProviderFactory = Substitute.For<IPaymentProviderFactory>();
    private readonly IPaymentProvider _mockPaymentProvider = Substitute.For<IPaymentProvider>();
    private readonly IVerificationProviderFactory _mockComplianceFactory = Substitute.For<IVerificationProviderFactory>();
    private readonly ILedgerPostingService _mockLedgerPostingService = Substitute.For<ILedgerPostingService>();
    private readonly IOutboxService _mockOutboxService = Substitute.For<IOutboxService>();
    private readonly ReconciliationMetrics _metrics = new();
    private readonly ReconciliationEngine _sut;

    public ReconciliationEngineTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new ApplicationDbContext(options);

        _mockProviderFactory.GetProvider(Arg.Any<PaymentProvider>())
            .Returns(_mockPaymentProvider);

        _sut = new ReconciliationEngine(
            _mockProviderFactory,
            Enumerable.Empty<ICardPaymentProvider>(),
            _mockComplianceFactory,
            _dbContext,
            _mockLedgerPostingService,
            _mockOutboxService,
            _metrics,
            NullLogger<ReconciliationEngine>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task ReconcilePaymentAttempt_WhenProviderReturnsUnknown_MarksAttemptUnknownWithoutTriggeringReversal()
    {
        var ledgerTxId = Guid.NewGuid();
        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxId,
            provider: PaymentProvider.Monnify,
            attemptNumber: 1,
            requestReference: "CBZ-REQ-001",
            amount: 10000m,
            currency: Currency.NGN);
        attempt.MarkProcessing();
        _dbContext.PaymentAttempts.Add(attempt);
        await _dbContext.SaveChangesAsync();

        _mockPaymentProvider.GetPaymentStatusAsync("CBZ-REQ-001", Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.Unknown("Gateway in-flight"));

        var result = await _sut.ReconcilePaymentAttemptAsync(attempt.Id);

        Assert.Equal(PaymentProviderResultStatus.Unknown, result.Status);

        var refreshed = await _dbContext.PaymentAttempts.FindAsync(attempt.Id);
        Assert.NotNull(refreshed);
        Assert.Equal(PaymentAttemptStatus.Unknown, refreshed.Status);

        // Verification: No reversal called
        await _mockLedgerPostingService.DidNotReceive().PostBankTransferReversalCoreAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcilePaymentAttempt_WhenProviderReturnsSuccess_MarksAttemptAndTransferCompleted()
    {
        var ledgerTxId = Guid.NewGuid();
        var walletId = Guid.NewGuid();

        var bankTransfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxId,
            senderWalletId: walletId,
            destinationBankCode: "058",
            destinationAccountNumber: "0123456789",
            destinationAccountName: "John Doe",
            amount: 5000m,
            currency: Currency.NGN,
            feeAmount: 50m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "CBZ-BT-001");
        bankTransfer.MarkProcessing();
        _dbContext.BankTransfers.Add(bankTransfer);

        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: "CBZ-REQ-002",
            amount: 5000m,
            currency: Currency.NGN);
        attempt.MarkProcessing();
        _dbContext.PaymentAttempts.Add(attempt);

        await _dbContext.SaveChangesAsync();

        _mockPaymentProvider.GetPaymentStatusAsync("CBZ-REQ-002", Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.Success("FLW_REF_999"));

        var result = await ((IReconciliationEngine)_sut).ReconcilePaymentAttemptAsync(attempt.Id);

        Assert.Equal(ReconciliationOutcome.Success, result.Outcome);
        Assert.Equal("FLW_REF_999", result.ProviderReference);

        var refreshedAttempt = await _dbContext.PaymentAttempts.FindAsync(attempt.Id);
        Assert.NotNull(refreshedAttempt);
        Assert.Equal(PaymentAttemptStatus.Succeeded, refreshedAttempt.Status);
        Assert.Equal("FLW_REF_999", refreshedAttempt.ProviderReference);

        var refreshedTransfer = await _dbContext.BankTransfers.FindAsync(bankTransfer.Id);
        Assert.NotNull(refreshedTransfer);
        Assert.Equal(BankTransferStatus.Completed, refreshedTransfer.Status);
    }

    [Fact]
    public async Task ResolveManualReview_WhenConfirmSuccess_UpdatesReconciliationRecordAndAudit()
    {
        var record = ReconciliationRecord.Create(
            reconciliationType: ReconciliationType.PaymentAttempt,
            sourceReference: "CBZ-MANUAL-001",
            provider: "Monnify",
            expectedAmount: 20000m,
            currency: Currency.NGN);
        record.MarkManualReview("Ambiguous amount reported by provider");
        _dbContext.ReconciliationRecords.Add(record);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ResolveManualReviewAsync(
            record.Id,
            ManualReviewDecision.ConfirmSuccess,
            "Bank statement verified manually by compliance officer",
            "ADMIN_USER_42");

        Assert.Equal(ReconciliationOutcome.Success, result.Outcome);

        var refreshed = await _dbContext.ReconciliationRecords.FindAsync(record.Id);
        Assert.NotNull(refreshed);
        Assert.Equal(ReconciliationStatus.ResolvedSuccess, refreshed.Status);

        var auditLog = await _dbContext.AuditLogs.FirstOrDefaultAsync(a => a.ResourceId == record.Id.ToString());
        Assert.NotNull(auditLog);
        Assert.Equal("ADMIN_USER_42", auditLog.ActorId);
        Assert.Equal(AuditActions.ReconciliationManualReview, auditLog.Action);
    }
}
