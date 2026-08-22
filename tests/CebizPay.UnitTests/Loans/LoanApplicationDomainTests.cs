using CebizPay.Domain.Loans.Entities;
using CebizPay.Domain.Loans.Enums;
using Xunit;

namespace CebizPay.UnitTests.Loans;

public class LoanApplicationDomainTests
{
    [Fact]
    public void Create_InitializesInDraftState()
    {
        var orgId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var app = LoanApplication.Create(
            orgId,
            planId,
            "usr-100",
            "John Doe",
            500_000m,
            0.10m,
            12,
            45_833.33m,
            50_000m,
            550_000m,
            400_000m,
            0m,
            45_833.33m,
            45_833.33m,
            0.1146m,
            true,
            null,
            RepaymentFrequency.Monthly,
            false);

        Assert.Equal(LoanApplicationStatus.Draft, app.Status);
        Assert.Equal("usr-100", app.ApplicantUserId);
        Assert.Equal(500_000m, app.RequestedAmount);
        Assert.True(app.IsDtiCompliantSnapshot);
    }

    [Fact]
    public void Submit_TransitionsDraftToSubmitted()
    {
        var app = LoanApplication.Create(
            Guid.NewGuid(), Guid.NewGuid(), "usr-100", "John Doe", 500_000m, 0.10m, 12,
            45_000m, 50_000m, 550_000m, 400_000m, 0m, 45_000m, 45_000m, 0.11m, true,
            null, RepaymentFrequency.Monthly, false);

        app.Submit();

        Assert.Equal(LoanApplicationStatus.Submitted, app.Status);
    }

    [Fact]
    public void Approve_ByDifferentApprover_TransitionsToApproved()
    {
        var app = LoanApplication.Create(
            Guid.NewGuid(), Guid.NewGuid(), "usr-100", "John Doe", 500_000m, 0.10m, 12,
            45_000m, 50_000m, 550_000m, 400_000m, 0m, 45_000m, 45_000m, 0.11m, true);

        app.Approve("admin-999");

        Assert.Equal(LoanApplicationStatus.Approved, app.Status);
        Assert.Equal("admin-999", app.DeciderUserId);
        Assert.NotNull(app.DecidedAtUtc);
    }

    [Fact]
    public void Approve_SelfApproval_ThrowsInvalidOperationException()
    {
        var app = LoanApplication.Create(
            Guid.NewGuid(), Guid.NewGuid(), "usr-100", "John Doe", 500_000m, 0.10m, 12,
            45_000m, 50_000m, 550_000m, 400_000m, 0m, 45_000m, 45_000m, 0.11m, true);

        // Invariant: Self-approval is strictly forbidden
        Assert.Throws<InvalidOperationException>(() => app.Approve("usr-100"));
    }

    [Fact]
    public void Decline_WithReason_TransitionsToDeclined()
    {
        var app = LoanApplication.Create(
            Guid.NewGuid(), Guid.NewGuid(), "usr-100", "John Doe", 500_000m, 0.10m, 12,
            45_000m, 50_000m, 550_000m, 400_000m, 0m, 45_000m, 45_000m, 0.11m, true);

        app.Decline("admin-999", "Failed company internal policy check.");

        Assert.Equal(LoanApplicationStatus.Declined, app.Status);
        Assert.Equal("admin-999", app.DeciderUserId);
        Assert.Equal("Failed company internal policy check.", app.DeclinedReason);
    }
}
