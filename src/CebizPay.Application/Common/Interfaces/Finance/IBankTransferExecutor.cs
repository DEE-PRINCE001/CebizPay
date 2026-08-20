using CebizPay.Domain.Finance.Entities;

namespace CebizPay.Application.Common.Interfaces.Finance;

/// <summary>
/// Application abstraction boundary for initiating external bank transfer payout execution.
/// Concrete payment provider implementations (Flutterwave, Paystack, Bank API) belong to Phase 3.
/// </summary>
public interface IBankTransferExecutor
{
    /// <summary>
    /// Enqueues or dispatches a pending bank transfer to external payment execution infrastructure.
    /// </summary>
    Task ExecuteAsync(BankTransfer transfer, CancellationToken cancellationToken = default);
}
