using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CebizPay.UnitTests.Payments;

public sealed class WebhookConcurrencyAndIdempotencyTests
{
    [Fact]
    public async Task InsufficientWalletBalance_DuringRefundReversal_CreatesRecoveryOutstandingWithoutNegativeBalance()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        using var dbContext = new ApplicationDbContext(options);

        var wallet = Wallet.CreateIndividualWallet("IND_123", Currency.NGN);
        // Customer has only 1,000 NGN balance
        wallet.Credit(1000m);
        dbContext.Wallets.Add(wallet);

        var ledgerAccount = LedgerAccount.CreateWalletAccount(wallet.Id, "WALLET_ACCT", Currency.NGN);
        dbContext.LedgerAccounts.Add(ledgerAccount);

        var refund = CardRefund.Create(
            fundingTransactionId: Guid.NewGuid(),
            walletId: wallet.Id,
            provider: PaymentProvider.Flutterwave,
            refundReference: "REF-FLW-999",
            idempotencyKey: "IDEMP-REF-001",
            amount: 5000m, // 5,000 NGN refund requested
            currency: Currency.NGN,
            reason: "Customer dispute");
        dbContext.CardRefunds.Add(refund);

        await dbContext.SaveChangesAsync();

        var ledgerPostingService = new LedgerPostingService(dbContext);

        var (txn, resRefund) = await ledgerPostingService.PostCardRefundReversalCoreAsync(
            refund.Id,
            refund.FundingTransactionId,
            refund.Amount,
            refund.Currency,
            refund.RefundReference,
            refund.ProviderRefundReference,
            "Dispute chargeback from card issuer");

        // Invariant 1: No negative wallet balance
        Assert.Equal(1000m, wallet.AvailableBalance);
        Assert.True(wallet.AvailableBalance >= 0m);

        // Invariant 2: Refund marked as RecoveryOutstanding
        Assert.Equal(CardRefundStatus.RecoveryOutstanding, resRefund.Status);

        // Invariant 3: Durable RecoveryOutstandingRecord created
        var recovery = await dbContext.RecoveryOutstandingRecords.FirstOrDefaultAsync(r => r.SourceReference == refund.RefundReference);
        Assert.NotNull(recovery);
        Assert.Equal(5000m, recovery.AmountOwed);
        Assert.Equal(RecoveryStatus.Pending, recovery.Status);
    }
}
