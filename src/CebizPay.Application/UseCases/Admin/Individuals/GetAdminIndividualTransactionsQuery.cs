using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Individuals;

/// <summary>
/// Query to retrieve paginated transaction history for an individual user from the administrative perspective.
/// </summary>
public sealed record GetAdminIndividualTransactionsQuery(
    string Id,
    int PageNumber = 1,
    int PageSize = 10,
    string? Search = null,
    string? Type = null,
    string? Status = null) : IRequest<PagedResult<AdminIndividualTransactionItemDto>>;

/// <summary>
/// Validator for <see cref="GetAdminIndividualTransactionsQuery"/>.
/// </summary>
public sealed class GetAdminIndividualTransactionsQueryValidator : AbstractValidator<GetAdminIndividualTransactionsQuery>
{
    /// <summary>
    /// Initializes validation rules.
    /// </summary>
    public GetAdminIndividualTransactionsQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Individual identifier is required.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for <see cref="GetAdminIndividualTransactionsQuery"/>.
/// </summary>
public sealed class GetAdminIndividualTransactionsQueryHandler : IRequestHandler<GetAdminIndividualTransactionsQuery, PagedResult<AdminIndividualTransactionItemDto>>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminIndividualTransactionsQueryHandler"/>.
    /// </summary>
    public GetAdminIndividualTransactionsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AdminIndividualTransactionItemDto>> Handle(
        GetAdminIndividualTransactionsQuery request,
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

        // 1. Resolve Wallet
        var wallet = await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.IndividualId == profile.UserId && w.Currency == Currency.NGN, cancellationToken);

        if (wallet == null)
        {
            return new PagedResult<AdminIndividualTransactionItemDto>(
                Array.Empty<AdminIndividualTransactionItemDto>(), 0, request.PageNumber, request.PageSize);
        }

        // 2. Resolve LedgerAccount
        var ledgerAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.WalletId == wallet.Id, cancellationToken);

        if (ledgerAccount == null)
        {
            return new PagedResult<AdminIndividualTransactionItemDto>(
                Array.Empty<AdminIndividualTransactionItemDto>(), 0, request.PageNumber, request.PageSize);
        }

        // 3. Query ledger entries
        var entriesQuery = _dbContext.LedgerEntries
            .Where(e => e.LedgerAccountId == ledgerAccount.Id);

        // Filter by Transaction Type (Send = Debit, Receives = Credit)
        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var typeStr = request.Type.Trim();
            if (typeStr.Equals("Send", StringComparison.OrdinalIgnoreCase))
            {
                entriesQuery = entriesQuery.Where(e => e.Direction == LedgerEntryDirection.Debit);
            }
            else if (typeStr.Equals("Receives", StringComparison.OrdinalIgnoreCase) ||
                     typeStr.Equals("Receive", StringComparison.OrdinalIgnoreCase))
            {
                entriesQuery = entriesQuery.Where(e => e.Direction == LedgerEntryDirection.Credit);
            }
        }

        var totalCount = await entriesQuery.CountAsync(cancellationToken);

        var entries = await entriesQuery
            .OrderByDescending(e => e.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
        {
            return new PagedResult<AdminIndividualTransactionItemDto>(
                Array.Empty<AdminIndividualTransactionItemDto>(), totalCount, request.PageNumber, request.PageSize);
        }

        var txnIds = entries.Select(e => e.LedgerTransactionId).Distinct().ToList();

        // 4. Fetch LedgerTransactions
        var txns = await _dbContext.LedgerTransactions
            .Where(t => txnIds.Contains(t.Id))
            .ToListAsync(cancellationToken);
        var txnMap = txns.ToDictionary(t => t.Id, t => t);

        // 5. Fetch opposing entries to identify counterparty
        var opposingEntries = await _dbContext.LedgerEntries
            .Where(e => txnIds.Contains(e.LedgerTransactionId) && e.LedgerAccountId != ledgerAccount.Id)
            .ToListAsync(cancellationToken);

        var opposingAccountIds = opposingEntries.Select(e => e.LedgerAccountId).Distinct().ToList();
        var opposingAccounts = await _dbContext.LedgerAccounts
            .Where(a => opposingAccountIds.Contains(a.Id))
            .ToListAsync(cancellationToken);
        var opposingAccountMap = opposingAccounts.ToDictionary(a => a.Id, a => a);

        var opposingWalletIds = opposingAccounts
            .Where(a => a.WalletId.HasValue)
            .Select(a => a.WalletId!.Value)
            .Distinct()
            .ToList();

        var opposingWallets = await _dbContext.Wallets
            .Where(w => opposingWalletIds.Contains(w.Id))
            .ToListAsync(cancellationToken);
        var opposingWalletMap = opposingWallets.ToDictionary(w => w.Id, w => w);

        var opposingUserIds = opposingWallets
            .Where(w => !string.IsNullOrEmpty(w.IndividualId))
            .Select(w => w.IndividualId!)
            .Distinct()
            .ToList();

        var opposingProfiles = await _dbContext.IndividualProfiles
            .Where(p => opposingUserIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);
        var opposingProfileMap = opposingProfiles.ToDictionary(p => p.UserId, p => p);

        var opposingOrgIds = opposingWallets
            .Where(w => w.OrganizationId.HasValue)
            .Select(w => w.OrganizationId!.Value)
            .Distinct()
            .ToList();

        var opposingOrgs = await _dbContext.Organizations
            .Where(o => opposingOrgIds.Contains(o.Id))
            .ToListAsync(cancellationToken);
        var opposingOrgMap = opposingOrgs.ToDictionary(o => o.Id, o => o.CompanyName);

        // 6. Fetch BankTransfers initiated from this wallet
        var bankTransfers = await _dbContext.BankTransfers
            .Where(b => b.SenderWalletId == wallet.Id)
            .ToListAsync(cancellationToken);
        var bankTransferMap = bankTransfers.ToDictionary(b => b.Reference, b => b);

        var items = new List<AdminIndividualTransactionItemDto>(entries.Count);

        foreach (var entry in entries)
        {
            if (!txnMap.TryGetValue(entry.LedgerTransactionId, out var txn))
            {
                continue;
            }

            var transactionType = entry.Direction == LedgerEntryDirection.Debit ? "Send" : "Receives";
            var displayStatus = txn.Status switch
            {
                LedgerTransactionStatus.Completed => "Successfull",
                LedgerTransactionStatus.Pending => "Pending",
                LedgerTransactionStatus.Reversed => "Reversed",
                LedgerTransactionStatus.Failed => "Failed",
                _ => txn.Status.ToString()
            };

            // Optional status filter
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                if (!displayStatus.Equals(request.Status.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            // Resolve counterparty details
            var counterpartyEntry = opposingEntries.FirstOrDefault(e => e.LedgerTransactionId == txn.Id);
            string counterpartyName = "CebizPay Settlement";
            string? counterpartyAvatarUrl = null;
            string method = "Wallet ID";
            string accountOrWalletId = wallet.Id.ToString("N")[..12];
            string receiverSenderId = txn.Reference;

            if (counterpartyEntry != null && opposingAccountMap.TryGetValue(counterpartyEntry.LedgerAccountId, out var oppAccount))
            {
                if (oppAccount.WalletId.HasValue && opposingWalletMap.TryGetValue(oppAccount.WalletId.Value, out var oppWallet))
                {
                    if (!string.IsNullOrEmpty(oppWallet.IndividualId) && opposingProfileMap.TryGetValue(oppWallet.IndividualId, out var oppProf))
                    {
                        counterpartyName = $"{oppProf.FirstName} {oppProf.LastName}".Trim();
                        counterpartyAvatarUrl = oppProf.AvatarUrl;
                        method = "Wallet ID";
                        accountOrWalletId = oppWallet.Id.ToString("N")[..12];
                        receiverSenderId = oppProf.UserId;
                    }
                    else if (oppWallet.OrganizationId.HasValue && opposingOrgMap.TryGetValue(oppWallet.OrganizationId.Value, out var orgName))
                    {
                        counterpartyName = orgName;
                        method = "Wallet ID";
                        accountOrWalletId = oppWallet.Id.ToString("N")[..12];
                        receiverSenderId = oppWallet.OrganizationId.Value.ToString();
                    }
                }
            }
            else if (bankTransferMap.TryGetValue(txn.Reference, out var bankTx))
            {
                counterpartyName = bankTx.DestinationAccountName ?? "Bank Transfer";
                method = "Bank Account";
                accountOrWalletId = bankTx.DestinationAccountNumber;
                receiverSenderId = bankTx.DestinationBankCode;
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var s = request.Search.Trim();
                var matches = counterpartyName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                              txn.Reference.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                              accountOrWalletId.Contains(s, StringComparison.OrdinalIgnoreCase);
                if (!matches)
                {
                    continue;
                }
            }

            items.Add(new AdminIndividualTransactionItemDto(
                txn.Reference,
                counterpartyName,
                counterpartyAvatarUrl,
                entry.Amount,
                transactionType,
                receiverSenderId,
                method,
                accountOrWalletId,
                txn.CreatedAtUtc,
                displayStatus));
        }

        return new PagedResult<AdminIndividualTransactionItemDto>(
            items, totalCount, request.PageNumber, request.PageSize);
    }
}
