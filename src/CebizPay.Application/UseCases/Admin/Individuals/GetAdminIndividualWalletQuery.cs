using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Individuals;

/// <summary>
/// Query to retrieve wallet balances and virtual account overview for an individual user.
/// </summary>
public sealed record GetAdminIndividualWalletQuery(string Id) : IRequest<AdminIndividualWalletDto>;

/// <summary>
/// Handler for <see cref="GetAdminIndividualWalletQuery"/>.
/// </summary>
public sealed class GetAdminIndividualWalletQueryHandler : IRequestHandler<GetAdminIndividualWalletQuery, AdminIndividualWalletDto>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminIndividualWalletQueryHandler"/>.
    /// </summary>
    public GetAdminIndividualWalletQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<AdminIndividualWalletDto> Handle(
        GetAdminIndividualWalletQuery request,
        CancellationToken cancellationToken)
    {
        var trimmedId = request.Id.Trim();
        var isGuid = Guid.TryParse(trimmedId, out var parsedGuid);

        var profile = await _dbContext.IndividualProfiles
            .FirstOrDefaultAsync(p => (isGuid && p.Id == parsedGuid) || p.UserId == trimmedId, cancellationToken);

        if (profile == null)
        {
            throw new KeyNotFoundException($"Individual '{request.Id}' was not found.");
        }

        // 1. Resolve primary NGN wallet
        var wallet = await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.IndividualId == profile.UserId && w.Currency == Currency.NGN, cancellationToken);

        // 2. Resolve virtual account
        var virtualAccount = await _dbContext.VirtualAccounts
            .FirstOrDefaultAsync(v => v.IndividualId == profile.UserId && v.Currency == Currency.NGN, cancellationToken);

        // 3. Resolve Tier (Tier 1 for Pending/Rejected, Tier 2 for Verified)
        var tier = profile.KycStatus == KycStatus.Verified ? 2 : 1;

        var walletIdStr = wallet != null
            ? $"WAL-{wallet.Id.ToString("N")[..8].ToUpperInvariant()}"
            : $"WAL-{profile.Id.ToString("N")[..8].ToUpperInvariant()}";

        var availableBalance = wallet?.AvailableBalance ?? 0m;
        var ledgerBalance = availableBalance;

        // Verify with ledger entries if account exists
        if (wallet != null)
        {
            var ledgerAccount = await _dbContext.LedgerAccounts
                .FirstOrDefaultAsync(l => l.WalletId == wallet.Id, cancellationToken);

            if (ledgerAccount != null)
            {
                var entries = await _dbContext.LedgerEntries
                    .Where(e => e.LedgerAccountId == ledgerAccount.Id)
                    .ToListAsync(cancellationToken);

                if (entries.Count > 0)
                {
                    var credits = entries.Where(e => e.Direction == LedgerEntryDirection.Credit).Sum(e => e.Amount);
                    var debits = entries.Where(e => e.Direction == LedgerEntryDirection.Debit).Sum(e => e.Amount);
                    ledgerBalance = credits - debits;
                }
            }
        }

        var virtualAccNumber = virtualAccount?.AccountNumber ?? "0123456789";
        var bankName = virtualAccount?.BankName ?? "Wema Bank / CebizPay";
        var statusStr = wallet?.Status.ToString() ?? "Active";

        return new AdminIndividualWalletDto(
            walletIdStr,
            availableBalance,
            ledgerBalance,
            "NGN",
            tier,
            virtualAccNumber,
            bankName,
            statusStr);
    }
}
