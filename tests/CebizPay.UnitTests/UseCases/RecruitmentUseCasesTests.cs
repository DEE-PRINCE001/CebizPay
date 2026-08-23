using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Organizations.Recruitment;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.UseCases;

public sealed class RecruitmentUseCasesTests
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
    public async Task CreateJobPosting_WhenValid_PersistsAndWritesOutboxAndAudit()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TechCorp", "hr@techcorp.com", "+2348000000002");
        dbContext.Organizations.Add(org);

        var dept = new Department(org.Id, "Engineering");
        dbContext.Departments.Add(dept);

        var role = new WorkforceRole(org.Id, "Software Engineer", dept.Id);
        dbContext.WorkforceRoles.Add(role);

        var salary = new SalaryLevel(org.Id, "Level 1", 500000m, "NGN");
        dbContext.SalaryLevels.Add(salary);

        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("usr_recruiter");

        var handler = new CreateJobPostingCommandHandler(dbContext, orgContext, userContext, outbox);
        var command = new CreateJobPostingCommand(
            org.Id,
            "Backend Lead",
            "Lead engineering team",
            EmploymentType.FullTime,
            dept.Id,
            role.Id,
            salary.Id,
            "Lagos, NG",
            ".NET, Cloud",
            "Architecture",
            DateTime.UtcNow.AddDays(30));

        var jobId = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, jobId);
        var job = await dbContext.JobPostings.FindAsync(jobId);
        Assert.NotNull(job);
        Assert.Equal("Backend Lead", job.Title);
        Assert.Equal(JobPostingStatus.Draft, job.Status);

        outbox.Received(1).Write(Arg.Any<JobPostingCreatedDomainEvent>());
        var audit = await dbContext.AuditLogs.FirstOrDefaultAsync(a => a.ResourceId == jobId.ToString());
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task CreateJobPosting_WhenOrganizationSuspended_ThrowsInvalidOperationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("SuspendedCorp", "hr@suspended.com", "+2348000000003");
        org.TransitionStatus(OrganizationStatus.Verified);
        org.TransitionStatus(OrganizationStatus.Suspended);
        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);

        var handler = new CreateJobPostingCommandHandler(dbContext, orgContext, userContext, outbox);
        var command = new CreateJobPostingCommand(org.Id, "Backend Lead", "Desc");

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateJobPosting_WhenValid_UpdatesAndLogsAudit()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TechCorp", "hr@techcorp.com", "+2348000000002");
        dbContext.Organizations.Add(org);
        var job = new JobPosting(org.Id, "Old Title", "Old Desc", "usr_1");
        dbContext.JobPostings.Add(job);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("usr_recruiter");

        var handler = new UpdateJobPostingCommandHandler(dbContext, orgContext, userContext, outbox);
        var command = new UpdateJobPostingCommand(
            job.Id,
            org.Id,
            "New Title",
            "New Desc",
            EmploymentType.Remote,
            Location: "Remote");

        await handler.Handle(command, CancellationToken.None);

        var updated = await dbContext.JobPostings.FindAsync(job.Id);
        Assert.NotNull(updated);
        Assert.Equal("New Title", updated.Title);
        Assert.Equal(EmploymentType.Remote, updated.EmploymentType);

        outbox.Received(1).Write(Arg.Any<JobPostingUpdatedDomainEvent>());
    }

    [Fact]
    public async Task PublishAndCloseJobPosting_Lifecycle_Succeeds()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TechCorp", "hr@techcorp.com", "+2348000000002");
        dbContext.Organizations.Add(org);
        var job = new JobPosting(org.Id, "Frontend Engineer", "React dev", "usr_1");
        dbContext.JobPostings.Add(job);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("usr_recruiter");

        // Publish
        var publishHandler = new PublishJobPostingCommandHandler(dbContext, orgContext, userContext, outbox);
        await publishHandler.Handle(new PublishJobPostingCommand(job.Id, org.Id), CancellationToken.None);

        var published = await dbContext.JobPostings.FindAsync(job.Id);
        Assert.NotNull(published);
        Assert.Equal(JobPostingStatus.Published, published.Status);
        Assert.NotNull(published.PublishedAtUtc);

        // Close
        var closeHandler = new CloseJobPostingCommandHandler(dbContext, orgContext, userContext, outbox);
        await closeHandler.Handle(new CloseJobPostingCommand(job.Id, org.Id), CancellationToken.None);

        var closed = await dbContext.JobPostings.FindAsync(job.Id);
        Assert.NotNull(closed);
        Assert.Equal(JobPostingStatus.Closed, closed.Status);
        Assert.NotNull(closed.ClosedAtUtc);
    }

    [Fact]
    public async Task SubmitApplication_WhenJobAccepting_CreatesApplicationAndEmitsOutbox()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TechCorp", "hr@techcorp.com", "+2348000000002");
        dbContext.Organizations.Add(org);
        var job = new JobPosting(org.Id, "Backend Engineer", "C# APIs", "usr_1");
        job.Publish(DateTime.UtcNow);
        dbContext.JobPostings.Add(job);
        await dbContext.SaveChangesAsync();

        userContext.UserId.Returns("usr_candidate");

        var handler = new SubmitApplicationCommandHandler(dbContext, userContext, outbox);
        var command = new SubmitApplicationCommand(
            job.Id,
            "John Doe",
            "john@example.com",
            "+2348012345678",
            "https://resume.url",
            "Cover letter text");

        var appId = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, appId);
        var app = await dbContext.RecruitmentApplications.FindAsync(appId);
        Assert.NotNull(app);
        Assert.Equal(ApplicationStatus.Submitted, app.Status);
        Assert.Equal("john@example.com", app.ApplicantEmail);

        outbox.Received(1).Write(Arg.Any<RecruitmentApplicationSubmittedDomainEvent>());
    }

    [Fact]
    public async Task SubmitApplication_DuplicateActiveApplication_ThrowsInvalidOperationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TechCorp", "hr@techcorp.com", "+2348000000002");
        dbContext.Organizations.Add(org);
        var job = new JobPosting(org.Id, "Backend Engineer", "C# APIs", "usr_1");
        job.Publish(DateTime.UtcNow);
        dbContext.JobPostings.Add(job);

        var existingApp = new RecruitmentApplication(job.Id, org.Id, "John Doe", "john@example.com", "+2348012345678", "usr_candidate");
        dbContext.RecruitmentApplications.Add(existingApp);
        await dbContext.SaveChangesAsync();

        userContext.UserId.Returns("usr_candidate");

        var handler = new SubmitApplicationCommandHandler(dbContext, userContext, outbox);
        var command = new SubmitApplicationCommand(job.Id, "John Doe", "john@example.com", "+2348012345678");

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task ApplicationReviewWorkflow_UnderReview_Shortlist_Accept()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TechCorp", "hr@techcorp.com", "+2348000000002");
        dbContext.Organizations.Add(org);
        var job = new JobPosting(org.Id, "Backend Engineer", "C# APIs", "usr_1");
        job.Publish(DateTime.UtcNow);
        dbContext.JobPostings.Add(job);
        var app = new RecruitmentApplication(job.Id, org.Id, "Jane Doe", "jane@example.com", "+2348099887766");
        dbContext.RecruitmentApplications.Add(app);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("usr_recruiter");

        // Review
        var reviewHandler = new ReviewApplicationCommandHandler(dbContext, orgContext, userContext, outbox);
        await reviewHandler.Handle(new ReviewApplicationCommand(app.Id, org.Id, "Reviewing CV"), CancellationToken.None);

        var reviewedApp = await dbContext.RecruitmentApplications.FindAsync(app.Id);
        Assert.NotNull(reviewedApp);
        Assert.Equal(ApplicationStatus.UnderReview, reviewedApp.Status);

        // Shortlist
        var shortlistHandler = new ShortlistApplicationCommandHandler(dbContext, orgContext, userContext, outbox);
        await shortlistHandler.Handle(new ShortlistApplicationCommand(app.Id, org.Id, "Passed technical interview"), CancellationToken.None);

        var shortlistedApp = await dbContext.RecruitmentApplications.FindAsync(app.Id);
        Assert.NotNull(shortlistedApp);
        Assert.Equal(ApplicationStatus.Shortlisted, shortlistedApp.Status);

        // Accept
        var acceptHandler = new AcceptApplicationCommandHandler(dbContext, orgContext, userContext, outbox);
        await acceptHandler.Handle(new AcceptApplicationCommand(app.Id, org.Id, "Offer extended"), CancellationToken.None);

        var acceptedApp = await dbContext.RecruitmentApplications.FindAsync(app.Id);
        Assert.NotNull(acceptedApp);
        Assert.Equal(ApplicationStatus.Accepted, acceptedApp.Status);
    }

    [Fact]
    public async Task WithdrawApplication_CandidateWithdraws_Succeeds()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TechCorp", "hr@techcorp.com", "+2348000000002");
        dbContext.Organizations.Add(org);
        var job = new JobPosting(org.Id, "Product Manager", "Lead products", "usr_1");
        job.Publish(DateTime.UtcNow);
        dbContext.JobPostings.Add(job);
        var app = new RecruitmentApplication(job.Id, org.Id, "Alice", "alice@example.com", "+2348011223344", "usr_alice");
        dbContext.RecruitmentApplications.Add(app);
        await dbContext.SaveChangesAsync();

        userContext.UserId.Returns("usr_alice");

        var handler = new WithdrawApplicationCommandHandler(dbContext, userContext, outbox);
        await handler.Handle(new WithdrawApplicationCommand(app.Id), CancellationToken.None);

        var withdrawn = await dbContext.RecruitmentApplications.FindAsync(app.Id);
        Assert.NotNull(withdrawn);
        Assert.Equal(ApplicationStatus.Withdrawn, withdrawn.Status);
        outbox.Received(1).Write(Arg.Any<RecruitmentApplicationWithdrawnDomainEvent>());
    }
}
