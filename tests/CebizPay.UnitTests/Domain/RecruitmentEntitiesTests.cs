using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using Xunit;

namespace CebizPay.UnitTests.Domain;

public sealed class RecruitmentEntitiesTests
{
    [Fact]
    public void JobPosting_Creation_ShouldInitializeValidState()
    {
        var orgId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var levelId = Guid.NewGuid();
        var deadline = DateTime.UtcNow.AddDays(14);

        var job = new JobPosting(
            orgId,
            "Senior Backend Engineer",
            "Build robust financial systems",
            "usr_admin",
            EmploymentType.FullTime,
            deptId,
            roleId,
            levelId,
            "Lagos, Nigeria",
            "C#, PostgreSQL, DDD",
            "Architecture & APIs",
            deadline);

        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.Equal(orgId, job.OrganizationId);
        Assert.Equal("Senior Backend Engineer", job.Title);
        Assert.Equal("Build robust financial systems", job.Description);
        Assert.Equal(JobPostingStatus.Draft, job.Status);
        Assert.Equal("usr_admin", job.CreatedByUserId);
        Assert.Equal(EmploymentType.FullTime, job.EmploymentType);
        Assert.Equal(deptId, job.DepartmentId);
        Assert.Equal(roleId, job.WorkforceRoleId);
        Assert.Equal(levelId, job.SalaryLevelId);
        Assert.Equal("Lagos, Nigeria", job.Location);
        Assert.Equal("C#, PostgreSQL, DDD", job.Requirements);
        Assert.Equal("Architecture & APIs", job.Responsibilities);
        Assert.Equal(deadline, job.ApplicationDeadline);
        Assert.Null(job.PublishedAtUtc);
        Assert.Null(job.ClosedAtUtc);
    }

    [Theory]
    [InlineData("", "Description", "usr_admin")]
    [InlineData("Title", "", "usr_admin")]
    [InlineData("Title", "Description", "")]
    public void JobPosting_Creation_InvalidArgs_ThrowsArgumentException(string title, string description, string createdBy)
    {
        var orgId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new JobPosting(orgId, title, description, createdBy));
    }

    [Fact]
    public void JobPosting_Update_ShouldUpdateFieldsAndTimestamp()
    {
        var orgId = Guid.NewGuid();
        var job = new JobPosting(orgId, "Junior Dev", "Initial desc", "usr_1");

        job.Update(
            "Mid-Level Dev",
            "Updated desc",
            EmploymentType.Contract,
            null,
            null,
            null,
            "Remote",
            "Go, Rust",
            "Backend pipelines",
            DateTime.UtcNow.AddDays(30));

        Assert.Equal("Mid-Level Dev", job.Title);
        Assert.Equal("Updated desc", job.Description);
        Assert.Equal(EmploymentType.Contract, job.EmploymentType);
        Assert.Equal("Remote", job.Location);
        Assert.Equal("Go, Rust", job.Requirements);
        Assert.Equal("Backend pipelines", job.Responsibilities);
        Assert.NotNull(job.UpdatedAtUtc);
    }

    [Fact]
    public void JobPosting_Publish_DraftToPublished_Succeeds()
    {
        var job = new JobPosting(Guid.NewGuid(), "Frontend Engineer", "React/TypeScript", "usr_1");
        var now = DateTime.UtcNow;

        job.Publish(now);

        Assert.Equal(JobPostingStatus.Published, job.Status);
        Assert.Equal(now, job.PublishedAtUtc);
    }

    [Fact]
    public void JobPosting_Publish_WhenNotDraft_ThrowsInvalidOperationException()
    {
        var job = new JobPosting(Guid.NewGuid(), "Frontend Engineer", "React/TypeScript", "usr_1");
        job.Publish(DateTime.UtcNow);
        job.Close(DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => job.Publish(DateTime.UtcNow));
    }

    [Fact]
    public void JobPosting_Close_PublishedToClosed_Succeeds()
    {
        var job = new JobPosting(Guid.NewGuid(), "QA Engineer", "Automation test", "usr_1");
        job.Publish(DateTime.UtcNow);
        var closeTime = DateTime.UtcNow.AddHours(1);

        job.Close(closeTime);

        Assert.Equal(JobPostingStatus.Closed, job.Status);
        Assert.Equal(closeTime, job.ClosedAtUtc);
    }

    [Fact]
    public void JobPosting_Close_WhenDraft_ThrowsInvalidOperationException()
    {
        var job = new JobPosting(Guid.NewGuid(), "QA Engineer", "Automation test", "usr_1");
        Assert.Throws<InvalidOperationException>(() => job.Close(DateTime.UtcNow));
    }

    [Fact]
    public void JobPosting_Cancel_DraftOrPublished_Succeeds()
    {
        var job1 = new JobPosting(Guid.NewGuid(), "DevOps Engineer", "Kubernetes", "usr_1");
        job1.Cancel(DateTime.UtcNow);
        Assert.Equal(JobPostingStatus.Cancelled, job1.Status);

        var job2 = new JobPosting(Guid.NewGuid(), "Product Manager", "Roadmaps", "usr_1");
        job2.Publish(DateTime.UtcNow);
        job2.Cancel(DateTime.UtcNow);
        Assert.Equal(JobPostingStatus.Cancelled, job2.Status);
    }

    [Fact]
    public void JobPosting_IsAcceptingApplications_EvaluatesCorrectly()
    {
        var now = DateTime.UtcNow;
        var job = new JobPosting(
            Guid.NewGuid(),
            "Data Scientist",
            "ML models",
            "usr_1",
            applicationDeadline: now.AddDays(7));

        // Draft: not accepting
        Assert.False(job.IsAcceptingApplications(now));

        // Published with future deadline: accepting
        job.Publish(now);
        Assert.True(job.IsAcceptingApplications(now));

        // Published with past deadline: not accepting
        Assert.False(job.IsAcceptingApplications(now.AddDays(8)));

        // Closed: not accepting
        job.Close(now.AddDays(2));
        Assert.False(job.IsAcceptingApplications(now));
    }

    [Fact]
    public void RecruitmentApplication_Creation_ShouldInitializeValidState()
    {
        var jobId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        var app = new RecruitmentApplication(
            jobId,
            orgId,
            "Ada Lovelace",
            "ada@example.com",
            "+2348011223344",
            "usr_ada",
            "https://cdn.example.com/resumes/ada.pdf",
            "I am excited to apply for this role.");

        Assert.NotEqual(Guid.Empty, app.Id);
        Assert.Equal(jobId, app.JobPostingId);
        Assert.Equal(orgId, app.OrganizationId);
        Assert.Equal("Ada Lovelace", app.ApplicantName);
        Assert.Equal("ada@example.com", app.ApplicantEmail);
        Assert.Equal("+2348011223344", app.ApplicantPhone);
        Assert.Equal("usr_ada", app.ApplicantUserId);
        Assert.Equal("https://cdn.example.com/resumes/ada.pdf", app.ResumeReference);
        Assert.Equal("I am excited to apply for this role.", app.CoverLetter);
        Assert.Equal(ApplicationStatus.Submitted, app.Status);
        Assert.Null(app.ReviewedByUserId);
        Assert.Null(app.ReviewedAtUtc);
        Assert.Null(app.RejectionReason);
    }

    [Theory]
    [InlineData("", "ada@example.com", "+2348011223344")]
    [InlineData("Ada", "", "+2348011223344")]
    [InlineData("Ada", "ada@example.com", "")]
    public void RecruitmentApplication_Creation_InvalidArgs_ThrowsArgumentException(string name, string email, string phone)
    {
        var jobId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new RecruitmentApplication(jobId, orgId, name, email, phone));
    }

    [Fact]
    public void RecruitmentApplication_Lifecycle_HappyPath_ReviewShortlistAccept()
    {
        var app = new RecruitmentApplication(Guid.NewGuid(), Guid.NewGuid(), "Grace Hopper", "grace@navy.mil", "+12025550199");
        var now = DateTime.UtcNow;

        // Review
        app.Review("usr_recruiter", now, "Impressive profile");
        Assert.Equal(ApplicationStatus.UnderReview, app.Status);
        Assert.Equal("usr_recruiter", app.ReviewedByUserId);
        Assert.Equal(now, app.ReviewedAtUtc);
        Assert.Equal("Impressive profile", app.ReviewNotes);

        // Shortlist
        var shortlistTime = now.AddHours(2);
        app.Shortlist("usr_recruiter", shortlistTime, "Shortlisted for interview");
        Assert.Equal(ApplicationStatus.Shortlisted, app.Status);
        Assert.Equal(shortlistTime, app.ReviewedAtUtc);
        Assert.Equal("Shortlisted for interview", app.ReviewNotes);

        // Accept (offer extended)
        var acceptTime = now.AddDays(1);
        app.Accept("usr_recruiter", acceptTime, "Offer extended and accepted");
        Assert.Equal(ApplicationStatus.Accepted, app.Status);
        Assert.Equal(acceptTime, app.ReviewedAtUtc);
    }

    [Fact]
    public void RecruitmentApplication_Lifecycle_Reject_FromAnyActiveState()
    {
        var app = new RecruitmentApplication(Guid.NewGuid(), Guid.NewGuid(), "Alan Turing", "alan@bletchley.uk", "+441908000000");
        var now = DateTime.UtcNow;

        app.Reject("usr_recruiter", "Position filled internally", now, "Good candidate for future roles");

        Assert.Equal(ApplicationStatus.Rejected, app.Status);
        Assert.Equal("Position filled internally", app.RejectionReason);
        Assert.Equal("usr_recruiter", app.ReviewedByUserId);
        Assert.Equal(now, app.ReviewedAtUtc);
    }

    [Fact]
    public void RecruitmentApplication_Lifecycle_Withdraw_CandidateSelfService()
    {
        var app = new RecruitmentApplication(Guid.NewGuid(), Guid.NewGuid(), "Margaret Hamilton", "margaret@mit.edu", "+16175550123");
        var now = DateTime.UtcNow;

        app.Withdraw(now);

        Assert.Equal(ApplicationStatus.Withdrawn, app.Status);
    }

    [Fact]
    public void RecruitmentApplication_Lifecycle_InvalidTransitions_ThrowExceptions()
    {
        var app = new RecruitmentApplication(Guid.NewGuid(), Guid.NewGuid(), "Claude Shannon", "claude@bell.com", "+12015550188");
        app.Withdraw(DateTime.UtcNow);

        // Cannot review withdrawn application
        Assert.Throws<InvalidOperationException>(() => app.Review("usr_recruiter", DateTime.UtcNow));

        // Cannot accept withdrawn application
        Assert.Throws<InvalidOperationException>(() => app.Accept("usr_recruiter", DateTime.UtcNow));

        // Cannot reject withdrawn application
        Assert.Throws<InvalidOperationException>(() => app.Reject("usr_recruiter", "Reason", DateTime.UtcNow));
    }
}
