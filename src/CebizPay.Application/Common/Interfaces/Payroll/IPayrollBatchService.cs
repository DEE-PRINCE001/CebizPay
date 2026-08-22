using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Application.Common.Interfaces.Payroll;

/// <summary>
/// Service coordinating payroll batch lifecycle, preparation, querying, retries, and voucher maintenance.
/// </summary>
public interface IPayrollBatchService
{
    /// <summary>
    /// Validates organization eligibility, compiles employee items from criteria, snapshots calculation inputs,
    /// and enqueues a new Pending PayrollBatch for asynchronous worker execution.
    /// </summary>
    Task<PayrollBatchDto> CreateAndEnqueueBatchAsync(
        Guid organizationId,
        string initiatorUserId,
        Currency currency,
        DateTime periodStart,
        DateTime periodEnd,
        PayrollSelectionCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves aggregate status and paged item details for a payroll batch.
    /// </summary>
    Task<PayrollBatchProgressDto?> GetBatchProgressAsync(
        Guid organizationId,
        Guid batchId,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-queues all eligible failed items in a batch for worker retry.
    /// </summary>
    Task<int> RetryFailedItemsAsync(
        Guid organizationId,
        Guid batchId,
        string initiatorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a Pending batch before any items have begun financial processing.
    /// </summary>
    Task CancelBatchAsync(
        Guid organizationId,
        Guid batchId,
        string initiatorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an issued payment voucher by ID with strict tenant isolation.
    /// </summary>
    Task<PaymentVoucherDto?> GetPaymentVoucherByIdAsync(
        Guid organizationId,
        Guid voucherId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates non-financial metadata on an issued payment voucher and records an audit log.
    /// </summary>
    Task<PaymentVoucherDto> UpdatePaymentVoucherMetadataAsync(
        Guid organizationId,
        Guid voucherId,
        string initiatorUserId,
        UpdatePaymentVoucherMetadataRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns high-level payroll execution analytics for Super Admin / executive oversight.
    /// </summary>
    Task<PayrollAnalyticsDto> GetOrganizationPayrollAnalyticsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);
}
