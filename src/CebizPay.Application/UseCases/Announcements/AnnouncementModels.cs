using CebizPay.Domain.Communication.Enums;

namespace CebizPay.Application.UseCases.Announcements;

/// <summary>
/// Data transfer object representing an announcement.
/// </summary>
public sealed record AnnouncementDto(
    Guid Id,
    Guid? OrganizationId,
    string? OrganizationName,
    string Title,
    string Description,
    AnnouncementScope Scope,
    AnnouncementStatus Status,
    DateTime? PublishedAtUtc,
    string? PublishedByUserId,
    DateTime CreatedAtUtc,
    string CreatedByUserId,
    DateTime? UpdatedAtUtc,
    string? UpdatedByUserId,
    DateTime? ArchivedAtUtc,
    string? ArchivedByUserId);

/// <summary>
/// Request payload for creating a new announcement.
/// </summary>
public sealed record CreateAnnouncementRequest(
    AnnouncementScope Scope,
    string Title,
    string Description,
    bool PublishImmediately = false);
