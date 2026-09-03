using CebizPay.Domain.Communication.Entities;
using CebizPay.Domain.Communication.Enums;
using Xunit;

namespace CebizPay.UnitTests.Domain;

/// <summary>
/// Domain unit tests for Announcement aggregate root.
/// Verifies domain invariants, scope enforcement, and explicit publication lifecycle.
/// </summary>
public sealed class AnnouncementTests
{
    [Fact]
    public void CreatePlatform_ShouldInitializeInDraftStatusWithNullOrganizationId()
    {
        var title = "System Maintenance Notice";
        var description = "Scheduled platform maintenance on Sunday at 2:00 AM UTC.";
        var userId = "super-admin-user-id";

        var announcement = Announcement.CreatePlatform(title, description, userId);

        Assert.NotEqual(Guid.Empty, announcement.Id);
        Assert.Null(announcement.OrganizationId);
        Assert.Equal(title, announcement.Title);
        Assert.Equal(description, announcement.Description);
        Assert.Equal(AnnouncementScope.Platform, announcement.Scope);
        Assert.Equal(AnnouncementStatus.Draft, announcement.Status);
        Assert.Null(announcement.PublishedAtUtc);
        Assert.Null(announcement.PublishedByUserId);
        Assert.Equal(userId, announcement.CreatedByUserId);
        Assert.Null(announcement.ArchivedAtUtc);
        Assert.Null(announcement.ArchivedByUserId);
    }

    [Fact]
    public void CreateWorkplace_ShouldInitializeInDraftStatusWithOrganizationId()
    {
        var orgId = Guid.NewGuid();
        var title = "All Hands Meeting";
        var description = "Quarterly review meeting on Friday at 3:00 PM.";
        var userId = "hr-manager-user-id";

        var announcement = Announcement.CreateWorkplace(orgId, title, description, userId);

        Assert.NotEqual(Guid.Empty, announcement.Id);
        Assert.Equal(orgId, announcement.OrganizationId);
        Assert.Equal(title, announcement.Title);
        Assert.Equal(description, announcement.Description);
        Assert.Equal(AnnouncementScope.Workplace, announcement.Scope);
        Assert.Equal(AnnouncementStatus.Draft, announcement.Status);
        Assert.Null(announcement.PublishedAtUtc);
        Assert.Equal(userId, announcement.CreatedByUserId);
    }

    [Fact]
    public void CreateWorkplace_WithEmptyOrganizationId_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Announcement.CreateWorkplace(
            Guid.Empty,
            "Title",
            "Description",
            "user-1"));
    }

    [Theory]
    [InlineData("", "Description", "user")]
    [InlineData("   ", "Description", "user")]
    [InlineData("Title", "", "user")]
    [InlineData("Title", "   ", "user")]
    [InlineData("Title", "Description", "")]
    public void CreatePlatform_WithInvalidArguments_ShouldThrowArgumentException(string title, string description, string userId)
    {
        Assert.Throws<ArgumentException>(() => Announcement.CreatePlatform(title, description, userId));
    }

    [Fact]
    public void CreatePlatform_WithTitleExceeding200Chars_ShouldThrowArgumentException()
    {
        var longTitle = new string('A', 201);
        Assert.Throws<ArgumentException>(() => Announcement.CreatePlatform(longTitle, "Description", "user-1"));
    }

    [Fact]
    public void CreatePlatform_WithDescriptionExceeding4000Chars_ShouldThrowArgumentException()
    {
        var longDesc = new string('B', 4001);
        Assert.Throws<ArgumentException>(() => Announcement.CreatePlatform("Title", longDesc, "user-1"));
    }

    [Fact]
    public void Publish_WhenInDraft_ShouldTransitionToPublishedStatus()
    {
        var announcement = Announcement.CreatePlatform("Title", "Desc", "user-1");
        var now = DateTime.UtcNow;

        announcement.Publish("publisher-user", now);

        Assert.Equal(AnnouncementStatus.Published, announcement.Status);
        Assert.Equal(now, announcement.PublishedAtUtc);
        Assert.Equal("publisher-user", announcement.PublishedByUserId);
        Assert.Equal(now, announcement.UpdatedAtUtc);
        Assert.Equal("publisher-user", announcement.UpdatedByUserId);
    }

    [Fact]
    public void Publish_WhenAlreadyPublished_ShouldThrowInvalidOperationException()
    {
        var announcement = Announcement.CreatePlatform("Title", "Desc", "user-1");
        announcement.Publish("publisher-1", DateTime.UtcNow);

        var ex = Assert.Throws<InvalidOperationException>(() => announcement.Publish("publisher-2", DateTime.UtcNow));
        Assert.Contains("already published", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Publish_WhenArchived_ShouldThrowInvalidOperationException()
    {
        var announcement = Announcement.CreatePlatform("Title", "Desc", "user-1");
        announcement.Archive("archiver-user", DateTime.UtcNow);

        var ex = Assert.Throws<InvalidOperationException>(() => announcement.Publish("publisher-user", DateTime.UtcNow));
        Assert.Contains("archived", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Archive_WhenInDraftOrPublished_ShouldTransitionToArchivedStatus()
    {
        var announcement = Announcement.CreatePlatform("Title", "Desc", "user-1");
        var now = DateTime.UtcNow;

        announcement.Archive("archiver-user", now);

        Assert.Equal(AnnouncementStatus.Archived, announcement.Status);
        Assert.Equal(now, announcement.ArchivedAtUtc);
        Assert.Equal("archiver-user", announcement.ArchivedByUserId);

        // Idempotent call
        announcement.Archive("another-user", DateTime.UtcNow);
        Assert.Equal(now, announcement.ArchivedAtUtc);
        Assert.Equal("archiver-user", announcement.ArchivedByUserId);
    }

    [Fact]
    public void Update_WhenActive_ShouldUpdateContentAndTimestamps()
    {
        var announcement = Announcement.CreatePlatform("Old Title", "Old Desc", "user-1");
        var now = DateTime.UtcNow;

        announcement.Update("New Title", "New Desc", "editor-user", now);

        Assert.Equal("New Title", announcement.Title);
        Assert.Equal("New Desc", announcement.Description);
        Assert.Equal(now, announcement.UpdatedAtUtc);
        Assert.Equal("editor-user", announcement.UpdatedByUserId);
    }

    [Fact]
    public void Update_WhenArchived_ShouldThrowInvalidOperationException()
    {
        var announcement = Announcement.CreatePlatform("Title", "Desc", "user-1");
        announcement.Archive("archiver", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => announcement.Update("New", "New", "editor", DateTime.UtcNow));
    }
}
