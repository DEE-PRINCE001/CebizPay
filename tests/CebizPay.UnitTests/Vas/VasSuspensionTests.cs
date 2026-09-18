using System.Collections;
using System.Linq.Expressions;
using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Interfaces.Vas;
using CebizPay.Application.UseCases.Vas.Commands.PurchaseAirtime;
using CebizPay.Application.UseCases.Vas.Commands.PurchaseData;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Vas;

public sealed class VasSuspensionTests
{
    private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ITransactionPinService _pinService = Substitute.For<ITransactionPinService>();
    private readonly ILedgerPostingService _ledgerService = Substitute.For<ILedgerPostingService>();
    private readonly IIdempotencyService _idempotencyService = Substitute.For<IIdempotencyService>();
    private readonly IOutboxService _outboxService = Substitute.For<IOutboxService>();
    private readonly IVasDuplicateGuard _duplicateGuard = Substitute.For<IVasDuplicateGuard>();
    private readonly IVasPurchaseExecutor _purchaseExecutor = Substitute.For<IVasPurchaseExecutor>();

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
    public async Task PurchaseAirtime_WhenSuspended_ThrowsComplianceRestrictedException()
    {
        // Arrange
        var userId = "user-suspended-airtime";
        _currentUser.UserId.Returns(userId);

        var profile = new IndividualProfile(userId, "Jane", "Doe");
        profile.Suspend("Compliance lock");

        var profileSet = new InMemoryEntitySet<IndividualProfile>(new List<IndividualProfile> { profile });
        _dbContext.IndividualProfiles.Returns(profileSet);

        var handler = new PurchaseAirtimeCommandHandler(
            _dbContext, _currentUser, _pinService, _ledgerService,
            _idempotencyService, _outboxService, _duplicateGuard, _purchaseExecutor);

        var command = new PurchaseAirtimeCommand("08031234567", "MTN", 1000m, "1234", "idem-airtime-1");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ComplianceRestrictedException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("Your account has been suspended. Value-added service purchases are blocked. Please contact support.", ex.Message);
    }

    [Fact]
    public async Task PurchaseData_WhenSuspended_ThrowsComplianceRestrictedException()
    {
        // Arrange
        var userId = "user-suspended-data";
        _currentUser.UserId.Returns(userId);

        var profile = new IndividualProfile(userId, "Jane", "Doe");
        profile.Suspend("Compliance lock");

        var profileSet = new InMemoryEntitySet<IndividualProfile>(new List<IndividualProfile> { profile });
        _dbContext.IndividualProfiles.Returns(profileSet);

        var handler = new PurchaseDataCommandHandler(
            _dbContext, _currentUser, _pinService, _ledgerService,
            _idempotencyService, _outboxService, _duplicateGuard, _purchaseExecutor);

        var command = new PurchaseDataCommand("08031234567", "MTN", "MTN-1GB", 1000m, "1234", "idem-data-1");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ComplianceRestrictedException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("Your account has been suspended. Value-added service purchases are blocked. Please contact support.", ex.Message);
    }
}
