using System.Globalization;
using System.Text;
using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Finance.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Wallet;

/// <summary>
/// Query to export organization wallet transactions dataset as a downloadable CSV stream.
/// </summary>
public sealed record ExportOrgWalletTransactionsQuery(
    Guid OrganizationId,
    string? Search = null,
    string? Type = null,
    string? Status = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null) : IRequest<ExportOrgWalletTransactionsResult>;

/// <summary>
/// Result containing CSV file stream bytes and metadata.
/// </summary>
public sealed record ExportOrgWalletTransactionsResult(
    byte[] Content,
    string ContentType,
    string FileName);

/// <summary>
/// Validator for <see cref="ExportOrgWalletTransactionsQuery"/>.
/// </summary>
public sealed class ExportOrgWalletTransactionsQueryValidator : AbstractValidator<ExportOrgWalletTransactionsQuery>
{
    /// <summary>
    /// Initializes validation rules.
    /// </summary>
    public ExportOrgWalletTransactionsQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
    }
}

/// <summary>
/// Handler for <see cref="ExportOrgWalletTransactionsQuery"/>.
/// </summary>
public sealed class ExportOrgWalletTransactionsQueryHandler : IRequestHandler<ExportOrgWalletTransactionsQuery, ExportOrgWalletTransactionsResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="ExportOrgWalletTransactionsQueryHandler"/>.
    /// </summary>
    public ExportOrgWalletTransactionsQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    /// <inheritdoc/>
    public async Task<ExportOrgWalletTransactionsResult> Handle(
        ExportOrgWalletTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken).ConfigureAwait(false);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        const string header = "Transaction ID,Reference,Date (UTC),Type,Direction,Amount (NGN),Status,Counterparty,Description";

        // 1. Resolve organization wallet
        var wallet = await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.OrganizationId == request.OrganizationId && w.Currency == Currency.NGN, cancellationToken)
            .ConfigureAwait(false);

        if (wallet == null)
        {
            return CreateEmptyCsv(header);
        }

        // 2. Resolve associated ledger account
        var ledgerAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.WalletId == wallet.Id, cancellationToken)
            .ConfigureAwait(false);

        if (ledgerAccount == null)
        {
            return CreateEmptyCsv(header);
        }

        // 3. Query ledger entries
        var entriesQuery = _dbContext.LedgerEntries
            .Where(e => e.LedgerAccountId == ledgerAccount.Id);

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

        if (request.FromUtc.HasValue)
        {
            entriesQuery = entriesQuery.Where(e => e.CreatedAtUtc >= request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            entriesQuery = entriesQuery.Where(e => e.CreatedAtUtc <= request.ToUtc.Value);
        }

        var entries = await entriesQuery
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (entries.Count == 0)
        {
            return CreateEmptyCsv(header);
        }

        var txnIds = entries.Select(e => e.LedgerTransactionId).Distinct().ToList();
        var transactions = await _dbContext.LedgerTransactions
            .Where(t => txnIds.Contains(t.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var txMap = transactions.ToDictionary(t => t.Id, t => t);

        // Load sibling counterparty entries
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

        var sb = new StringBuilder();
        sb.AppendLine(header);

        var searchTerm = request.Search?.Trim().ToLowerInvariant();

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

            var reference = tx?.Reference ?? $"REF-{entry.Id.ToString("N")[..8].ToUpperInvariant()}";
            var description = tx?.Description ?? $"{entry.Direction} Transaction";
            var txnType = tx?.TransactionType.ToString() ?? "Transfer";

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var matchesSearch = reference.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                    description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                    (counterparty != null && counterparty.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));

                if (!matchesSearch)
                {
                    continue;
                }
            }

            sb.Append(EscapeCsv(entry.Id.ToString())).Append(',');
            sb.Append(EscapeCsv(reference)).Append(',');
            sb.Append(EscapeCsv(entry.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(EscapeCsv(txnType)).Append(',');
            sb.Append(EscapeCsv(entry.Direction.ToString())).Append(',');
            sb.Append(entry.Amount.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(statusStr)).Append(',');
            sb.Append(EscapeCsv(counterparty ?? string.Empty)).Append(',');
            sb.AppendLine(EscapeCsv(description));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return new ExportOrgWalletTransactionsResult(
            bytes,
            "text/csv; charset=utf-8",
            "org-wallet-transactions.csv");
    }

    private static ExportOrgWalletTransactionsResult CreateEmptyCsv(string header)
    {
        var content = Encoding.UTF8.GetBytes(header + Environment.NewLine);
        return new ExportOrgWalletTransactionsResult(
            content,
            "text/csv; charset=utf-8",
            "org-wallet-transactions.csv");
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',', StringComparison.Ordinal) ||
            value.Contains('"', StringComparison.Ordinal) ||
            value.Contains('\n', StringComparison.Ordinal) ||
            value.Contains('\r', StringComparison.Ordinal))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }
}
