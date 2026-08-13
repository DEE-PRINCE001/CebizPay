namespace CebizPay.Domain.Events;

/// <summary>
/// Event emitted when an organization is registered.
/// </summary>
/// <param name="OrganizationId">Unique ID of the organization.</param>
/// <param name="CompanyName">Company name.</param>
/// <param name="Email">Company contact email.</param>
/// <param name="OccurredOnUtc">Timestamp when the event occurred.</param>
public sealed record OrganizationRegisteredDomainEvent(
    Guid OrganizationId,
    string CompanyName,
    string Email,
    DateTime OccurredOnUtc);
