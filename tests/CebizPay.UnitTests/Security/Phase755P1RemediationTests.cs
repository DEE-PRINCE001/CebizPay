#pragma warning disable CS1591
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Application.Common.Interfaces.Payroll;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Compliance;
using CebizPay.Application.UseCases.Individuals.GetKycDocuments;
using CebizPay.Application.UseCases.Individuals.SubmitKyc;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Payroll.Entities;
using CebizPay.Domain.Payroll.Enums;
using CebizPay.Domain.Permissions;
using CebizPay.Infrastructure.Payments.Common;
using CebizPay.Infrastructure.Payments.Flutterwave;
using CebizPay.Infrastructure.Payments.Monnify;
using CebizPay.Infrastructure.Payments.Paystack;
using CebizPay.Infrastructure.Payroll;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Security;

/// <summary>
/// Regression test suite verifying all Phase 7.5.5 P1 remediations:
/// - P1-01: Individual KYC document BOLA/IDOR protection (GET and POST /api/v1/individuals/{id}/kyc-documents)
/// - P1-02: Compliance profile/risk/risk-history IDOR protection (GET /api/v1/compliance/profile, /risk, /risk/history, /restrictions)
/// - P1-03: Payroll cancel and retry-failed RBAC enforcement (POST /api/v1/org/payroll/{batchId}/cancel, /retry-failed)
/// - P1-04: Webhook DeadLetter permanent-stranding remediation and safe reactivation
/// </summary>
public sealed class Phase755P1RemediationTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    #region P1-01: KYC Document BOLA/IDOR Tests

    [Fact]
    public async Task GetKycDocuments_WhenCallerIsDifferentUserAndNotAdmin_ThrowsUnauthorizedAccessException()
    {
        using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("attacker-user-id");

        var doc = new KycDocument("victim-user-id", DocumentType.Nimc, "12345678901", "https://storage/doc.pdf");
        db.KycDocuments.Add(doc);
        await db.SaveChangesAsync();

        var handler = new GetKycDocumentsQueryHandler(db, currentUserService);
        var query = new GetKycDocumentsQuery("victim-user-id");

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(query, CancellationToken.None));
        Assert.Contains("not authorized", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetKycDocuments_WhenCallerIsOwner_ReturnsOwnDocuments()
    {
        using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("legit-user-id");

        var doc = new KycDocument("legit-user-id", DocumentType.Nimc, "12345678901", "https://storage/doc.pdf");
        db.KycDocuments.Add(doc);
        await db.SaveChangesAsync();

        var handler = new GetKycDocumentsQueryHandler(db, currentUserService);
        var query = new GetKycDocumentsQuery("legit-user-id");

        var result = await handler.Handle(query, CancellationToken.None);
        var docList = result.ToList();
        Assert.Single(docList);
        Assert.Equal("legit-user-id", docList[0].UserId);
    }

    [Fact]
    public async Task GetKycDocuments_WhenCallerIsAdminWithKycView_ReturnsVictimDocuments()
    {
        using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("admin-user-id");

        var admin = new AdminProfile("admin-user-id", AdminRoleType.Admin);
        admin.GrantPermission(Permissions.KycView);
        db.AdminProfiles.Add(admin);

        var doc = new KycDocument("victim-user-id", DocumentType.InternationalPassport, "A12345678", "https://storage/doc.pdf");
        db.KycDocuments.Add(doc);
        await db.SaveChangesAsync();

        var handler = new GetKycDocumentsQueryHandler(db, currentUserService);
        var query = new GetKycDocumentsQuery("victim-user-id");

        var result = await handler.Handle(query, CancellationToken.None);
        var docList = result.ToList();
        Assert.Single(docList);
        Assert.Equal("victim-user-id", docList[0].UserId);
    }

    [Fact]
    public async Task SubmitKyc_WhenCallerIsDifferentUserAndNotAdmin_ThrowsUnauthorizedAccessException()
    {
        using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("attacker-user-id");

        var handler = new SubmitKycCommandHandler(db, currentUserService);
        var command = new SubmitKycCommand("victim-user-id", DocumentType.Nimc, "11122233344", "https://storage/fake.pdf");

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("not authorized", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitKyc_WhenCallerIsOwner_SuccessfullyCreatesDocument()
    {
        using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("legit-user-id");

        var handler = new SubmitKycCommandHandler(db, currentUserService);
        var command = new SubmitKycCommand("legit-user-id", DocumentType.Nimc, "11122233344", "https://storage/valid.pdf");

        var result = await handler.Handle(command, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("legit-user-id", result.UserId);
        Assert.Equal(DocumentType.Nimc.ToString(), result.DocumentType);

        var persisted = await db.KycDocuments.FirstOrDefaultAsync(d => d.Id == result.DocumentId);
        Assert.NotNull(persisted);
        Assert.Equal("legit-user-id", persisted.UserId);
    }

    [Fact]
    public async Task SubmitKyc_WhenCallerIsAdminWithKycReview_SuccessfullySubmitsForOtherUser()
    {
        using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("admin-user-id");

        var admin = new AdminProfile("admin-user-id", AdminRoleType.Admin);
        db.AdminProfiles.Add(admin);
        await db.SaveChangesAsync();

        var handler = new SubmitKycCommandHandler(db, currentUserService);
        var command = new SubmitKycCommand("assisted-user-id", DocumentType.DriversLicense, "DL-998877", "https://storage/assisted.pdf");

        var result = await handler.Handle(command, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("assisted-user-id", result.UserId);
    }

    #endregion

    #region P1-02: Compliance Profile/Risk IDOR Tests

    [Fact]
    public async Task GetComplianceProfile_WhenUserAccessesAnotherUserProfile_ThrowsUnauthorizedAccessException()
    {
        using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("user-a");

        var cddService = Substitute.For<ICddService>();
        var decisionService = Substitute.For<IComplianceDecisionService>();
        var restrictionService = Substitute.For<IComplianceRestrictionService>();

        var handler = new GetComplianceProfileQueryHandler(cddService, decisionService, restrictionService, db, currentUserService);
        var query = new GetComplianceProfileQuery(RiskSubjectType.Individual, "user-b");

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(query, CancellationToken.None));
        Assert.Contains("not authorized", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetComplianceProfile_WhenUserAccessesOwnProfile_Succeeds()
    {
        using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("user-a");

        var cddService = Substitute.For<ICddService>();
        cddService.GetOrCreateCddProfileAsync(RiskSubjectType.Individual, "user-a", null, Arg.Any<CancellationToken>())
            .Returns(new CddProfileDto(Guid.NewGuid(), RiskSubjectType.Individual, "user-a", null, CddStatus.Completed, RiskRating.Low, CddLevel.Standard, 1, null, DateTime.UtcNow, DateTime.UtcNow, null));

        var decisionService = Substitute.For<IComplianceDecisionService>();
        var restrictionService = Substitute.For<IComplianceRestrictionService>();
        restrictionService.GetActiveRestrictionsAsync(RiskSubjectType.Individual, "user-a", Arg.Any<CancellationToken>())
            .Returns(new List<ComplianceRestrictionDto>());

        var handler = new GetComplianceProfileQueryHandler(cddService, decisionService, restrictionService, db, currentUserService);
        var query = new GetComplianceProfileQuery(RiskSubjectType.Individual, "user-a");

        var result = await handler.Handle(query, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("user-a", result.CddProfile.SubjectId);
    }

    [Fact]
    public async Task GetComplianceProfile_WhenMemberAccessesOtherOrganizationProfile_ThrowsUnauthorizedAccessException()
    {
        using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("user-a");

        var org1 = Guid.NewGuid();
        var org2 = Guid.NewGuid();

        // User belongs to org1 only
        var membership = new OrganizationMembership("user-a", org1, MembershipRoleType.Owner);
        db.OrganizationMemberships.Add(membership);
        await db.SaveChangesAsync();

        var cddService = Substitute.For<ICddService>();
        var decisionService = Substitute.For<IComplianceDecisionService>();
        var restrictionService = Substitute.For<IComplianceRestrictionService>();

        var handler = new GetComplianceProfileQueryHandler(cddService, decisionService, restrictionService, db, currentUserService);
        var query = new GetComplianceProfileQuery(RiskSubjectType.Organization, org2.ToString(), org2);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(query, CancellationToken.None));
        Assert.Contains("not authorized", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetRiskAssessment_WhenCrossUserAccess_ThrowsUnauthorizedAccessException()
    {
        using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("user-a");

        var handler = new GetRiskAssessmentQueryHandler(db, currentUserService);
        var query = new GetRiskAssessmentQuery(RiskSubjectType.Individual, "user-b");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task GetRiskHistory_WhenCrossUserAccess_ThrowsUnauthorizedAccessException()
    {
        using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("user-a");

        var handler = new GetRiskHistoryQueryHandler(db, currentUserService);
        var query = new GetRiskHistoryQuery(RiskSubjectType.Individual, "user-b");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task GetComplianceRestrictions_WhenCrossUserAccess_ThrowsUnauthorizedAccessException()
    {
        using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("user-a");

        var restrictionService = Substitute.For<IComplianceRestrictionService>();
        var handler = new GetComplianceRestrictionsQueryHandler(restrictionService, db, currentUserService);
        var query = new GetComplianceRestrictionsQuery(RiskSubjectType.Individual, "user-b");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task GetComplianceProfile_WhenAdminAccessesAnyProfile_Succeeds()
    {
        using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("admin-user");

        var admin = new AdminProfile("admin-user", AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(admin);
        await db.SaveChangesAsync();

        var cddService = Substitute.For<ICddService>();
        cddService.GetOrCreateCddProfileAsync(RiskSubjectType.Individual, "arbitrary-user", null, Arg.Any<CancellationToken>())
            .Returns(new CddProfileDto(Guid.NewGuid(), RiskSubjectType.Individual, "arbitrary-user", null, CddStatus.Completed, RiskRating.Low, CddLevel.Standard, 1, null, DateTime.UtcNow, DateTime.UtcNow, null));

        var decisionService = Substitute.For<IComplianceDecisionService>();
        var restrictionService = Substitute.For<IComplianceRestrictionService>();
        restrictionService.GetActiveRestrictionsAsync(RiskSubjectType.Individual, "arbitrary-user", Arg.Any<CancellationToken>())
            .Returns(new List<ComplianceRestrictionDto>());

        var handler = new GetComplianceProfileQueryHandler(cddService, decisionService, restrictionService, db, currentUserService);
        var query = new GetComplianceProfileQuery(RiskSubjectType.Individual, "arbitrary-user");

        var result = await handler.Handle(query, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("arbitrary-user", result.CddProfile.SubjectId);
    }

    #endregion

    #region P1-03: Payroll Cancel/Retry Authorization Bypass Tests

    [Fact]
    public async Task CancelBatch_WhenCallerLacksPayrollExecutePermission_ThrowsUnauthorizedAccessException()
    {
        using var db = CreateInMemoryDbContext();
        var orgId = Guid.NewGuid();

        var callerMembership = new OrganizationMembership("staff-user", orgId, MembershipRoleType.Member);
        db.OrganizationMemberships.Add(callerMembership);

        var batch = PayrollBatch.Create(orgId, Currency.NGN, PayrollSelectionMode.All, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, "owner-id");
        db.PayrollBatches.Add(batch);
        await db.SaveChangesAsync();

        var calcService = new PayrollCalculationService(db, new NullPayrollDeductionProvider());
        var batchService = new PayrollBatchService(db, calcService, Substitute.For<IOutboxService>(), NullLogger<PayrollBatchService>.Instance);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            batchService.CancelBatchAsync(orgId, batch.Id, "staff-user"));
        Assert.Contains("permission", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelBatch_WhenCallerBelongsToDifferentOrg_ThrowsUnauthorizedAccessException()
    {
        using var db = CreateInMemoryDbContext();
        var org1 = Guid.NewGuid();
        var org2 = Guid.NewGuid();

        var callerMembership = new OrganizationMembership("foreign-manager", org2, MembershipRoleType.PayrollManager);
        db.OrganizationMemberships.Add(callerMembership);

        var batch = PayrollBatch.Create(org1, Currency.NGN, PayrollSelectionMode.All, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, "owner-1");
        db.PayrollBatches.Add(batch);
        await db.SaveChangesAsync();

        var calcService = new PayrollCalculationService(db, new NullPayrollDeductionProvider());
        var batchService = new PayrollBatchService(db, calcService, Substitute.For<IOutboxService>(), NullLogger<PayrollBatchService>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            batchService.CancelBatchAsync(org1, batch.Id, "foreign-manager"));
    }

    [Fact]
    public async Task CancelBatch_WhenBatchNotPending_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDbContext();
        var orgId = Guid.NewGuid();

        var callerMembership = new OrganizationMembership("payroll-officer", orgId, MembershipRoleType.PayrollManager);
        db.OrganizationMemberships.Add(callerMembership);

        var batch = PayrollBatch.Create(orgId, Currency.NGN, PayrollSelectionMode.All, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, "payroll-officer");
        batch.MarkProcessing();
        db.PayrollBatches.Add(batch);
        await db.SaveChangesAsync();

        var calcService = new PayrollCalculationService(db, new NullPayrollDeductionProvider());
        var batchService = new PayrollBatchService(db, calcService, Substitute.For<IOutboxService>(), NullLogger<PayrollBatchService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            batchService.CancelBatchAsync(orgId, batch.Id, "payroll-officer"));
        Assert.Contains("Cannot cancel batch in status", ex.Message);
    }

    [Fact]
    public async Task CancelBatch_WhenAuthorizedAndBatchPending_SuccessfullyCancels()
    {
        using var db = CreateInMemoryDbContext();
        var orgId = Guid.NewGuid();

        var callerMembership = new OrganizationMembership("payroll-officer", orgId, MembershipRoleType.PayrollManager);
        db.OrganizationMemberships.Add(callerMembership);

        var batch = PayrollBatch.Create(orgId, Currency.NGN, PayrollSelectionMode.All, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, "payroll-officer");
        var item = PayrollItem.Create(batch.Id, orgId, "emp-1", "Emp One", "emp@org.com", Currency.NGN, 50000m, 0m);
        batch.AddItem(item);
        db.PayrollBatches.Add(batch);
        await db.SaveChangesAsync();

        var calcService = new PayrollCalculationService(db, new NullPayrollDeductionProvider());
        var batchService = new PayrollBatchService(db, calcService, Substitute.For<IOutboxService>(), NullLogger<PayrollBatchService>.Instance);

        await batchService.CancelBatchAsync(orgId, batch.Id, "payroll-officer");

        var updated = await db.PayrollBatches.FirstAsync(b => b.Id == batch.Id);
        Assert.Equal(PayrollBatchStatus.Cancelled, updated.Status);
    }

    [Fact]
    public async Task RetryFailedItems_WhenCallerLacksPayrollExecute_ThrowsUnauthorizedAccessException()
    {
        using var db = CreateInMemoryDbContext();
        var orgId = Guid.NewGuid();

        var callerMembership = new OrganizationMembership("member-user", orgId, MembershipRoleType.Member);
        db.OrganizationMemberships.Add(callerMembership);

        var batch = PayrollBatch.Create(orgId, Currency.NGN, PayrollSelectionMode.All, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, "owner-id");
        db.PayrollBatches.Add(batch);
        await db.SaveChangesAsync();

        var calcService = new PayrollCalculationService(db, new NullPayrollDeductionProvider());
        var batchService = new PayrollBatchService(db, calcService, Substitute.For<IOutboxService>(), NullLogger<PayrollBatchService>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            batchService.RetryFailedItemsAsync(orgId, batch.Id, "member-user"));
    }

    [Fact]
    public async Task RetryFailedItems_WhenCallerBelongsToDifferentOrg_ThrowsUnauthorizedAccessException()
    {
        using var db = CreateInMemoryDbContext();
        var org1 = Guid.NewGuid();
        var org2 = Guid.NewGuid();

        var callerMembership = new OrganizationMembership("manager-2", org2, MembershipRoleType.PayrollManager);
        db.OrganizationMemberships.Add(callerMembership);

        var batch = PayrollBatch.Create(org1, Currency.NGN, PayrollSelectionMode.All, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, "owner-1");
        db.PayrollBatches.Add(batch);
        await db.SaveChangesAsync();

        var calcService = new PayrollCalculationService(db, new NullPayrollDeductionProvider());
        var batchService = new PayrollBatchService(db, calcService, Substitute.For<IOutboxService>(), NullLogger<PayrollBatchService>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            batchService.RetryFailedItemsAsync(org1, batch.Id, "manager-2"));
    }

    [Fact]
    public async Task RetryFailedItems_WhenBatchIsCancelled_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDbContext();
        var orgId = Guid.NewGuid();

        var callerMembership = new OrganizationMembership("manager-1", orgId, MembershipRoleType.PayrollManager);
        db.OrganizationMemberships.Add(callerMembership);

        var batch = PayrollBatch.Create(orgId, Currency.NGN, PayrollSelectionMode.All, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, "manager-1");
        batch.Cancel();
        db.PayrollBatches.Add(batch);
        await db.SaveChangesAsync();

        var calcService = new PayrollCalculationService(db, new NullPayrollDeductionProvider());
        var batchService = new PayrollBatchService(db, calcService, Substitute.For<IOutboxService>(), NullLogger<PayrollBatchService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            batchService.RetryFailedItemsAsync(orgId, batch.Id, "manager-1"));
        Assert.Contains("Cannot retry failed items for batch in status 'Cancelled'", ex.Message);
    }

    #endregion

    #region P1-04: Webhook DeadLetter Stranding & Reactivation Tests

    [Fact]
    public void WebhookEvent_ReactivateForRetry_ResetsStatusAttemptCountAndUnlocks()
    {
        var evt = WebhookEvent.Create(
            PaymentProvider.Paystack,
            "evt_deadletter_001",
            "charge.success",
            "hash_123",
            "{}",
            "ref_001");

        for (int i = 0; i < 5; i++)
        {
            evt.Claim("worker-test", TimeSpan.FromMinutes(1));
            evt.ReleaseClaim($"Failure {i + 1}", TimeSpan.FromSeconds(10));
        }

        Assert.Equal(WebhookEventStatus.DeadLetter, evt.Status);
        Assert.Equal(5, evt.AttemptCount);

        evt.ReactivateForRetry("Provider redelivery received", TimeSpan.Zero);

        Assert.Equal(WebhookEventStatus.Received, evt.Status);
        Assert.Equal(0, evt.AttemptCount);
        Assert.Null(evt.LockedBy);
        Assert.Null(evt.LockedUntilUtc);
        Assert.True(evt.NextRetryAtUtc <= DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void ComplianceWebhookEvent_ReactivateForRetry_ResetsStatusAttemptCountAndUnlocks()
    {
        var evt = ComplianceWebhookEvent.Create(
            VerificationProvider.SmileId,
            "cmp_evt_001",
            "document_verification",
            "hash_cmp",
            "{}",
            "corr_cmp");

        for (int i = 0; i < 5; i++)
        {
            evt.Claim("worker-cmp", TimeSpan.FromMinutes(1));
            evt.ReleaseClaim($"Cmp failure {i + 1}", TimeSpan.FromSeconds(10));
        }

        Assert.Equal(ComplianceWebhookEventStatus.DeadLetter, evt.Status);
        Assert.Equal(5, evt.AttemptCount);

        evt.ReactivateForRetry("Compliance provider retry received", TimeSpan.Zero);

        Assert.Equal(ComplianceWebhookEventStatus.Received, evt.Status);
        Assert.Equal(0, evt.AttemptCount);
        Assert.Null(evt.LockedBy);
        Assert.Null(evt.LockedUntilUtc);
        Assert.True(evt.NextRetryAtUtc <= DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task WebhookProcessor_WhenEventWasDeadLetter_RedeliveryReactivatesAndClearsDeadLetter()
    {
        using var db = CreateInMemoryDbContext();
        var signatureVerifier = Substitute.For<IWebhookSignatureVerifier>();
        signatureVerifier.VerifySignature(Arg.Any<PaymentProvider>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>())
            .Returns(true);

        var pstkOptions = Options.Create(new PaystackOptions { WebhookSecret = "secret", SecretKey = "secret" });
        var flwOptions = Options.Create(new FlutterwaveOptions { WebhookSecretHash = "secret", SecretKey = "secret" });
        var monnifyOptions = Options.Create(new MonnifyOptions { WebhookSecret = "secret", SecretKey = "secret" });

        var processor = new WebhookProcessor(
            signatureVerifier,
            db,
            Substitute.For<ILedgerPostingService>(),
            Substitute.For<IPlatformFeePolicyService>(),
            Substitute.For<IOutboxService>(),
            flwOptions,
            pstkOptions,
            monnifyOptions,
            NullLogger<WebhookProcessor>.Instance);

        const string expectedProviderEventId = "flw_evt_9988_SUCCESSFUL";
        var matchingDeadLetter = WebhookEvent.Create(PaymentProvider.Flutterwave, expectedProviderEventId, "transfer.completed", "hash", "{}", "REF123");
        for (int i = 0; i < 5; i++)
        {
            matchingDeadLetter.Claim("worker-1", TimeSpan.FromMinutes(1));
            matchingDeadLetter.ReleaseClaim($"Fail {i}", TimeSpan.FromMinutes(1));
        }
        db.WebhookEvents.Add(matchingDeadLetter);
        await db.SaveChangesAsync();

        Assert.Equal(WebhookEventStatus.DeadLetter, matchingDeadLetter.Status);

        const string rawPayload = """{"event":"transfer.completed","data":{"id":9988,"status":"SUCCESSFUL","reference":"REF123","amount":5000,"currency":"NGN"}}""";

        // Act: Ingestion of redelivered webhook for flw_evt_9988_SUCCESSFUL
        var result = await processor.IngestWebhookAsync(
            PaymentProvider.Flutterwave,
            rawPayload,
            new Dictionary<string, string>(),
            CancellationToken.None);

        Assert.Equal(WebhookProcessingStatus.Processed, result.Status);

        var reactivated = await db.WebhookEvents.FirstAsync(e => e.ProviderEventId == expectedProviderEventId);
        Assert.Equal(WebhookEventStatus.Received, reactivated.Status);
        Assert.Equal(0, reactivated.AttemptCount);
        Assert.Null(reactivated.LockedBy);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.Action == AuditActions.WebhookReactivated);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task WebhookProcessor_WhenEventAlreadyProcessed_DuplicateIsReportedWithoutReactivation()
    {
        using var db = CreateInMemoryDbContext();
        var signatureVerifier = Substitute.For<IWebhookSignatureVerifier>();
        signatureVerifier.VerifySignature(Arg.Any<PaymentProvider>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>())
            .Returns(true);

        var pstkOptions = Options.Create(new PaystackOptions { WebhookSecret = "secret", SecretKey = "secret" });
        var flwOptions = Options.Create(new FlutterwaveOptions { WebhookSecretHash = "secret", SecretKey = "secret" });
        var monnifyOptions = Options.Create(new MonnifyOptions { WebhookSecret = "secret", SecretKey = "secret" });

        var processor = new WebhookProcessor(
            signatureVerifier,
            db,
            Substitute.For<ILedgerPostingService>(),
            Substitute.For<IPlatformFeePolicyService>(),
            Substitute.For<IOutboxService>(),
            flwOptions,
            pstkOptions,
            monnifyOptions,
            NullLogger<WebhookProcessor>.Instance);

        const string expectedProviderEventId = "flw_evt_8888_SUCCESSFUL";
        var existingProcessed = WebhookEvent.Create(PaymentProvider.Flutterwave, expectedProviderEventId, "transfer.completed", "hash", "{}", "REF888");
        existingProcessed.Claim("worker-1", TimeSpan.FromMinutes(1));
        existingProcessed.MarkProcessed(Guid.NewGuid(), "{}", "REF888");
        db.WebhookEvents.Add(existingProcessed);
        await db.SaveChangesAsync();

        const string rawPayload = """{"event":"transfer.completed","data":{"id":8888,"status":"SUCCESSFUL","reference":"REF888","amount":5000,"currency":"NGN"}}""";

        var result = await processor.IngestWebhookAsync(
            PaymentProvider.Flutterwave,
            rawPayload,
            new Dictionary<string, string>(),
            CancellationToken.None);

        Assert.Equal(WebhookProcessingStatus.Duplicate, result.Status);
        var evtAfter = await db.WebhookEvents.FirstAsync(e => e.ProviderEventId == expectedProviderEventId);
        Assert.Equal(WebhookEventStatus.Processed, evtAfter.Status);
    }

    [Fact]
    public async Task WebhookProcessingService_WhenDirectRetryOnDeadLetter_ReactivatesAndProcessesSuccessfully()
    {
        using var db = CreateInMemoryDbContext();
        var evt = WebhookEvent.Create(PaymentProvider.Paystack, "evt_dl_retry", "charge.success", "hash", "{}", "REF_RETRY");
        for (int i = 0; i < 5; i++)
        {
            evt.Claim("worker-1", TimeSpan.FromMinutes(1));
            evt.ReleaseClaim($"Error {i}", TimeSpan.FromMinutes(1));
        }
        db.WebhookEvents.Add(evt);
        await db.SaveChangesAsync();

        Assert.Equal(WebhookEventStatus.DeadLetter, evt.Status);

        var financialProcessor = Substitute.For<IWebhookProcessor>();
        financialProcessor.ProcessFinancialWebhookEventAsync(evt.Id, Arg.Any<CancellationToken>())
            .Returns(WebhookProcessingResult.Processed(evt.ProviderEventId, Guid.NewGuid()));

        var service = new WebhookProcessingService(
            db,
            financialProcessor,
            Substitute.For<IComplianceWebhookProcessor>(),
            new ReconciliationMetrics(),
            NullLogger<WebhookProcessingService>.Instance);

        var result = await service.ProcessSingleFinancialWebhookAsync(evt.Id, CancellationToken.None);

        Assert.Equal(WebhookProcessingStatus.Processed, result.Status);
        var updated = await db.WebhookEvents.FirstAsync(e => e.Id == evt.Id);
        Assert.Equal(WebhookEventStatus.Processed, updated.Status);
    }

    #endregion
}
