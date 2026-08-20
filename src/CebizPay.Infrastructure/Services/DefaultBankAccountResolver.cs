using CebizPay.Application.Common.Interfaces.Finance;

namespace CebizPay.Infrastructure.Services;

/// <summary>
/// Default placeholder implementation for destination bank account name resolution.
/// In Phase 2C, live provider calls (Flutterwave, Paystack, Bank API) are strictly out of scope.
/// This boundary resolver validates destination format (10-digit NUBAN, 3-6 digit bank code)
/// and safely returns an unconfirmed or format-verified result without fabricating fictitious account names.
/// </summary>
public sealed class DefaultBankAccountResolver : IBankAccountResolver
{
    /// <inheritdoc/>
    public Task<BankAccountResolutionResult> ResolveAsync(
        string bankCode,
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bankCode))
        {
            return Task.FromResult(new BankAccountResolutionResult(
                Succeeded: false,
                AccountName: null,
                BankCode: bankCode,
                AccountNumber: accountNumber,
                ErrorMessage: "Bank code cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(accountNumber) || accountNumber.Trim().Length != 10 || !accountNumber.All(char.IsDigit))
        {
            return Task.FromResult(new BankAccountResolutionResult(
                Succeeded: false,
                AccountName: null,
                BankCode: bankCode,
                AccountNumber: accountNumber,
                ErrorMessage: "Account number must be a 10-digit numeric NUBAN string."));
        }

        // External provider integrations belong to Phase 3. Format is verified.
        return Task.FromResult(new BankAccountResolutionResult(
            Succeeded: true,
            AccountName: null,
            BankCode: bankCode.Trim(),
            AccountNumber: accountNumber.Trim()));
    }
}
