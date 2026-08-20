using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Finance.Events;
using MediatR;

namespace CebizPay.Application.UseCases.Wallet.Transfer;

/// <summary>
/// Handles <see cref="ReverseBankTransferCommand"/>.
/// Reverses a pending, processing, or unknown bank transfer upon definitive failure,
/// restoring funds to sender wallet and updating status to FAILED.
/// </summary>
public sealed class ReverseBankTransferCommandHandler : IRequestHandler<ReverseBankTransferCommand, BankTransferResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILedgerPostingService _ledgerService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="ReverseBankTransferCommandHandler"/>.
    /// </summary>
    public ReverseBankTransferCommandHandler(
        IApplicationDbContext dbContext,
        ILedgerPostingService ledgerService,
        IOutboxService outboxService)
    {
        _dbContext = dbContext;
        _ledgerService = ledgerService;
        _outboxService = outboxService;
    }

    /// <inheritdoc/>
    public async Task<BankTransferResponseDto> Handle(ReverseBankTransferCommand request, CancellationToken cancellationToken)
    {
        var transfer = await _dbContext.BankTransfers
            .FirstOrDefaultAsync(t => t.Id == request.BankTransferId, cancellationToken)
            ?? throw new KeyNotFoundException($"Bank transfer '{request.BankTransferId}' was not found.");

        await using var dbTx = await _dbContext.BeginTransactionAsync(cancellationToken);

        try
        {
            // Execute reversal in ledger posting service (locks sender wallet, restores balance, creates reversal txn & entries, marks transfer FAILED)
            var reversalTxn = await _ledgerService.PostBankTransferReversalCoreAsync(
                request.BankTransferId,
                request.Reason,
                cancellationToken);

            var maskedAccount = transfer.GetMaskedAccountNumber();

            // Create AuditLog
            var auditLog = Domain.Entities.AuditLog.Create(
                actorId: request.InitiatedByUserId,
                action: Domain.Auditing.AuditActions.BankTransferReversed,
                resourceType: Domain.Auditing.AuditResourceTypes.BankTransfer,
                resourceId: transfer.Id.ToString(),
                afterJson: JsonSerializer.Serialize(new
                {
                    BankTransferId = transfer.Id,
                    Reference = transfer.Reference,
                    ReversalReference = reversalTxn.Reference,
                    TotalRefunded = transfer.TotalDebited,
                    Reason = request.Reason,
                    DestinationBankCode = transfer.DestinationBankCode,
                    DestinationAccountNumber = maskedAccount,
                    Status = BankTransferStatus.Failed.ToString()
                }));

            _dbContext.AuditLogs.Add(auditLog);

            // Publish Outbox Events
            var failedEvent = new BankTransferFailedEvent(
                TransferId: transfer.Id,
                TransactionReference: transfer.Reference,
                Reason: request.Reason,
                OccurredOnUtc: DateTime.UtcNow);

            var reversedEvent = new BankTransferReversedEvent(
                TransferId: transfer.Id,
                OriginalTransactionReference: transfer.Reference,
                ReversalTransactionId: reversalTxn.Id,
                ReversalTransactionReference: reversalTxn.Reference,
                Amount: transfer.Amount,
                Currency: transfer.Currency.ToString(),
                FeeAmount: transfer.FeeAmount,
                Reason: request.Reason,
                OccurredOnUtc: DateTime.UtcNow);

            _outboxService.Write(failedEvent);
            _outboxService.Write(reversedEvent);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await dbTx.CommitAsync(cancellationToken);

            return new BankTransferResponseDto(
                TransactionReference: transfer.Reference,
                Status: BankTransferStatus.Failed.ToString().ToUpperInvariant(),
                Amount: transfer.Amount,
                Currency: transfer.Currency.ToString(),
                FeeAmount: transfer.FeeAmount,
                TotalDebited: transfer.TotalDebited,
                DestinationBankCode: transfer.DestinationBankCode,
                DestinationAccountNumber: maskedAccount,
                DestinationAccountName: transfer.DestinationAccountName,
                AppliedFeePolicyVersion: transfer.FeePolicyVersion,
                CreatedAtUtc: transfer.CreatedAtUtc);
        }
        catch
        {
            await dbTx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
