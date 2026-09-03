import React from 'react';
import { Link } from 'react-router-dom';
import { ROUTES } from '../../constants/routes';
import Card from '../common/Card';
import Table from '../tables/Table';
import TableHeader from '../tables/TableHeader';
import TableRow from '../tables/TableRow';
import Badge from '../common/Badge';
import Skeleton from '../common/Skeleton';
import EmptyState from '../feedback/EmptyState';
import ErrorState from '../feedback/ErrorState';
import { ArrowDownLeft, ArrowUpRight, Receipt, ChevronRight } from 'lucide-react';

/**
 * Recent transactions ledger table.
 */
export default function RecentTransactions({
  transactions = [],
  loading = false,
  error = null,
  onRetry,
  onViewTransaction,
  className = ''
}) {
  const getStatusBadge = (status) => {
    const s = (status || '').toLowerCase();
    if (s === 'completed' || s === 'success' || s === 'settled') {
      return <Badge variant="success" dot={true}>Completed</Badge>;
    }
    if (s === 'pending' || s === 'processing') {
      return <Badge variant="warning" dot={true}>Pending</Badge>;
    }
    if (s === 'failed' || s === 'rejected' || s === 'reversed') {
      return <Badge variant="danger" dot={true}>Failed</Badge>;
    }
    return <Badge variant="neutral">{status || 'Unknown'}</Badge>;
  };

  const formatAmount = (amount, isCredit) => {
    const formatted = new Intl.NumberFormat('en-NG', {
      style: 'currency',
      currency: 'NGN',
      minimumFractionDigits: 2
    }).format(Math.abs(amount || 0));

    return (
      <span className={`font-bold ${isCredit ? 'text-status-success' : 'text-slate-900'}`}>
        {isCredit ? `+${formatted}` : `-${formatted}`}
      </span>
    );
  };

  const formatDate = (dateString) => {
    if (!dateString) return '—';
    try {
      const date = new Date(dateString);
      return new Intl.DateTimeFormat('en-GB', {
        day: 'numeric',
        month: 'short',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
      }).format(date);
    } catch {
      return dateString;
    }
  };

  return (
    <Card padding="p-0" className={`overflow-hidden ${className}`}>
      {/* Card Header */}
      <div className="px-6 py-4 border-b border-slate-100 flex items-center justify-between">
        <div>
          <h3 className="text-sm font-bold text-slate-900">Recent Transactions</h3>
          <p className="text-xs text-slate-500 mt-0.5">Real-time ledger entries and settlement records</p>
        </div>
        <Link
          to={ROUTES.WALLET}
          className="inline-flex items-center gap-1 text-xs font-semibold text-brand-600 hover:text-brand-700 hover:underline"
        >
          <span>View All in Wallet</span>
          <ChevronRight size={14} />
        </Link>
      </div>

      {/* Loading State */}
      {loading && (
        <div className="p-6 space-y-3">
          <Skeleton variant="table-row" count={5} />
        </div>
      )}

      {/* Error State */}
      {!loading && error && (
        <div className="p-6">
          <ErrorState
            title="Failed to load recent transactions"
            message={error.message || 'Unable to retrieve ledger history.'}
            onRetry={onRetry}
          />
        </div>
      )}

      {/* Empty State */}
      {!loading && !error && transactions.length === 0 && (
        <div className="p-6">
          <EmptyState
            icon={Receipt}
            title="No transactions yet"
            description="Your recent wallet inflows, payouts, and transfers will appear here."
          />
        </div>
      )}

      {/* Transaction Table */}
      {!loading && !error && transactions.length > 0 && (
        <Table>
          <TableHeader
            columns={[
              { label: 'Transaction / Description' },
              { label: 'Type' },
              { label: 'Amount' },
              { label: 'Date & Time' },
              { label: 'Status' },
              { label: 'Action', align: 'right' }
            ]}
          />
          <tbody>
            {transactions.map((tx) => {
              const isCredit = tx.type === 'Credit' || tx.direction === 'Inflow' || (tx.amount > 0 && !tx.isDebit);
              const txId = tx.id || tx.transactionId || tx.reference;

              return (
                <TableRow key={txId} onClick={() => onViewTransaction && onViewTransaction(tx)}>
                  <td className="py-3.5 px-4 font-semibold text-xs text-slate-900">
                    <div className="flex items-center gap-2.5">
                      <div className={`w-7 h-7 rounded-full flex items-center justify-center shrink-0 ${
                        isCredit ? 'bg-status-success-bg text-status-success' : 'bg-slate-100 text-slate-600'
                      }`}>
                        {isCredit ? <ArrowDownLeft size={14} /> : <ArrowUpRight size={14} />}
                      </div>
                      <div className="min-w-0">
                        <div className="truncate font-medium">{tx.description || tx.reference || 'Wallet Operation'}</div>
                        {tx.reference && <div className="text-[10px] text-slate-400 font-mono truncate">{tx.reference}</div>}
                      </div>
                    </div>
                  </td>

                  <td className="py-3.5 px-4 text-xs font-medium text-slate-600">
                    {tx.type || (isCredit ? 'Inflow' : 'Outflow')}
                  </td>

                  <td className="py-3.5 px-4 text-xs font-mono">
                    {formatAmount(tx.amount, isCredit)}
                  </td>

                  <td className="py-3.5 px-4 text-xs text-slate-500 whitespace-nowrap">
                    {formatDate(tx.createdAt || tx.timestamp || tx.date)}
                  </td>

                  <td className="py-3.5 px-4">
                    {getStatusBadge(tx.status)}
                  </td>

                  <td className="py-3.5 px-4 text-right">
                    <button
                      type="button"
                      onClick={(e) => {
                        e.stopPropagation();
                        onViewTransaction && onViewTransaction(tx);
                      }}
                      className="text-xs font-semibold text-brand-600 hover:underline"
                    >
                      Details
                    </button>
                  </td>
                </TableRow>
              );
            })}
          </tbody>
        </Table>
      )}
    </Card>
  );
}
