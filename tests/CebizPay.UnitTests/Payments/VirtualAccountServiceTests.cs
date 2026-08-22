using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Payments.Events;
using CebizPay.Infrastructure.Payments.VirtualAccounts;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="VirtualAccountService"/> provisioning, idempotency, and lookups.
/// </summary>
public sealed class VirtualAccountServiceTests
{
    private readonly IVirtualAccountProvider _flwProvider = Substitute.For<IVirtualAccountProvider>();
    private readonly IVirtualAccountProvider _pstkProvider = Substitute.For<IVirtualAccountProvider>();
    private readonly IOutboxService _outbox = Substitute.For<IOutboxService>();

    public VirtualAccountServiceTests()
    {
        _flwProvider.Provider.Returns(PaymentProvider.Flutterwave);
        _pstkProvider.Provider.Returns(PaymentProvider.Paystack);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private VirtualAccountService CreateService(ApplicationDbContext dbContext)
    {
        return new VirtualAccountService(
            new[] { _flwProvider, _pstkProvider },
            dbContext,
            _outbox,
            NullLogger<VirtualAccountService>.Instance);
    }

    [Fact]
    public async Task ProvisionIndividualVirtualAccountAsync_WhenNew_CallsProviderAndPersists()
    {
        // Arrange
        using var db = CreateDbContext();
        var profile = new IndividualProfile("usr_456", "Jane", "Doe");
        db.IndividualProfiles.Add(profile);
        await db.SaveChangesAsync();

        _flwProvider.CreateVirtualAccountAsync(Arg.Any<VirtualAccountCreationRequest>(), Arg.Any<CancellationToken>())
            .Returns(VirtualAccountCreationResult.Success("0112233445", "Jane Doe", "035", "Wema Bank", "flw_va_1"));

        var service = CreateService(db);

        // Act
        var result = await service.ProvisionIndividualVirtualAccountAsync("usr_456", Currency.NGN, PaymentProvider.Flutterwave);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("0112233445", result.AccountNumber);
        Assert.Equal("Jane Doe", result.AccountName);
        Assert.Equal("035", result.BankCode);
        Assert.Equal(PaymentProvider.Flutterwave, result.Provider);
        Assert.Equal(VirtualAccountStatus.Active, result.Status);

        var persisted = await db.VirtualAccounts.FirstOrDefaultAsync(v => v.IndividualId == "usr_456");
        Assert.NotNull(persisted);
        Assert.Equal("0112233445", persisted.AccountNumber);

        _outbox.Received(1).Write(Arg.Any<VirtualAccountProvisionedDomainEvent>());
    }

    [Fact]
    public async Task ProvisionIndividualVirtualAccountAsync_WhenAlreadyExists_ReturnsExistingWithoutCallingProvider()
    {
        // Arrange
        using var db = CreateDbContext();
        var existing = VirtualAccount.CreateIndividual(
            individualId: "usr_existing",
            provider: PaymentProvider.Flutterwave,
            accountNumber: "9988776655",
            accountName: "Existing User",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN);
        db.VirtualAccounts.Add(existing);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var result = await service.ProvisionIndividualVirtualAccountAsync("usr_existing", Currency.NGN, PaymentProvider.Flutterwave);

        // Assert
        Assert.Equal("9988776655", result.AccountNumber);
        await _flwProvider.DidNotReceive().CreateVirtualAccountAsync(Arg.Any<VirtualAccountCreationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProvisionOrganizationVirtualAccountAsync_WhenNew_CallsProviderAndPersists()
    {
        // Arrange
        using var db = CreateDbContext();
        var org = new Organization("Acme Ltd", "acme@example.com", "08012345678");
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        _pstkProvider.CreateVirtualAccountAsync(Arg.Any<VirtualAccountCreationRequest>(), Arg.Any<CancellationToken>())
            .Returns(VirtualAccountCreationResult.Success("1122334455", "Acme Ltd", "035", "Wema Bank", "pstk_va_2"));

        var service = CreateService(db);

        // Act
        var result = await service.ProvisionOrganizationVirtualAccountAsync(org.Id, Currency.NGN, PaymentProvider.Paystack);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("1122334455", result.AccountNumber);
        Assert.Equal(PaymentProvider.Paystack, result.Provider);

        var persisted = await db.VirtualAccounts.FirstOrDefaultAsync(v => v.OrganizationId == org.Id);
        Assert.NotNull(persisted);

        _outbox.Received(1).Write(Arg.Any<VirtualAccountProvisionedDomainEvent>());
    }

    [Fact]
    public async Task Provision_WhenProviderFails_ThrowsInvalidOperationException()
    {
        // Arrange
        using var db = CreateDbContext();
        _flwProvider.CreateVirtualAccountAsync(Arg.Any<VirtualAccountCreationRequest>(), Arg.Any<CancellationToken>())
            .Returns(VirtualAccountCreationResult.Failure("BVN validation failed."));

        var service = CreateService(db);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProvisionIndividualVirtualAccountAsync("usr_fail", Currency.NGN, PaymentProvider.Flutterwave));

        Assert.Contains("BVN validation failed", ex.Message);
    }

    [Fact]
    public async Task GetVirtualAccountForOwnerAsync_ReturnsMatchingAccount()
    {
        // Arrange
        using var db = CreateDbContext();
        var va = VirtualAccount.CreateIndividual(
            individualId: "usr_lookup",
            provider: PaymentProvider.Flutterwave,
            accountNumber: "5544332211",
            accountName: "Lookup User",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN);
        db.VirtualAccounts.Add(va);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var found = await service.GetVirtualAccountForOwnerAsync("usr_lookup", null, Currency.NGN);
        var notFound = await service.GetVirtualAccountForOwnerAsync("usr_other", null, Currency.NGN);

        // Assert
        Assert.NotNull(found);
        Assert.Equal("5544332211", found.AccountNumber);
        Assert.Null(notFound);
    }
}
