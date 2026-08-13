using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Finance.Events;

/// <summary>
/// Domain event emitted when a wallet is created.
/// </summary>
/// <param name="WalletId">Created wallet ID.</param>
/// <param name="IndividualId">Individual owner ID if applicable.</param>
/// <param name="OrganizationId">Organization owner ID if applicable.</param>
/// <param name="Currency">Wallet currency.</param>
/// <param name="OccurredOnUtc">Timestamp when event occurred.</param>
public sealed record WalletCreatedDomainEvent(
    Guid WalletId,
    string? IndividualId,
    Guid? OrganizationId,
    Currency Currency,
    DateTime OccurredOnUtc);
