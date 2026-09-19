using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Organizations.RegisterStep2;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Infrastructure.Persistence;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class RegisterStep2CommandTests
{
    private readonly ICurrentOrganizationContext _orgContext = Substitute.For<ICurrentOrganizationContext>();
    private readonly RegisterStep2CommandValidator _validator = new();

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void Validator_WhenOrganizationIdEmpty_ShouldFail()
    {
        var command = new RegisterStep2Command(Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.OrganizationId);
    }

    [Fact]
    public void Validator_WhenOnlyOrganizationIdProvided_ShouldPass()
    {
        var command = new RegisterStep2Command(Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Handle_StreamlinedSubmission_WhenCacAndDocsAlreadyPersisted_CompletesStep2()
    {
        await using var dbContext = CreateDbContext();
        var orgId = Guid.NewGuid();

        _orgContext.HasAccessToOrganizationAsync(orgId, Arg.Any<CancellationToken>()).Returns(true);

        var org = new Organization("Apex Tech Ltd", "info@apex.com", "+2348011223344");
        typeof(Organization).GetProperty(nameof(Organization.Id))!.SetValue(org, orgId);
        org.SetCacNumber("RC123456");
        org.SetCacCertificateUrl("https://res.cloudinary.com/cebizpay/cac.pdf");
        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync();

        var handler = new RegisterStep2CommandHandler(dbContext, _orgContext);
        var command = new RegisterStep2Command(orgId); // Omitted all redundant parameters!

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Step2Completed", result.KybStatus);
        Assert.Equal("RC123456", result.CacNumber);

        var updatedOrg = await dbContext.Organizations.FirstAsync(o => o.Id == orgId);
        Assert.Equal(KybStatus.Step2Completed, updatedOrg.KybStatus);
        Assert.Equal("https://res.cloudinary.com/cebizpay/cac.pdf", updatedOrg.CacCertificateUrl);
    }

    [Fact]
    public async Task Handle_StreamlinedSubmission_WhenCertificateMissing_ThrowsInvalidOperationException()
    {
        await using var dbContext = CreateDbContext();
        var orgId = Guid.NewGuid();

        _orgContext.HasAccessToOrganizationAsync(orgId, Arg.Any<CancellationToken>()).Returns(true);

        var org = new Organization("Apex Tech Ltd", "info@apex.com", "+2348011223344");
        typeof(Organization).GetProperty(nameof(Organization.Id))!.SetValue(org, orgId);
        org.SetCacNumber("RC123456"); // No certificate uploaded!
        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync();

        var handler = new RegisterStep2CommandHandler(dbContext, _orgContext);
        var command = new RegisterStep2Command(orgId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("CAC certificate document is required", ex.Message);
    }
}
