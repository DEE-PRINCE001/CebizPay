namespace CebizPay.Application.Common.Interfaces.Finance;

/// <summary>
/// Result of resolving a destination bank account name.
/// </summary>
public sealed record BankAccountResolutionResult(
    bool Succeeded,
    string? AccountName,
    string? BankCode,
    string? AccountNumber,
    string? ErrorMessage = null);

/// <summary>
/// Application abstraction boundary for resolving destination bank account names.
/// (Concrete provider integrations belong to Phase 3).
/// </summary>
public interface IBankAccountResolver
{
    /// <summary>
    /// Attempts to resolve the beneficiary name for a given bank institution code and account number.
    /// </summary>
    Task<BankAccountResolutionResult> ResolveAsync(
        string bankCode,
        string accountNumber,
        CancellationToken cancellationToken = default);
}
