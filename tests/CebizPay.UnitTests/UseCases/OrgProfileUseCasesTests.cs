using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Organizations.Profile;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.UseCases;

public sealed class OrgProfileUseCasesTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
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
        var validator = new GetOrgProfileQueryValidator();
        var query = new GetOrgProfileQuery(Guid.Empty);

        var result = validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(query.OrganizationId));
    }

    [Fact]
    public async Task Handle_WhenAuthorizedAndKybStep2Exists_ReturnsProfileWithKybCert()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var org = new Organization("Cebis Tech", "info@cebistech.com", "+2348011223344", "Technology", "Lagos, Nigeria");
        org.CompleteStep2("RC123456", "https://example.com/logo.png", "https://example.com/cac_root.pdf");
        dbContext.Organizations.Add(org);

        var kybStep2 = new KybDetail(
            org.Id,
            2,
            org.CompanyName,
            org.Email,
            org.Phone,
            "RC123456",
            "https://example.com/logo2.png",
            "https://example.com/kyb_latest.pdf");
        dbContext.KybDetails.Add(kybStep2);

        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new GetOrgProfileQueryHandler(dbContext, orgContext);
        var result = await handler.Handle(new GetOrgProfileQuery(org.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(org.Id, result.OrganizationId);
        Assert.Equal("Cebis Tech", result.Name);
        Assert.Equal("info@cebistech.com", result.Email);
        Assert.Equal("+2348011223344", result.PhoneNumber);
        Assert.Equal("Lagos, Nigeria", result.Address);
        Assert.Equal("Technology", result.Category);
        Assert.Equal("Pending", result.Status);
        Assert.Equal("https://example.com/kyb_latest.pdf", result.CacCertificateUrl);
        Assert.Equal("RC123456", result.CacNumber);
        Assert.Equal("https://example.com/logo.png", result.LogoUrl);
    }

    [Fact]
    public async Task Handle_WhenNoKybDetails_FallsBackToOrganizationRootCertificate()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var org = new Organization("Fallback Org", "contact@fallback.com", "+2348099887766");
        org.CompleteStep2("RC987654", "https://example.com/org_logo.png", "https://example.com/org_cac.pdf");
        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new GetOrgProfileQueryHandler(dbContext, orgContext);
        var result = await handler.Handle(new GetOrgProfileQuery(org.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("https://example.com/org_cac.pdf", result.CacCertificateUrl);
        Assert.Equal("RC987654", result.CacNumber);
        Assert.Equal("https://example.com/org_logo.png", result.LogoUrl);
    }

    [Fact]
    public async Task Handle_WhenUnauthorized_ThrowsUnauthorizedAccessException()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var orgId = Guid.NewGuid();

        orgContext.HasAccessToOrganizationAsync(orgId, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new GetOrgProfileQueryHandler(dbContext, orgContext);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new GetOrgProfileQuery(orgId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenOrganizationNotFound_ReturnsNull()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var orgId = Guid.NewGuid();

        orgContext.HasAccessToOrganizationAsync(orgId, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new GetOrgProfileQueryHandler(dbContext, orgContext);
        var result = await handler.Handle(new GetOrgProfileQuery(orgId), CancellationToken.None);

        Assert.Null(result);
    }
}
