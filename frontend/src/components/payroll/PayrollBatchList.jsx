import React from 'react';
import Card from '../common/Card';
import Table from '../tables/Table';
import TableHeader from '../tables/TableHeader';
import TableRow from '../tables/TableRow';
import Badge from '../common/Badge';
import Skeleton from '../common/Skeleton';
import EmptyState from '../feedback/EmptyState';
import ErrorState from '../feedback/ErrorState';
import Pagination from '../tables/Pagination';
import { Receipt, Calendar, Users, ChevronRight } from 'lucide-react';

/**
 * Payroll batches execution history table.
 */
export default function PayrollBatchList({
  batches = [],
  loading = false,
  error = null,
  currentPage = 1,
  totalPages = 1,
  onPageChange,
  onRetry,
  onViewBatch,
  onRunPayroll,
  className = ''
}) {
  const getStatusBadge = (status) => {
    const s = (status || '').toLowerCase();
    if (s === 'completed' || s === 'success') {
      return <Badge variant="success" dot={true}>Completed</Badge>;
    }
    if (s === 'processing' || s === 'running') {
      return <Badge variant="brand" dot={true}>Processing</Badge>;
    }
    if (s === 'pending') {
      return <Badge variant="warning" dot={true}>Pending</Badge>;
    }
    if (s === 'failed') {
      return <Badge variant="danger" dot={true}>Failed</Badge>;
    }
    if (s === 'cancelled') {
      return <Badge variant="neutral">Cancelled</Badge>;
    }
    return <Badge variant="neutral">{status || 'Settled'}</Badge>;
  };

  const formatAmount = (amt) => {
    return new Intl.NumberFormat('en-NG', {
      style: 'currency',
      currency: 'NGN',
      minimumFractionDigits: 2
    }).format(amt || 0);
  };

  const formatDate = (dateString) => {
    if (!dateString) return '—';
    try {
      const date = new Date(dateString);
      return new Intl.DateTimeFormat('en-GB', {
        day: 'numeric',
        month: 'short',
        year: 'numeric'
      }).format(date);
    } catch {
      return dateString;
    }
  };

  return (
    <Card padding="p-0" className={`overflow-hidden ${className}`}>
      <div className="px-6 py-4 border-b border-slate-100 flex items-center justify-between">
        <div>
          <h3 className="text-sm font-bold text-slate-900">Payroll Execution Runs</h3>
          <p className="text-xs text-slate-500 mt-0.5">Historical batch runs and salary disbursal vouchers</p>
        </div>
      </div>

      {loading && (
        <div className="p-6 space-y-3">
          <Skeleton variant="table-row" count={5} />
        </div>
      )}

      {!loading && error && (
        <div className="p-6">
          <ErrorState
            title="Failed to load payroll batches"
            message={error.message || 'Unable to retrieve payroll records.'}
            onRetry={onRetry}
          />
        </div>
      )}

      {!loading && !error && batches.length === 0 && (
        <div className="p-8">
          <EmptyState
            icon={Receipt}
            title="No payroll batches found"
            description="Run your first corporate payroll batch to automate salary disbursements."
            actionLabel="Run Payroll"
            onAction={onRunPayroll}
          />
        </div>
      )}

      {!loading && !error && batches.length > 0 && (
        <>
          <Table>
            <TableHeader
              columns={[
                { label: 'Batch / Pay Period' },
                { label: 'Staff Count' },
                { label: 'Total Net Disbursal' },
                { label: 'Date Executed' },
                { label: 'Status' },
                { label: 'Action', align: 'right' }
              ]}
            />
            <tbody>
              {batches.map((batch) => {
                const batchId = batch.batchId || batch.id;
                return (
                  <TableRow key={batchId} onClick={() => onViewBatch && onViewBatch(batchId)}>
                    <td className="py-3.5 px-4 font-semibold text-xs text-slate-900">
                      <div className="flex items-center gap-2.5">
                        <div className="w-8 h-8 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center shrink-0">
                          <Receipt size={16} />
                        </div>
                        <div>
                          <div className="font-bold text-slate-900">
                            {batch.periodName || `Payroll Run — ${formatDate(batch.periodStart)}`}
                          </div>
                          <div className="text-[10px] text-slate-400 font-mono">{batchId}</div>
                        </div>
                      </div>
                    </td>

                    <td className="py-3.5 px-4 text-xs font-medium text-slate-700">
                      <div className="flex items-center gap-1.5">
                        <Users size={13} className="text-slate-400" />
                        <span>{batch.staffCount || batch.totalEmployees || 0} Staff</span>
                      </div>
                    </td>

                    <td className="py-3.5 px-4 text-xs font-mono font-bold text-slate-900">
                      {formatAmount(batch.totalNetPay || batch.totalAmount || batch.amount)}
                    </td>

                    <td className="py-3.5 px-4 text-xs text-slate-500 whitespace-nowrap">
                      {formatDate(batch.executedAtUtc || batch.createdAt || batch.date)}
                    </td>

                    <td className="py-3.5 px-4">
                      {getStatusBadge(batch.status)}
                    </td>

                    <td className="py-3.5 px-4 text-right">
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation();
                          onViewBatch && onViewBatch(batchId);
                        }}
                        className="text-xs font-semibold text-brand-600 hover:underline"
                      >
                        Inspect
                      </button>
                    </td>
                  </TableRow>
                );
              })}
            </tbody>
          </Table>

          {totalPages > 1 && (
            <div className="p-4 border-t border-slate-100">
              <Pagination
                currentPage={currentPage}
                totalPages={totalPages}
                onPageChange={onPageChange}
                hasNextPage={currentPage < totalPages}
                hasPrevPage={currentPage > 1}
              />
            </div>
          )}
        </>
      )}
    </Card>
  );
}
