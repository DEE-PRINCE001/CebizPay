using CebizPay.Domain.Communication.Enums;

namespace CebizPay.Domain.Communication.Entities;

/// <summary>
/// Domain aggregate representing an authoritative communication announcement.
/// Supports both global Platform announcements (Super Admin only) and tenant-isolated Workplace announcements (HR Manager / Org Owner).
/// </summary>
public class Announcement
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Target organization ID for Workplace announcements.
    /// Invariant: MUST be NULL for Platform announcements and MUST NOT be NULL for Workplace announcements.
    /// </summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Announcement headline/title.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Announcement content / body / description.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Target audience scope (Platform or Workplace).</summary>
    public AnnouncementScope Scope { get; private set; }

    /// <summary>Explicit publication lifecycle status (Draft, Published, Archived).</summary>
    public AnnouncementStatus Status { get; private set; } = AnnouncementStatus.Draft;

    /// <summary>Timestamp when the announcement was officially published.</summary>
    public DateTime? PublishedAtUtc { get; private set; }

    /// <summary>User ID of the actor who published the announcement.</summary>
    public string? PublishedByUserId { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>User ID of the actor who created the announcement.</summary>
    public string CreatedByUserId { get; private set; } = string.Empty;

    /// <summary>Last updated timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>User ID of the actor who last modified the announcement.</summary>
    public string? UpdatedByUserId { get; private set; }

    /// <summary>Timestamp when the announcement was archived.</summary>
    public DateTime? ArchivedAtUtc { get; private set; }

    /// <summary>User ID of the actor who archived the announcement.</summary>
    public string? ArchivedByUserId { get; private set; }

    private Announcement() { } // EF Core

    /// <summary>
    /// Factory method to create a new global Platform announcement in Draft status.
    /// Enforces the invariant that OrganizationId must be null.
    /// </summary>
    public static Announcement CreatePlatform(
        string title,
        string description,
        string createdByUserId)
    {
        ValidateCommonFields(title, description, createdByUserId);

        return new Announcement
        {
            Id = Guid.NewGuid(),
            OrganizationId = null,
            Title = title.Trim(),
            Description = description.Trim(),
            Scope = AnnouncementScope.Platform,
            Status = AnnouncementStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId.Trim()
        };
    }

    /// <summary>
    /// Factory method to create a new tenant-isolated Workplace announcement in Draft status.
    /// Enforces the invariant that OrganizationId must be non-null and not empty.
    /// </summary>
    public static Announcement CreateWorkplace(
        Guid organizationId,
        string title,
        string description,
        string createdByUserId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Workplace announcement must have a valid non-empty OrganizationId.", nameof(organizationId));
        }

        ValidateCommonFields(title, description, createdByUserId);

        return new Announcement
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Title = title.Trim(),
            Description = description.Trim(),
            Scope = AnnouncementScope.Workplace,
            Status = AnnouncementStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId.Trim()
        };
    }

    /// <summary>
    /// Explicitly transitions the announcement from Draft to Published.
    /// </summary>
    public void Publish(string publishedByUserId, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(publishedByUserId))
        {
            throw new ArgumentException("PublishedByUserId is required.", nameof(publishedByUserId));
        }

        if (Status == AnnouncementStatus.Archived)
        {
            throw new InvalidOperationException("Cannot publish an announcement that is already archived.");
        }

        if (Status == AnnouncementStatus.Published)
        {
            throw new InvalidOperationException("Announcement is already published.");
        }

        Status = AnnouncementStatus.Published;
        PublishedAtUtc = now;
        PublishedByUserId = publishedByUserId.Trim();
        UpdatedAtUtc = now;
        UpdatedByUserId = publishedByUserId.Trim();
    }

    /// <summary>
    /// Explicitly archives the announcement, permanently hiding it from active feeds.
    /// </summary>
    public void Archive(string archivedByUserId, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(archivedByUserId))
        {
            throw new ArgumentException("ArchivedByUserId is required.", nameof(archivedByUserId));
        }

        if (Status == AnnouncementStatus.Archived)
        {
            return; // Idempotent
        }

        Status = AnnouncementStatus.Archived;
        ArchivedAtUtc = now;
        ArchivedByUserId = archivedByUserId.Trim();
        UpdatedAtUtc = now;
        UpdatedByUserId = archivedByUserId.Trim();
    }

    /// <summary>
    /// Updates announcement title and description prior to archiving.
    /// </summary>
    public void Update(string title, string description, string updatedByUserId, DateTime now)
    {
        if (Status == AnnouncementStatus.Archived)
        {
            throw new InvalidOperationException("Cannot update an announcement that is already archived.");
        }

        ValidateCommonFields(title, description, updatedByUserId);

        Title = title.Trim();
        Description = description.Trim();
        UpdatedAtUtc = now;
        UpdatedByUserId = updatedByUserId.Trim();
    }

    private static void ValidateCommonFields(string title, string description, string userId)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        if (title.Trim().Length > 200)
        {
            throw new ArgumentException("Title cannot exceed 200 characters.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required.", nameof(description));
        }

        if (description.Trim().Length > 4000)
        {
            throw new ArgumentException("Description cannot exceed 4000 characters.", nameof(description));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }
    }
}
