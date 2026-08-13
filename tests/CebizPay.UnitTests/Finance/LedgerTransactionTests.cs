using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using Xunit;

namespace CebizPay.UnitTests.Finance;

public sealed class LedgerTransactionTests
{
    [Fact]
    public void CreateTransaction_ShouldInitializeAsPending()
    {
        // Act
        var transaction = new LedgerTransaction(LedgerTransactionType.PeerTransfer, description: "Test peer transfer");

        // Assert
        Assert.NotEqual(Guid.Empty, transaction.Id);
        Assert.Equal(LedgerTransactionType.PeerTransfer, transaction.TransactionType);
        Assert.Equal(LedgerTransactionStatus.Pending, transaction.Status);
        Assert.StartsWith("TXN-", transaction.Reference);
    }

    [Fact]
    public void Complete_PendingTransaction_ShouldSetStatusToCompleted()
    {
        // Arrange
        var transaction = new LedgerTransaction(LedgerTransactionType.PeerTransfer);
        var now = DateTime.UtcNow;

        // Act
        transaction.Complete(now);

        // Assert
        Assert.Equal(LedgerTransactionStatus.Completed, transaction.Status);
        Assert.Equal(now, transaction.CompletedAtUtc);
    }

    [Fact]
    public void MarkReversed_CompletedTransaction_ShouldSucceed()
    {
        // Arrange
        var transaction = new LedgerTransaction(LedgerTransactionType.PeerTransfer);
        var now = DateTime.UtcNow;
        transaction.Complete(now);

        // Act
        transaction.MarkReversed(now);

        // Assert
        Assert.Equal(LedgerTransactionStatus.Reversed, transaction.Status);
    }

    [Fact]
    public void MarkReversed_AlreadyReversedTransaction_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var transaction = new LedgerTransaction(LedgerTransactionType.PeerTransfer);
        var now = DateTime.UtcNow;
        transaction.Complete(now);
        transaction.MarkReversed(now);

        // Act & Assert (Second reversal must be rejected)
        var ex = Assert.Throws<InvalidOperationException>(() => transaction.MarkReversed(now));
        Assert.Contains("already been reversed", ex.Message);
    }
}
