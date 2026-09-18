using System.Collections;
using System.Linq.Expressions;
using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Wallet.Transfer;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Finance;

public sealed class PeerTransferSuspensionTests
{
    private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IUserLookupService _userLookup = Substitute.For<IUserLookupService>();
    private readonly ICurrentOrganizationContext _orgContext = Substitute.For<ICurrentOrganizationContext>();
    private readonly ITransactionPinService _pinService = Substitute.For<ITransactionPinService>();
    private readonly IFeePolicyService _feePolicyService = Substitute.For<IFeePolicyService>();
    private readonly ILedgerPostingService _ledgerService = Substitute.For<ILedgerPostingService>();
    private readonly IIdempotencyService _idempotencyService = Substitute.For<IIdempotencyService>();
    private readonly IOutboxService _outboxService = Substitute.For<IOutboxService>();

    private readonly PeerTransferCommandHandler _handler;

    public PeerTransferSuspensionTests()
    {
        _handler = new PeerTransferCommandHandler(
            _dbContext,
            _currentUser,
            _userLookup,
            _orgContext,
            _pinService,
            _feePolicyService,
            _ledgerService,
            _idempotencyService,
            _outboxService);
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
    public async Task Handle_SuspendedSender_ShouldThrowComplianceRestrictedException()
    {
        // Arrange
        var senderUserId = "user-suspended-sender";
        _currentUser.UserId.Returns(senderUserId);

        var senderProfile = new IndividualProfile(senderUserId, "Jane", "Doe");
        senderProfile.Suspend("AML review");

        var profileSet = new InMemoryEntitySet<IndividualProfile>(new List<IndividualProfile> { senderProfile });
        _dbContext.IndividualProfiles.Returns(profileSet);

        var command = new PeerTransferCommand("recipient@cebizpay.com", 5000m, "NGN", "1234", "idem-key-1");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ComplianceRestrictedException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Equal("Your account has been suspended. Outbound financial transfers are blocked. Please contact support.", ex.Message);
    }

    [Fact]
    public async Task Handle_SuspendedRecipient_ShouldAllowInboundTransfer_AndProceedToPinVerification()
    {
        // Arrange
        var senderUserId = "user-active-sender";
        var recipientUserId = "user-suspended-recipient";
        _currentUser.UserId.Returns(senderUserId);

        var senderProfile = new IndividualProfile(senderUserId, "Active", "Sender");
        var recipientProfile = new IndividualProfile(recipientUserId, "Suspended", "Recipient");
        recipientProfile.Suspend("Account compliance audit");

        var senderWallet = Wallet.CreateIndividualWallet(senderUserId, Currency.NGN);
        senderWallet.Credit(10000m);

        var recipientWallet = Wallet.CreateIndividualWallet(recipientUserId, Currency.NGN);

        var profiles = new List<IndividualProfile> { senderProfile, recipientProfile };
        var wallets = new List<Wallet> { senderWallet, recipientWallet };

        _dbContext.IndividualProfiles.Returns(new InMemoryEntitySet<IndividualProfile>(profiles));
        _dbContext.Wallets.Returns(new InMemoryEntitySet<Wallet>(wallets));

        _userLookup.FindByEmailAsync("recipient@cebizpay.com", Arg.Any<CancellationToken>())
            .Returns(new UserSummary(recipientUserId, "recipient@cebizpay.com", "0815000000"));

        _pinService.VerifyPinAsync(senderUserId, "1234", Arg.Any<CancellationToken>())
            .Returns((false, false, "Invalid transaction PIN."));

        var command = new PeerTransferCommand("recipient@cebizpay.com", 2000m, "NGN", "1234", "idem-key-inbound");

        // Act & Assert: Recipient being suspended does not trigger ComplianceRestrictedException; flow proceeds
        var ex = await Assert.ThrowsAsync<InvalidPinException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Invalid transaction PIN", ex.Message);
    }
}
