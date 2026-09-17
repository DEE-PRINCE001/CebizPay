namespace CebizPay.Infrastructure.Savings.Providers.Anchor;

/// <summary>
/// HTTP client contract for interacting with Anchor BaaS API.
/// </summary>
public interface IAnchorClient
{
    /// <summary>Creates or fetches an individual customer profile at Anchor.</summary>
    Task<AnchorResource<AnchorIndividualCustomerAttributes>?> CreateCustomerAsync(
        AnchorResourceEnvelope<AnchorResource<AnchorIndividualCustomerAttributes>> request,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a subledger account tied to a customer under the root FBO deposit account.</summary>
    Task<AnchorResource<AnchorSubAccountAttributes>?> CreateSubAccountAsync(
        AnchorResourceEnvelope<AnchorResource<AnchorSubAccountAttributes>> request,
        CancellationToken cancellationToken = default);

    /// <summary>Fetches live details and balances of an Anchor subledger account.</summary>
    Task<AnchorResource<AnchorSubAccountAttributes>?> GetSubAccountAsync(
        string subAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>Executes a transfer (such as a BookTransfer between FBO and subledger).</summary>
    Task<AnchorResource<AnchorTransferAttributes>?> CreateTransferAsync(
        AnchorResourceEnvelope<AnchorResource<AnchorTransferAttributes>> request,
        CancellationToken cancellationToken = default);
}
