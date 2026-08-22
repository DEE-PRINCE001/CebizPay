using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Domain.Payments.Entities;

/// <summary>
/// Domain aggregate entity representing a dedicated virtual account (DVA) assigned to an individual user or organization.
/// Maps external inbound bank transfers to the owner's internal CebizPay wallet.
/// </summary>
public class VirtualAccount
{
    /// <summary>Unique virtual account identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Owning individual user identity ID (null if organization-owned).</summary>
    public string? IndividualId { get; private set; }

    /// <summary>Owning organization ID (null if individual-owned).</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Payment service provider issuing the virtual account (Flutterwave or Paystack).</summary>
    public PaymentProvider Provider { get; private set; }

    /// <summary>NUBAN / Virtual account number.</summary>
    public string AccountNumber { get; private set; } = string.Empty;

    /// <summary>Beneficiary account name assigned by the provider.</summary>
    public string AccountName { get; private set; } = string.Empty;

    /// <summary>Partner bank institution code.</summary>
    public string BankCode { get; private set; } = string.Empty;

    /// <summary>Partner bank institution name.</summary>
    public string BankName { get; private set; } = string.Empty;

    /// <summary>Currency for this virtual account (strictly transactional V1 currency).</summary>
    public Currency Currency { get; private set; }

    /// <summary>Current virtual account lifecycle status.</summary>
    public VirtualAccountStatus Status { get; private set; }

    /// <summary>Provider-specific reference or order token.</summary>
    public string? ProviderReference { get; private set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last updated timestamp (UTC).</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    private VirtualAccount() { }

    /// <summary>
    /// Creates a dedicated virtual account for an individual user.
    /// </summary>
    public static VirtualAccount CreateIndividual(
        string individualId,
        PaymentProvider provider,
        string accountNumber,
        string accountName,
        string bankCode,
        string bankName,
        Currency currency,
        string? providerReference = null)
    {
        if (string.IsNullOrWhiteSpace(individualId))
            throw new ArgumentException("IndividualId cannot be empty.", nameof(individualId));

        ValidateCommonFields(accountNumber, accountName, bankCode, bankName, currency);

        return new VirtualAccount
        {
            Id = Guid.NewGuid(),
            IndividualId = individualId,
            OrganizationId = null,
            Provider = provider,
            AccountNumber = accountNumber.Trim(),
            AccountName = accountName.Trim(),
            BankCode = bankCode.Trim(),
            BankName = bankName.Trim(),
            Currency = currency,
            Status = VirtualAccountStatus.Active,
            ProviderReference = providerReference?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a dedicated virtual account for an organization.
    /// </summary>
    public static VirtualAccount CreateOrganization(
        Guid organizationId,
        PaymentProvider provider,
        string accountNumber,
        string accountName,
        string bankCode,
        string bankName,
        Currency currency,
        string? providerReference = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));

        ValidateCommonFields(accountNumber, accountName, bankCode, bankName, currency);

        return new VirtualAccount
        {
            Id = Guid.NewGuid(),
            IndividualId = null,
            OrganizationId = organizationId,
            Provider = provider,
            AccountNumber = accountNumber.Trim(),
            AccountName = accountName.Trim(),
            BankCode = bankCode.Trim(),
            BankName = bankName.Trim(),
            Currency = currency,
            Status = VirtualAccountStatus.Active,
            ProviderReference = providerReference?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static void ValidateCommonFields(string accountNumber, string accountName, string bankCode, string bankName, Currency currency)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new ArgumentException("AccountNumber cannot be empty.", nameof(accountNumber));

        if (string.IsNullOrWhiteSpace(accountName))
            throw new ArgumentException("AccountName cannot be empty.", nameof(accountName));

        if (string.IsNullOrWhiteSpace(bankCode))
            throw new ArgumentException("BankCode cannot be empty.", nameof(bankCode));

        if (string.IsNullOrWhiteSpace(bankName))
            throw new ArgumentException("BankName cannot be empty.", nameof(bankName));

        currency.EnsureTransactionalV1();
    }

    /// <summary>
    /// Activates the virtual account.
    /// </summary>
    public void MarkActive()
    {
        Status = VirtualAccountStatus.Active;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Suspends the virtual account.
    /// </summary>
    public void MarkSuspended()
    {
        Status = VirtualAccountStatus.Suspended;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Closes the virtual account permanently.
    /// </summary>
    public void MarkClosed()
    {
        Status = VirtualAccountStatus.Closed;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
