using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Service contract for orchestrating dedicated virtual account provisioning and lifecycle management.
/// </summary>
public interface IVirtualAccountService
{
    /// <summary>
    /// Provisions or retrieves the primary persistent dedicated virtual account for an Individual.
    /// </summary>
    Task<VirtualAccountDto> ProvisionIndividualVirtualAccountAsync(
        string individualId,
        Currency currency,
        PaymentProvider provider = PaymentProvider.Flutterwave,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Provisions or retrieves the primary persistent dedicated virtual account for an Organization.
    /// </summary>
    Task<VirtualAccountDto> ProvisionOrganizationVirtualAccountAsync(
        Guid organizationId,
        Currency currency,
        PaymentProvider provider = PaymentProvider.Flutterwave,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the primary virtual account for the specified owner, if one exists.
    /// </summary>
    Task<VirtualAccountDto?> GetVirtualAccountForOwnerAsync(
        string? individualId,
        Guid? organizationId,
        Currency currency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a virtual account by provider and account number.
    /// </summary>
    Task<VirtualAccountDto?> GetVirtualAccountByNumberAsync(
        PaymentProvider provider,
        string accountNumber,
        CancellationToken cancellationToken = default);
}
