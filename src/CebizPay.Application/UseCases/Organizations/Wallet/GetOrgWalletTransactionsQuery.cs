using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Finance.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Wallet;

/// <summary>
/// Query to retrieve paginated transaction history for an organization's corporate wallet.
/// </summary>
public sealed record GetOrgWalletTransactionsQuery(
    Guid OrganizationId,
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null,
    string? Type = null,
    string? Status = null) : IRequest<PagedResult<OrgWalletTransactionItemDto>>;

/// <summary>
/// Validator for <see cref="GetOrgWalletTransactionsQuery"/>.
/// </summary>
public sealed class GetOrgWalletTransactionsQueryValidator : AbstractValidator<GetOrgWalletTransactionsQuery>
{
    /// <summary>
    /// Initializes validation rules.
    /// </summary>
    public GetOrgWalletTransactionsQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for <see cref="GetOrgWalletTransactionsQuery"/>.
/// </summary>
public sealed class GetOrgWalletTransactionsQueryHandler : IRequestHandler<GetOrgWalletTransactionsQuery, PagedResult<OrgWalletTransactionItemDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetOrgWalletTransactionsQueryHandler"/>.
    /// </summary>
    public GetOrgWalletTransactionsQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<OrgWalletTransactionItemDto>> Handle(
        GetOrgWalletTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken).ConfigureAwait(false);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        // 1. Resolve organization wallet
        var wallet = await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.OrganizationId == request.OrganizationId && w.Currency == Currency.NGN, cancellationToken)
            .ConfigureAwait(false);

        if (wallet == null)
        {
            return new PagedResult<OrgWalletTransactionItemDto>(
                Array.Empty<OrgWalletTransactionItemDto>(), 0, request.PageNumber, request.PageSize);
        }

        // 2. Resolve associated LedgerAccount
        var ledgerAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.WalletId == wallet.Id, cancellationToken)
            .ConfigureAwait(false);

        if (ledgerAccount == null)
        {
            return new PagedResult<OrgWalletTransactionItemDto>(
                Array.Empty<OrgWalletTransactionItemDto>(), 0, request.PageNumber, request.PageSize);
        }

        // 3. Query ledger entries
        var entriesQuery = _dbContext.LedgerEntries
            .Where(e => e.LedgerAccountId == ledgerAccount.Id);

        // Filter by direction: Send / Debit vs Receive / Credit
        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var typeStr = request.Type.Trim();
            if (typeStr.Equals("Debit", StringComparison.OrdinalIgnoreCase) ||
                typeStr.Equals("Send", StringComparison.OrdinalIgnoreCase) ||
                typeStr.Equals("Outbound", StringComparison.OrdinalIgnoreCase))
            {
                entriesQuery = entriesQuery.Where(e => e.Direction == LedgerEntryDirection.Debit);
            }
            else if (typeStr.Equals("Credit", StringComparison.OrdinalIgnoreCase) ||
                     typeStr.Equals("Receive", StringComparison.OrdinalIgnoreCase) ||
                     typeStr.Equals("Inbound", StringComparison.OrdinalIgnoreCase))
            {
                entriesQuery = entriesQuery.Where(e => e.Direction == LedgerEntryDirection.Credit);
            }
        }

        var totalCount = await entriesQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var entries = await entriesQuery
            .OrderByDescending(e => e.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (entries.Count == 0)
        {
            return new PagedResult<OrgWalletTransactionItemDto>(
                Array.Empty<OrgWalletTransactionItemDto>(), totalCount, request.PageNumber, request.PageSize);
        }

        var txnIds = entries.Select(e => e.LedgerTransactionId).Distinct().ToList();
        var transactions = await _dbContext.LedgerTransactions
            .Where(t => txnIds.Contains(t.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var txMap = transactions.ToDictionary(t => t.Id, t => t);

        // Load all sibling counterparty entries for these transactions
        var siblingEntries = await _dbContext.LedgerEntries
            .Where(e => txnIds.Contains(e.LedgerTransactionId) && e.LedgerAccountId != ledgerAccount.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var siblingAccountIds = siblingEntries.Select(e => e.LedgerAccountId).Distinct().ToList();
        var siblingAccounts = await _dbContext.LedgerAccounts
            .Where(a => siblingAccountIds.Contains(a.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var siblingAccountMap = siblingAccounts.ToDictionary(a => a.Id, a => a);
        var siblingByTxn = siblingEntries.GroupBy(e => e.LedgerTransactionId)
            .ToDictionary(g => g.Key, g => g.First());

        var items = new List<OrgWalletTransactionItemDto>();

        foreach (var entry in entries)
        {
            txMap.TryGetValue(entry.LedgerTransactionId, out var tx);

            string? counterparty = null;
            if (siblingByTxn.TryGetValue(entry.LedgerTransactionId, out var sib) &&
                siblingAccountMap.TryGetValue(sib.LedgerAccountId, out var sibAcc))
            {
                counterparty = sibAcc.AccountType.ToString();
            }

            var statusStr = tx?.Status.ToString() ?? "Completed";
            if (!string.IsNullOrWhiteSpace(request.Status) &&
                !statusStr.Equals(request.Status.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            items.Add(new OrgWalletTransactionItemDto(
                Id: entry.Id,
                Amount: entry.Amount,
                Direction: entry.Direction.ToString(),
                TransactionType: tx?.TransactionType.ToString() ?? "Transfer",
                Reference: tx?.Reference ?? $"REF-{entry.Id.ToString("N")[..8].ToUpperInvariant()}",
                Description: tx?.Description ?? $"{entry.Direction} Transaction",
                Counterparty: counterparty,
                Status: statusStr,
                TimestampUtc: tx?.CreatedAtUtc ?? entry.CreatedAtUtc));
        }

        return new PagedResult<OrgWalletTransactionItemDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
