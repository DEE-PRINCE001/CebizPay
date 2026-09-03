using CebizPay.Domain.Communication.Enums;

namespace CebizPay.Domain.Communication.Events;

/// <summary>
/// Domain event published when an announcement is officially published.
/// Propagated through Outbox and RabbitMQ to trigger asynchronous notification fan-out.
/// </summary>
public sealed record AnnouncementPublishedDomainEvent(
    Guid AnnouncementId,
    AnnouncementScope Scope,
    Guid? OrganizationId,
    string Title,
    string Description,
    string PublishedByUserId,
    DateTime PublishedAtUtc);
