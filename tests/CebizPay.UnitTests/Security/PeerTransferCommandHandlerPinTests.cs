using System.Collections;
using System.Linq.Expressions;
using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Wallet.Transfer;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Security;

public sealed class PeerTransferCommandHandlerPinTests
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserLookupService _userLookup;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ITransactionPinService _pinService;
    private readonly IFeePolicyService _feePolicyService;
    private readonly ILedgerPostingService _ledgerService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IOutboxService _outboxService;

    public PeerTransferCommandHandlerPinTests()
    {
        _dbContext = Substitute.For<IApplicationDbContext>();
        _currentUser = Substitute.For<ICurrentUserService>();
        _userLookup = Substitute.For<IUserLookupService>();
        _orgContext = Substitute.For<ICurrentOrganizationContext>();
        _pinService = Substitute.For<ITransactionPinService>();
        _feePolicyService = Substitute.For<IFeePolicyService>();
        _ledgerService = Substitute.For<ILedgerPostingService>();
        _idempotencyService = Substitute.For<IIdempotencyService>();
        _outboxService = Substitute.For<IOutboxService>();
    }

    private sealed class InMemoryEntitySet<T> : IEntitySet<T> where T : class
    {
        private readonly List<T> _items;
        public InMemoryEntitySet(List<T> items) => _items = items;
        public Type ElementType => _items.AsQueryable().ElementType;
        public Expression Expression => _items.AsQueryable().Expression;
        public IQueryProvider Provider => _items.AsQueryable().Provider;
        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(T entity) => _items.Add(entity);
        public void Update(T entity) { }
        public void Remove(T entity) => _items.Remove(entity);
    }

    [Fact]
    public async Task Handle_InvalidPin_ThrowsInvalidPinException_AndPerformsZeroFinancialMutations()
    {
        // Arrange
        var userId = "user-123";
        _currentUser.UserId.Returns(userId);

        var senderWallet = Wallet.CreateIndividualWallet(userId, Currency.NGN);
        senderWallet.Credit(50000m);

        var recipientUserId = "user-456";
        var recipientWallet = Wallet.CreateIndividualWallet(recipientUserId, Currency.NGN);

        var wallets = new List<Wallet> { senderWallet, recipientWallet };
        var mockWallets = new InMemoryEntitySet<Wallet>(wallets);
        _dbContext.Wallets.Returns(mockWallets);

        _userLookup.FindByEmailAsync("recipient@example.com", Arg.Any<CancellationToken>())
            .Returns(new UserSummary(recipientUserId, "recipient@example.com", "+2348000000000"));

        var profiles = new List<IndividualProfile> { new(recipientUserId, "Recipient", "User") };
        var mockProfiles = new InMemoryEntitySet<IndividualProfile>(profiles);
        _dbContext.IndividualProfiles.Returns(mockProfiles);

        // PIN fails
        _pinService.VerifyPinAsync(userId, "0000", Arg.Any<CancellationToken>())
            .Returns((false, false, "Invalid transaction PIN. Attempts remaining: 2."));

        var handler = new PeerTransferCommandHandler(
            _dbContext, _currentUser, _userLookup, _orgContext, _pinService,
            _feePolicyService, _ledgerService, _idempotencyService, _outboxService);

        var command = new PeerTransferCommand(
            RecipientIdentifier: "recipient@example.com",
            Amount: 1000m,
            Currency: "NGN",
            TransactionPin: "0000",
            IdempotencyKey: Guid.NewGuid().ToString());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidPinException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("INVALID_TRANSACTION_PIN", ex.Code);
        Assert.Contains("Invalid transaction PIN", ex.Message);

        // Verify no financial transactions were begun, posted, or outboxed
        await _dbContext.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _ledgerService.DidNotReceive().PostPeerTransferCoreAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(),
            Arg.Any<decimal>(), Arg.Any<Currency>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        _outboxService.DidNotReceive().Write(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_LockedPin_ThrowsPinLockedException_AndPerformsZeroFinancialMutations()
    {
        // Arrange
        var userId = "user-123";
        _currentUser.UserId.Returns(userId);

        var senderWallet = Wallet.CreateIndividualWallet(userId, Currency.NGN);
        senderWallet.Credit(50000m);

        var recipientUserId = "user-456";
        var recipientWallet = Wallet.CreateIndividualWallet(recipientUserId, Currency.NGN);

        var wallets = new List<Wallet> { senderWallet, recipientWallet };
        var mockWallets = new InMemoryEntitySet<Wallet>(wallets);
        _dbContext.Wallets.Returns(mockWallets);

        _userLookup.FindByEmailAsync("recipient@example.com", Arg.Any<CancellationToken>())
            .Returns(new UserSummary(recipientUserId, "recipient@example.com", "+2348000000000"));

        var profiles = new List<IndividualProfile> { new(recipientUserId, "Recipient", "User") };
        var mockProfiles = new InMemoryEntitySet<IndividualProfile>(profiles);
        _dbContext.IndividualProfiles.Returns(mockProfiles);

        _pinService.VerifyPinAsync(userId, "1234", Arg.Any<CancellationToken>())
            .Returns((false, true, "Transaction PIN debit lock activated for 15 minutes due to 3 failed attempts."));

        var handler = new PeerTransferCommandHandler(
            _dbContext, _currentUser, _userLookup, _orgContext, _pinService,
            _feePolicyService, _ledgerService, _idempotencyService, _outboxService);

        var command = new PeerTransferCommand(
            RecipientIdentifier: "recipient@example.com",
            Amount: 1000m,
            Currency: "NGN",
            TransactionPin: "1234",
            IdempotencyKey: Guid.NewGuid().ToString());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PinLockedException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("TRANSFER_PIN_LOCKED", ex.Code);
        Assert.Contains("lock", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Verify zero financial operations
        await _dbContext.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _ledgerService.DidNotReceive().PostPeerTransferCoreAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(),
            Arg.Any<decimal>(), Arg.Any<Currency>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
