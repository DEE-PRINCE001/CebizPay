using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Organizations.Kyb;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Infrastructure.Persistence;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class LookupCacCommandTests
{
    private readonly IVerificationOrchestrator _orchestrator = Substitute.For<IVerificationOrchestrator>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly LookupCacCommandValidator _validator = new();

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
        var command = new LookupCacCommand(Guid.Empty, "RC123456");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.OrganizationId);
    }

    [Fact]
    public void Validator_WhenCacNumberEmpty_ShouldFail()
    {
        var command = new LookupCacCommand(Guid.NewGuid(), string.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.CacNumber);
    }

    [Fact]
    public void Validator_WhenValid_ShouldPass()
    {
        var command = new LookupCacCommand(Guid.NewGuid(), "RC123456", "Acme Ltd");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ThrowsUnauthorizedAccessException()
    {
        await using var dbContext = CreateDbContext();
        _currentUserService.UserId.Returns((string?)null);

        var handler = new LookupCacCommandHandler(_orchestrator, dbContext, _currentUserService);
        var command = new LookupCacCommand(Guid.NewGuid(), "RC123456");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenUserNotMember_ThrowsUnauthorizedAccessException()
    {
        await using var dbContext = CreateDbContext();
        var orgId = Guid.NewGuid();
        _currentUserService.UserId.Returns("user_outsider");

        var handler = new LookupCacCommandHandler(_orchestrator, dbContext, _currentUserService);
        var command = new LookupCacCommand(orgId, "RC123456");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenUserIsVerifiedSignatoryDirector_ReturnsCacDetailsWithSignatoryFlagTrue()
    {
        await using var dbContext = CreateDbContext();
        var orgId = Guid.NewGuid();
        var userId = "user_director_01";
        _currentUserService.UserId.Returns(userId);

        var membership = new OrganizationMembership(userId, orgId, MembershipRoleType.Owner);
        dbContext.OrganizationMemberships.Add(membership);

        var profile = new IndividualProfile(userId, "Emeka", "Okonkwo");
        profile.SetKycStatus(KycStatus.Verified);
        dbContext.IndividualProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        var safeMeta = """
        {
            "rc_number": "RC123456",
            "company_name": "EMEKA & SONS VENTURES LTD",
            "company_type": "PRIVATE_LIMITED_COMPANY",
            "registration_date": "2021-04-12",
            "status": "ACTIVE",
            "address": "Plot 100 Victoria Island, Lagos",
            "directors": [
                { "name": "EMEKA OKONKWO", "designation": "Managing Director" },
                { "name": "ADAOBI OKONKWO", "designation": "Director" }
            ]
        }
        """;

        var evidences = new List<VerificationEvidenceSummaryDto>
        {
            new(
                EvidenceId: Guid.NewGuid(),
                Capability: VerificationCapability.Business,
                Provider: VerificationProvider.Dojah,
                ResultStatus: VerificationResultStatus.Match,
                ConfidenceScore: 100m,
                VerifiedAtUtc: DateTime.UtcNow,
                ExpiresAtUtc: null,
                FailureCode: null,
                FailureReason: null,
                SafeMetadata: safeMeta)
        };

        var verificationResponse = new VerificationOperationResponse(
            OperationId: Guid.NewGuid(),
            Reference: "CBZKYB-REF-999",
            VerificationType: VerificationType.OrganizationKyb,
            Capability: VerificationCapability.Business,
            Status: VerificationStatus.Completed,
            PrimaryProvider: VerificationProvider.Dojah,
            ActiveProvider: VerificationProvider.Dojah,
            UsedFallback: false,
            LatestResultStatus: VerificationResultStatus.Match,
            MatchScore: 100m,
            Summary: "Corporate CAC registry verified.",
            FailureReason: null,
            CreatedAtUtc: DateTime.UtcNow,
            CompletedAtUtc: DateTime.UtcNow,
            Evidences: evidences);

        _orchestrator.VerifyBusinessAsync(orgId, "RC123456", Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(verificationResponse);

        var handler = new LookupCacCommandHandler(_orchestrator, dbContext, _currentUserService);
        var command = new LookupCacCommand(orgId, "RC123456");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsMatched);
        Assert.Equal("RC123456", result.CacNumber);
        Assert.Equal("EMEKA & SONS VENTURES LTD", result.CompanyName);
        Assert.Equal("ACTIVE", result.Status);
        Assert.Equal(2, result.Directors.Count);

        var emekaDirector = Assert.Single(result.Directors, d => d.Name == "EMEKA OKONKWO");
        Assert.True(emekaDirector.IsKycVerifiedSignatory);

        var adaobiDirector = Assert.Single(result.Directors, d => d.Name == "ADAOBI OKONKWO");
        Assert.False(adaobiDirector.IsKycVerifiedSignatory);
    }
}
