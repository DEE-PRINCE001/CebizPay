using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Provider-neutral abstraction for provisioning and managing dedicated/dynamic virtual accounts.
/// </summary>
public interface IVirtualAccountProvider
{
    /// <summary>The payment provider identifier implemented by this adapter.</summary>
    PaymentProvider Provider { get; }

    /// <summary>
    /// Provisions a dedicated virtual account for the specified customer/organization.
    /// </summary>
    Task<VirtualAccountCreationResult> CreateVirtualAccountAsync(
        VirtualAccountCreationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the current status of a provisioned virtual account with the provider.
    /// </summary>
    Task<VirtualAccountStatusResult> GetVirtualAccountStatusAsync(
        string providerReference,
        CancellationToken cancellationToken = default);
}
