using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Utils;
using CebizPay.Domain.Finance.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Wallet.Transfer;

/// <summary>
/// Response payload for wallet destination resolution.
/// </summary>
public sealed record WalletResolutionResponseDto(
    Guid WalletId,
    string AccountName,
    string AccountType,
    string Currency,
    string Status,
    string? PhoneNumber = null,
    string? Email = null);

/// <summary>
/// Query to resolve destination wallet holder details using phone number, email, or wallet GUID.
/// </summary>
public sealed record ResolveWalletQuery(string Identifier) : IRequest<WalletResolutionResponseDto?>;

/// <summary>
/// Validator for ResolveWalletQuery.
/// </summary>
public sealed class ResolveWalletQueryValidator : AbstractValidator<ResolveWalletQuery>
{
    /// <summary>
    /// Initializes validation rules.
    /// </summary>
    public ResolveWalletQueryValidator()
    {
        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage("Wallet identifier is required.");
    }
}

/// <summary>
/// Handler for <see cref="ResolveWalletQuery"/>.
/// </summary>
public sealed class ResolveWalletQueryHandler : IRequestHandler<ResolveWalletQuery, WalletResolutionResponseDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUserLookupService _userLookup;

    /// <summary>
    /// Initializes a new instance of <see cref="ResolveWalletQueryHandler"/>.
    /// </summary>
    public ResolveWalletQueryHandler(
        IApplicationDbContext dbContext,
        IUserLookupService userLookup)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userLookup = userLookup ?? throw new ArgumentNullException(nameof(userLookup));
    }

    /// <inheritdoc/>
    public async Task<WalletResolutionResponseDto?> Handle(
        ResolveWalletQuery request,
        CancellationToken cancellationToken)
    {
        var raw = request.Identifier?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        // ─── 1. Resolve by Phone Number ──────────────────────────────────────────
        var isLikelyPhone = raw.StartsWith('+') || (raw.All(char.IsDigit) && raw.Length is >= 10 and <= 15);
        if (isLikelyPhone)
        {
            var user = await _userLookup.FindByPhoneAsync(raw, cancellationToken).ConfigureAwait(false);
            if (user != null)
            {
                return await ResolveForIndividualUserAsync(user.UserId, user.PhoneNumber, user.Email, cancellationToken).ConfigureAwait(false);
            }
        }

        // ─── 2. Resolve by Email ────────────────────────────────────────────────
        if (raw.Contains('@', StringComparison.Ordinal))
        {
            var user = await _userLookup.FindByEmailAsync(raw, cancellationToken).ConfigureAwait(false);
            if (user != null)
            {
                return await ResolveForIndividualUserAsync(user.UserId, user.PhoneNumber, user.Email, cancellationToken).ConfigureAwait(false);
            }
        }

        // ─── 3. Resolve by GUID (Wallet ID) ─────────────────────────────────────
        if (Guid.TryParse(raw, out var walletGuid))
        {
            var wallet = await _dbContext.Wallets
                .FirstOrDefaultAsync(w => w.Id == walletGuid, cancellationToken)
                .ConfigureAwait(false);

            if (wallet != null)
            {
                return await MapWalletAsync(wallet, cancellationToken).ConfigureAwait(false);
            }
        }

        // ─── 4. Fallback: Search individual user by Raw User ID ───────────────────
        var directWallet = await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.IndividualId == raw && w.Currency == Currency.NGN, cancellationToken)
            .ConfigureAwait(false);

        if (directWallet != null)
        {
            return await MapWalletAsync(directWallet, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private async Task<WalletResolutionResponseDto?> ResolveForIndividualUserAsync(
        string userId,
        string? phone,
        string? email,
        CancellationToken cancellationToken)
    {
        var wallet = await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.IndividualId == userId && w.Currency == Currency.NGN, cancellationToken)
            .ConfigureAwait(false);

        if (wallet == null)
        {
            return null;
        }

        var profile = await _dbContext.IndividualProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        var fullName = profile != null
            ? $"{profile.FirstName} {profile.LastName}".Trim()
            : "CebizPay User";

        return new WalletResolutionResponseDto(
            WalletId: wallet.Id,
            AccountName: string.IsNullOrWhiteSpace(fullName) ? "CebizPay User" : fullName,
            AccountType: "Individual",
            Currency: wallet.Currency.ToString(),
            Status: wallet.Status.ToString(),
            PhoneNumber: phone,
            Email: email);
    }

    private async Task<WalletResolutionResponseDto> MapWalletAsync(
        Domain.Finance.Entities.Wallet wallet,
        CancellationToken cancellationToken)
    {
        if (wallet.OrganizationId.HasValue)
        {
            var org = await _dbContext.Organizations
                .FirstOrDefaultAsync(o => o.Id == wallet.OrganizationId.Value, cancellationToken)
                .ConfigureAwait(false);

            return new WalletResolutionResponseDto(
                WalletId: wallet.Id,
                AccountName: org?.CompanyName ?? "Corporate Account",
                AccountType: "Organization",
                Currency: wallet.Currency.ToString(),
                Status: wallet.Status.ToString(),
                PhoneNumber: null,
                Email: null);
        }

        if (!string.IsNullOrWhiteSpace(wallet.IndividualId))
        {
            var profile = await _dbContext.IndividualProfiles
                .FirstOrDefaultAsync(p => p.UserId == wallet.IndividualId, cancellationToken)
                .ConfigureAwait(false);

            var fullName = profile != null
                ? $"{profile.FirstName} {profile.LastName}".Trim()
                : "CebizPay User";

            return new WalletResolutionResponseDto(
                WalletId: wallet.Id,
                AccountName: string.IsNullOrWhiteSpace(fullName) ? "CebizPay User" : fullName,
                AccountType: "Individual",
                Currency: wallet.Currency.ToString(),
                Status: wallet.Status.ToString(),
                PhoneNumber: null,
                Email: null);
        }

        return new WalletResolutionResponseDto(
            WalletId: wallet.Id,
            AccountName: "CebizPay Wallet",
            AccountType: "Individual",
            Currency: wallet.Currency.ToString(),
            Status: wallet.Status.ToString(),
            PhoneNumber: null,
            Email: null);
    }
}
