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
import { Smartphone, Zap, Wifi, Tv, Receipt } from 'lucide-react';

/**
 * VAS and utility purchase history table.
 */
export default function VasTransactionsTable({
  transactions = [],
  loading = false,
  error = null,
  currentPage = 1,
  totalPages = 1,
  onPageChange,
  onRetry,
  onViewDetails,
  className = ''
}) {
  const getIcon = (type) => {
    const t = (type || '').toLowerCase();
    if (t.includes('airtime')) return <Smartphone size={14} />;
    if (t.includes('data')) return <Wifi size={14} />;
    if (t.includes('electric')) return <Zap size={14} />;
    if (t.includes('cable') || t.includes('tv')) return <Tv size={14} />;
    return <Zap size={14} />;
  };

  const getStatusBadge = (status) => {
    const s = (status || '').toLowerCase();
    if (s === 'completed' || s === 'success' || s === 'settled') {
      return <Badge variant="success" dot={true}>Completed</Badge>;
    }
    if (s === 'pending' || s === 'processing') {
      return <Badge variant="warning" dot={true}>Pending</Badge>;
    }
    if (s === 'failed' || s === 'rejected') {
      return <Badge variant="danger" dot={true}>Failed</Badge>;
    }
    return <Badge variant="neutral">{status || 'Settled'}</Badge>;
  };

  const formatAmount = (amount) => {
    return new Intl.NumberFormat('en-NG', {
      style: 'currency',
      currency: 'NGN',
      minimumFractionDigits: 2
    }).format(Math.abs(amount || 0));
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
      <div className="px-6 py-4 border-b border-slate-100 flex items-center justify-between">
        <div>
          <h3 className="text-sm font-bold text-slate-900">VAS Recharge History</h3>
          <p className="text-xs text-slate-500 mt-0.5">Completed airtime, data, and bill settlement orders</p>
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
            title="Failed to load recharge history"
            message={error.message || 'Unable to retrieve VAS records.'}
            onRetry={onRetry}
          />
        </div>
      )}

      {!loading && !error && transactions.length === 0 && (
        <div className="p-8">
          <EmptyState
            icon={Receipt}
            title="No recharge transactions yet"
            description="Your airtime, data bundle, and utility payment receipts will populate here."
          />
        </div>
      )}

      {!loading && !error && transactions.length > 0 && (
        <>
          <Table>
            <TableHeader
              columns={[
                { label: 'Service / Description' },
                { label: 'Recipient / Account' },
                { label: 'Amount' },
                { label: 'Date & Time' },
                { label: 'Status' },
                { label: 'Action', align: 'right' }
              ]}
            />
            <tbody>
              {transactions.map((tx) => {
                const txId = tx.id || tx.transactionId || tx.reference;
                return (
                  <TableRow key={txId} onClick={() => onViewDetails && onViewDetails(tx)}>
                    <td className="py-3.5 px-4 font-semibold text-xs text-slate-900">
                      <div className="flex items-center gap-2.5">
                        <div className="w-7 h-7 rounded-full bg-brand-50 text-brand-600 flex items-center justify-center shrink-0">
                          {getIcon(tx.type || tx.description)}
                        </div>
                        <div className="min-w-0">
                          <div className="truncate font-medium">{tx.description || tx.service || 'VAS Recharge'}</div>
                          {tx.reference && <div className="text-[10px] text-slate-400 font-mono truncate">{tx.reference}</div>}
                        </div>
                      </div>
                    </td>

                    <td className="py-3.5 px-4 text-xs font-mono text-slate-600">
                      {tx.recipient || tx.phoneNumber || tx.meterNumber || '—'}
                    </td>

                    <td className="py-3.5 px-4 text-xs font-mono font-bold text-slate-900">
                      {formatAmount(tx.amount)}
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
                          onViewDetails && onViewDetails(tx);
                        }}
                        className="text-xs font-semibold text-brand-600 hover:underline"
                      >
                        Receipt
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
