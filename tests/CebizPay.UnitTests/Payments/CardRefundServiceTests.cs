using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Payments.Events;
using CebizPay.Infrastructure.Payments.Funding;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="CardRefundService"/> request handling, idempotency, provider delegation, and ledger reversal.
/// </summary>
public sealed class CardRefundServiceTests
{
    private readonly ICardPaymentProvider _flwProvider = Substitute.For<ICardPaymentProvider>();
    private readonly ILedgerPostingService _ledgerPosting = Substitute.For<ILedgerPostingService>();
    private readonly IOutboxService _outbox = Substitute.For<IOutboxService>();

    public CardRefundServiceTests()
    {
        _flwProvider.Provider.Returns(PaymentProvider.Flutterwave);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private CardRefundService CreateService(ApplicationDbContext dbContext)
    {
        return new CardRefundService(
            new[] { _flwProvider },
            dbContext,
            _ledgerPosting,
            _outbox,
            NullLogger<CardRefundService>.Instance);
    }

    [Fact]
    public async Task RequestCardRefundAsync_WhenSuccessful_CompletesRefundAndPostsLedgerReversal()
    {
        using var db = CreateDbContext();
        var wallet = Wallet.CreateIndividualWallet("usr_ref_01", Currency.NGN);
        db.Wallets.Add(wallet);

        var funding = FundingTransaction.Create(
            walletId: wallet.Id,
            virtualAccountId: null,
            provider: PaymentProvider.Flutterwave,
            providerTransactionReference: "FLW-CARD-TX-01",
            fundingChannel: FundingChannel.Card,
            amount: 10000m,
            currency: Currency.NGN);
        funding.MarkCompleted(Guid.NewGuid());
        db.FundingTransactions.Add(funding);
        await db.SaveChangesAsync();

        _flwProvider.RefundCardPaymentAsync(Arg.Any<CardRefundRequest>(), Arg.Any<CancellationToken>())
            .Returns(CardRefundResult.Success("flw_refund_ref_123"));

        var ledgerTxn = new LedgerTransaction(LedgerTransactionType.Refund, "REF-123", null, null);
        _ledgerPosting.PostCardRefundReversalCoreAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<Currency>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var refId = callInfo.ArgAt<Guid>(0);
                var r = db.CardRefunds.Find(refId)!;
                r.MarkSucceeded("flw_refund_ref_123", ledgerTxn.Id);
                return (ledgerTxn, r);
            });

        var service = CreateService(db);

        var response = await service.RequestCardRefundAsync(
            fundingTransactionId: funding.Id,
            amount: 5000m,
            reason: "Partial refund requested",
            idempotencyKey: "idem_ref_001",
            actorUserId: "usr_ref_01");

        Assert.NotNull(response);
        Assert.Equal("Succeeded", response.Status);
        Assert.Equal("flw_refund_ref_123", response.ProviderRefundReference);

        _outbox.Received(1).Write(Arg.Any<CardRefundRequestedDomainEvent>());
        _outbox.Received(1).Write(Arg.Any<CardRefundCompletedDomainEvent>());
    }

    [Fact]
    public async Task RequestCardRefundAsync_WhenIdempotentRetry_ReturnsExistingRefund()
    {
        using var db = CreateDbContext();
        var wallet = Wallet.CreateIndividualWallet("usr_ref_02", Currency.NGN);
        db.Wallets.Add(wallet);

        var funding = FundingTransaction.Create(
            walletId: wallet.Id,
            virtualAccountId: null,
            provider: PaymentProvider.Flutterwave,
            providerTransactionReference: "FLW-CARD-TX-02",
            fundingChannel: FundingChannel.Card,
            amount: 8000m,
            currency: Currency.NGN);
        funding.MarkCompleted(Guid.NewGuid());
        db.FundingTransactions.Add(funding);
        await db.SaveChangesAsync();

        _flwProvider.RefundCardPaymentAsync(Arg.Any<CardRefundRequest>(), Arg.Any<CancellationToken>())
            .Returns(CardRefundResult.Success("flw_refund_ref_999"));

        var ledgerTxn = new LedgerTransaction(LedgerTransactionType.Refund, "REF-999", null, null);
        _ledgerPosting.PostCardRefundReversalCoreAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<Currency>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var refId = callInfo.ArgAt<Guid>(0);
                var r = db.CardRefunds.Find(refId)!;
                r.MarkSucceeded("flw_refund_ref_999", ledgerTxn.Id);
                return (ledgerTxn, r);
            });

        var service = CreateService(db);

        var first = await service.RequestCardRefundAsync(funding.Id, 4000m, "Customer duplicate charge", "idem_ref_dup", "usr_ref_02");
        var second = await service.RequestCardRefundAsync(funding.Id, 4000m, "Customer duplicate charge", "idem_ref_dup", "usr_ref_02");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.RefundReference, second.RefundReference);
    }
}
