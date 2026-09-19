using System.Text;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Interfaces.Storage;
using CebizPay.Application.UseCases.Organizations.Kyb;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Infrastructure.Persistence;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class UploadKybDocumentCommandTests
{
    private readonly ICloudinaryStorageService _storageService = Substitute.For<ICloudinaryStorageService>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly UploadKybDocumentCommandValidator _validator = new();

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void Validator_WhenFileTooLarge_ShouldFail()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var command = new UploadKybDocumentCommand(
            OrganizationId: Guid.NewGuid(),
            DocumentType: "CacCertificate",
            FileStream: stream,
            FileName: "doc.pdf",
            ContentType: "application/pdf",
            FileSizeBytes: 11 * 1024 * 1024); // 11MB > 10MB limit

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.FileSizeBytes);
    }

    [Fact]
    public void Validator_WhenValid_ShouldPass()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var command = new UploadKybDocumentCommand(
            OrganizationId: Guid.NewGuid(),
            DocumentType: "CacCertificate",
            FileStream: stream,
            FileName: "cac_cert.pdf",
            ContentType: "application/pdf",
            FileSizeBytes: 1024);

        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Handle_WhenCallerNotAuthenticated_ThrowsUnauthorizedAccessException()
    {
        await using var dbContext = CreateDbContext();
        _currentUserService.UserId.Returns((string?)null);

        using var stream = new MemoryStream([1, 2, 3]);
        var handler = new UploadKybDocumentCommandHandler(_storageService, dbContext, _currentUserService);
        var command = new UploadKybDocumentCommand(Guid.NewGuid(), "CacCertificate", stream, "doc.pdf", "application/pdf", 3);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenCallerNotMember_ThrowsUnauthorizedAccessException()
    {
        await using var dbContext = CreateDbContext();
        _currentUserService.UserId.Returns("outsider_user");

        using var stream = new MemoryStream([1, 2, 3]);
        var handler = new UploadKybDocumentCommandHandler(_storageService, dbContext, _currentUserService);
        var command = new UploadKybDocumentCommand(Guid.NewGuid(), "CacCertificate", stream, "doc.pdf", "application/pdf", 3);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenCloudinaryUploadFails_ThrowsInvalidOperationException()
    {
        await using var dbContext = CreateDbContext();
        var orgId = Guid.NewGuid();
        var userId = "org_admin_user";
        _currentUserService.UserId.Returns(userId);

        var membership = new OrganizationMembership(userId, orgId, MembershipRoleType.Admin);
        dbContext.OrganizationMemberships.Add(membership);

        var org = new Organization("TechCorp Ltd", "contact@techcorp.com", "+2348011223344");
        typeof(Organization).GetProperty(nameof(Organization.Id))!.SetValue(org, orgId);
        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync();

        _storageService.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CloudinaryUploadResult.Failure("Corrupt or invalid PDF file header."));

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var handler = new UploadKybDocumentCommandHandler(_storageService, dbContext, _currentUserService);
        var command = new UploadKybDocumentCommand(orgId, "CacCertificate", stream, "cac.pdf", "application/pdf", 4);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("Corrupt or invalid PDF file header", ex.Message);
    }

    [Fact]
    public async Task Handle_WhenUploadSucceeds_UpdatesOrganizationCacCertificateUrl()
    {
        await using var dbContext = CreateDbContext();
        var orgId = Guid.NewGuid();
        var userId = "org_admin_user";
        _currentUserService.UserId.Returns(userId);

        var membership = new OrganizationMembership(userId, orgId, MembershipRoleType.Admin);
        dbContext.OrganizationMemberships.Add(membership);

        var org = new Organization("TechCorp Ltd", "contact@techcorp.com", "+2348011223344");
        typeof(Organization).GetProperty(nameof(Organization.Id))!.SetValue(org, orgId);
        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync();

        const string expectedUrl = "https://res.cloudinary.com/cebizpay/raw/upload/v1/cebizpay/kyb/test/cac.pdf";
        const string expectedPublicId = "cebizpay/kyb/test/cac";

        _storageService.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CloudinaryUploadResult.Success(expectedUrl, expectedPublicId, "pdf", 1024));

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var handler = new UploadKybDocumentCommandHandler(_storageService, dbContext, _currentUserService);
        var command = new UploadKybDocumentCommand(orgId, "CacCertificate", stream, "cac.pdf", "application/pdf", 1024);

        var response = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(expectedUrl, response.FileUrl);
        Assert.Equal(expectedPublicId, response.PublicId);
        Assert.Equal("CacCertificate", response.DocumentType);

        var updatedOrg = await dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == orgId);
        Assert.NotNull(updatedOrg);
        Assert.Equal(expectedUrl, updatedOrg.CacCertificateUrl);
    }
}
