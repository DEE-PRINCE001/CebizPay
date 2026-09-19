using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Organizations.ReviewKyb;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class ReviewKybCommandHandlerTests
{
    private readonly IEventPublisher _eventPublisher = Substitute.For<IEventPublisher>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IWalletService _walletService = Substitute.For<IWalletService>();
    private readonly IVirtualAccountService _virtualAccountService = Substitute.For<IVirtualAccountService>();

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_WhenKybVerified_ProvisionsMonnifyVirtualAccountAndInitializesWallet()
    {
        await using var dbContext = CreateDbContext();
        var adminId = "admin_super_01";
        var orgId = Guid.NewGuid();

        _currentUserService.UserId.Returns(adminId);

        var adminProfile = new AdminProfile(adminId, AdminRoleType.SuperAdmin);
        dbContext.AdminProfiles.Add(adminProfile);

        var org = new Organization("Apex Logistics Ltd", "ops@apex.com", "+2348099887766");
        typeof(Organization).GetProperty(nameof(Organization.Id))!.SetValue(org, orgId);
        org.CompleteStep2("RC987654", "https://apex.com/logo.png", "https://apex.com/cac.pdf");
        dbContext.Organizations.Add(org);

        var kybDetail = new KybDetail(orgId, 2, org.CompanyName, org.Email, org.Phone, "RC987654", "https://apex.com/logo.png", "https://apex.com/cac.pdf");
        dbContext.KybDetails.Add(kybDetail);
        await dbContext.SaveChangesAsync();

        var handler = new ReviewKybCommandHandler(
            dbContext,
            _eventPublisher,
            _currentUserService,
            _walletService,
            _virtualAccountService);

        var command = new ReviewKybCommand(orgId, KybStatus.Verified, adminId);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Verified", result.KybStatus);
        Assert.Equal("Verified", result.OrganizationStatus);

        // Verify organization wallet was created/retrieved
        await _walletService.Received(1).GetOrCreateOrganizationWalletAsync(orgId, Currency.NGN, Arg.Any<CancellationToken>());

        // Verify corporate virtual account on Monnify was provisioned
        await _virtualAccountService.Received(1).ProvisionOrganizationVirtualAccountAsync(
            orgId,
            Currency.NGN,
            PaymentProvider.Monnify,
            Arg.Any<CancellationToken>());
    }
}
