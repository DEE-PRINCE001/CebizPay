namespace CebizPay.Application.Common.Exceptions;

/// <summary>
/// Thrown when a transfer is blocked by KYC/KYB compliance rules.
/// For example: an unverified organization attempting an outbound transfer.
/// Maps to HTTP 422 Unprocessable Entity with code TRANSFER_COMPLIANCE_RESTRICTED.
/// </summary>
public sealed class ComplianceRestrictedException : Exception
{
    /// <summary>Stable machine-readable error code.</summary>
    public string Code { get; } = "TRANSFER_COMPLIANCE_RESTRICTED";

    /// <summary>
    /// Initializes a new instance of <see cref="ComplianceRestrictedException"/>.
    /// </summary>
    public ComplianceRestrictedException(string reason)
        : base(reason)
    {
    }
}
