namespace CebizPay.Domain.Events;

/// <summary>
/// Event emitted when a user is successfully registered.
/// </summary>
/// <param name="UserId">The unique identity ID of the user.</param>
/// <param name="Email">The email address of the registered user.</param>
/// <param name="PhoneNumber">The phone number of the user.</param>
/// <param name="OccurredOnUtc">Timestamp when the event occurred.</param>
public sealed record UserRegisteredDomainEvent(
    string UserId,
    string Email,
    string? PhoneNumber,
    DateTime OccurredOnUtc);
