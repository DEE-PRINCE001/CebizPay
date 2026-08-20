using System.Collections;
using System.Linq.Expressions;
using System.Text.Json;
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

public sealed class BankTransferCommandHandlerTests
{
    private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ITransactionPinService _pinService = Substitute.For<ITransactionPinService>();
    private readonly IBankTransferFeePolicyService _feePolicyService = Substitute.For<IBankTransferFeePolicyService>();
    private readonly ILedgerPostingService _ledgerService = Substitute.For<ILedgerPostingService>();
    private readonly IIdempotencyService _idempotencyService = Substitute.For<IIdempotencyService>();
    private readonly IOutboxService _outboxService = Substitute.For<IOutboxService>();
    private readonly IBankAccountResolver _accountResolver = Substitute.For<IBankAccountResolver>();

    private readonly BankTransferCommandHandler _handler;

    public BankTransferCommandHandlerTests()
    {
        _handler = new BankTransferCommandHandler(
            _dbContext,
            _currentUserService,
            _pinService,
            _feePolicyService,
            _ledgerService,
            _idempotencyService,
            _outboxService,
            _accountResolver);
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
    public async Task Handle_UnauthenticatedUser_ShouldThrowUnauthorizedAccessException()
    {
        _currentUserService.UserId.Returns((string?)null);

        var command = new BankTransferCommand("058", "0123456789", 1000m, "NGN", "1234", "key-1");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ReportingCurrency_ShouldThrowArgumentException()
    {
        _currentUserService.UserId.Returns("user-1");

        var command = new BankTransferCommand("058", "0123456789", 1000m, "USD", "1234", "key-1");

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnverifiedIndividual_Transferring50kOrMore_ShouldThrowComplianceRestrictedException()
    {
        // Arrange
        var userId = "user-unverified";
        _currentUserService.UserId.Returns(userId);

        var unverifiedProfile = new IndividualProfile(userId, "John", "Doe");
        // KycStatus is Pending/Unverified

        var profileSet = new InMemoryEntitySet<IndividualProfile>(new List<IndividualProfile> { unverifiedProfile });
        _dbContext.IndividualProfiles.Returns(profileSet);

        var command = new BankTransferCommand("058", "0123456789", 50000m, "NGN", "1234", "key-1");

        // Act & Assert
        await Assert.ThrowsAsync<ComplianceRestrictedException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PinLocked_ShouldThrowPinLockedException()
    {
        // Arrange
        var userId = "user-locked";
        _currentUserService.UserId.Returns(userId);

        var profile = new IndividualProfile(userId, "John", "Doe");
        profile.SetKycStatus(KycStatus.Verified);

        var wallet = Wallet.CreateIndividualWallet(userId, Currency.NGN);
        wallet.Credit(100000m);

        var profileSet = new InMemoryEntitySet<IndividualProfile>(new List<IndividualProfile> { profile });
        var walletSet = new InMemoryEntitySet<Wallet>(new List<Wallet> { wallet });

        _dbContext.IndividualProfiles.Returns(profileSet);
        _dbContext.Wallets.Returns(walletSet);

        _accountResolver.ResolveAsync("058", "0123456789", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new BankAccountResolutionResult(true, "John Doe", "058", "0123456789")));

        _pinService.VerifyPinAsync(userId, "1234", Arg.Any<CancellationToken>())
            .Returns((false, true, "Transaction PIN debit lock is active."));

        var command = new BankTransferCommand("058", "0123456789", 1000m, "NGN", "1234", "key-1");

        // Act & Assert
        await Assert.ThrowsAsync<PinLockedException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_InsufficientBalance_ShouldThrowInsufficientFundsException()
    {
        // Arrange
        var userId = "user-poor";
        _currentUserService.UserId.Returns(userId);

        var profile = new IndividualProfile(userId, "John", "Doe");
        profile.SetKycStatus(KycStatus.Verified);

        var wallet = Wallet.CreateIndividualWallet(userId, Currency.NGN);
        wallet.Credit(500m); // Balance = 500

        var profileSet = new InMemoryEntitySet<IndividualProfile>(new List<IndividualProfile> { profile });
        var walletSet = new InMemoryEntitySet<Wallet>(new List<Wallet> { wallet });

        _dbContext.IndividualProfiles.Returns(profileSet);
        _dbContext.Wallets.Returns(walletSet);

        _accountResolver.ResolveAsync("058", "0123456789", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new BankAccountResolutionResult(true, "John Doe", "058", "0123456789")));

        _pinService.VerifyPinAsync(userId, "1234", Arg.Any<CancellationToken>())
            .Returns((true, false, null));

        _feePolicyService.GetActivePolicyAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BankTransferFeePolicy?>(null)); // Free

        var command = new BankTransferCommand("058", "0123456789", 1000m, "NGN", "1234", "key-1"); // Transfer = 1000 > 500

        // Act & Assert
        await Assert.ThrowsAsync<InsufficientFundsException>(() => _handler.Handle(command, CancellationToken.None));
    }
}

