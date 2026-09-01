using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Base marker interface for compliance verification provider implementations.
/// </summary>
public interface IVerificationProvider
{
    /// <summary>Verification provider identifier.</summary>
    VerificationProvider Provider { get; }
}
