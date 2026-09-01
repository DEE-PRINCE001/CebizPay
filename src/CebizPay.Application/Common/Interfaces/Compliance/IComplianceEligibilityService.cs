using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Financial enforcement boundary evaluating transaction compliance eligibility before execution.
/// Ensures fail-closed gating without modifying ledger or wallet balances directly.
/// </summary>
public interface IComplianceEligibilityService
{
    /// <summary>
    /// Evaluates whether a financial transaction is permissible under current compliance decisions, restrictions, and KYC caps.
    /// </summary>
    Task<TransactionEligibilityResult> EvaluateEligibilityAsync(
        string userId,
        Guid? organizationId,
        ComplianceOperationType operationType,
        decimal amount,
        Currency currency,
        CancellationToken cancellationToken = default);
}
