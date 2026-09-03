import React, { useState } from 'react';
import CustomerLayout from '../../layouts/CustomerLayout';
import QuickTransferModal from '../../components/dashboard/QuickTransferModal';
import TransactionReceiptDrawer from '../../components/wallet/TransactionReceiptDrawer';

import Card from '../../components/common/Card';
import Table from '../../components/tables/Table';
import TableHeader from '../../components/tables/TableHeader';
import TableRow from '../../components/tables/TableRow';
import TableFilter from '../../components/tables/TableFilter';
import TableExport from '../../components/tables/TableExport';
import Pagination from '../../components/tables/Pagination';
import SearchInput from '../../components/forms/SearchInput';
import Badge from '../../components/common/Badge';
import Skeleton from '../../components/common/Skeleton';
import EmptyState from '../../components/feedback/EmptyState';
import ErrorState from '../../components/feedback/ErrorState';
import Button from '../../components/common/Button';

import { useApiQuery } from '../../hooks/useApiQuery';
import { useOrg } from '../../context/OrgContext';
import apiClient from '../../services/api/client';
import {
  ArrowDownLeft,
  ArrowUpRight,
  Send,
  RefreshCw,
  ArrowRightLeft
} from 'lucide-react';

/**
 * Payouts and transfers management view.
 */
export default function TransfersPage() {
  const { currentOrgId } = useOrg();

  const [searchQuery, setSearchQuery] = useState('');
  const [selectedFilter, setSelectedFilter] = useState([]);
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 15;

  const [isTransferOpen, setIsTransferOpen] = useState(false);
  const [selectedTransaction, setSelectedTransaction] = useState(null);

  const {
    data: settlementData,
    loading: txLoading,
    error: txError,
    refetch: refetchTxs
  } = useApiQuery(
    () => {
      if (!currentOrgId) return Promise.resolve({ items: [], totalPages: 1, totalCount: 0 });
      return apiClient
        .get('/org/reports/settlements', {
          params: {
            pageNumber: currentPage,
            pageSize
          }
        })
        .catch(() => ({ items: [], totalPages: 1, totalCount: 0 }));
    },
    { deps: [currentOrgId, currentPage] }
  );

  const transactionsList = settlementData?.items || settlementData?.records || [];
  const totalPages = settlementData?.totalPages || 1;

  const filteredTransactions = transactionsList.filter((tx) => {
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      const ref = (tx.reference || '').toLowerCase();
      const desc = (tx.description || '').toLowerCase();
      if (!ref.includes(q) && !desc.includes(q)) return false;
    }

    if (selectedFilter.length > 0) {
      const type = (tx.type || '').toLowerCase();
      const status = (tx.status || '').toLowerCase();
      const matches = selectedFilter.some(
        (f) => type.includes(f.toLowerCase()) || status.includes(f.toLowerCase())
      );
      if (!matches) return false;
    }

    return true;
  });

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

  const formatAmount = (amount, isCredit) => {
    const formatted = new Intl.NumberFormat('en-NG', {
      style: 'currency',
      currency: 'NGN',
      minimumFractionDigits: 2
    }).format(Math.abs(amount || 0));

    return (
      <span className={`font-bold font-mono ${isCredit ? 'text-status-success' : 'text-slate-900'}`}>
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
    <CustomerLayout
      title="Transfers & Payouts"
      subtitle="Peer-to-peer wallet transfers and external commercial bank settlements"
      headerAction={
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            icon={RefreshCw}
            onClick={refetchTxs}
            className="hidden sm:inline-flex"
          >
            Refresh
          </Button>
          <Button
            variant="primary"
            size="sm"
            icon={Send}
            onClick={() => setIsTransferOpen(true)}
          >
            Send Transfer
          </Button>
        </div>
      }
    >
      <div className="space-y-4">
        {/* Search & Export Toolbar */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <SearchInput
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            onClear={() => setSearchQuery('')}
            placeholder="Search transfer history..."
            className="w-full sm:max-w-xs"
          />

          <div className="flex items-center gap-2">
            <TableFilter
              label="Filter"
              options={[
                { value: 'Credit', label: 'Inflow' },
                { value: 'Debit', label: 'Outflow' },
                { value: 'Completed', label: 'Completed' },
                { value: 'Pending', label: 'Pending' },
                { value: 'Failed', label: 'Failed' }
              ]}
              selectedValues={selectedFilter}
              onSelect={(val) => {
                setSelectedFilter((prev) =>
                  prev.includes(val) ? prev.filter((x) => x !== val) : [...prev, val]
                );
              }}
              onReset={() => setSelectedFilter([])}
            />

            <TableExport
              label="Export"
              onExportCsv={() => {
                const csvContent =
                  'data:text/csv;charset=utf-8,' +
                  ['Reference,Description,Type,Amount,Date,Status']
                    .concat(
                      filteredTransactions.map(
                        (t) =>
                          `"${t.reference || ''}","${t.description || ''}","${t.type || ''}",${t.amount || 0},"${t.createdAt || ''}","${t.status || ''}"`
                      )
                    )
                    .join('\n');
                const encodedUri = encodeURI(csvContent);
                const link = document.createElement('a');
                link.setAttribute('href', encodedUri);
                link.setAttribute('download', `transfers_${new Date().toISOString().slice(0, 10)}.csv`);
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
              }}
            />
          </div>
        </div>

        {/* Transactions Table */}
        <Card padding="p-0" className="overflow-hidden">
          {txLoading && (
            <div className="p-6 space-y-3">
              <Skeleton variant="table-row" count={6} />
            </div>
          )}

          {!txLoading && txError && (
            <div className="p-6">
              <ErrorState
                title="Failed to load transfer history"
                message={txError.message || 'Unable to retrieve transfers.'}
                onRetry={refetchTxs}
              />
            </div>
          )}

          {!txLoading && !txError && filteredTransactions.length === 0 && (
            <div className="p-8">
              <EmptyState
                icon={ArrowRightLeft}
                title="No transfers found"
                description="Initiate a peer transfer or commercial bank payout to disburse funds."
                actionLabel="Send Transfer"
                onAction={() => setIsTransferOpen(true)}
              />
            </div>
          )}

          {!txLoading && !txError && filteredTransactions.length > 0 && (
            <>
              <Table>
                <TableHeader
                  columns={[
                    { label: 'Recipient / Narration' },
                    { label: 'Type' },
                    { label: 'Amount' },
                    { label: 'Date & Time' },
                    { label: 'Status' },
                    { label: 'Action', align: 'right' }
                  ]}
                />
                <tbody>
                  {filteredTransactions.map((tx) => {
                    const isCredit =
                      tx.type === 'Credit' ||
                      tx.direction === 'Inflow' ||
                      (tx.amount > 0 && !tx.isDebit);
                    const txId = tx.id || tx.transactionId || tx.reference;

                    return (
                      <TableRow
                        key={txId}
                        onClick={() => setSelectedTransaction(tx)}
                      >
                        <td className="py-3.5 px-4 font-semibold text-xs text-slate-900">
                          <div className="flex items-center gap-2.5">
                            <div
                              className={`w-7 h-7 rounded-full flex items-center justify-center shrink-0 ${
                                isCredit
                                  ? 'bg-status-success-bg text-status-success'
                                  : 'bg-slate-100 text-slate-600'
                              }`}
                            >
                              {isCredit ? <ArrowDownLeft size={14} /> : <ArrowUpRight size={14} />}
                            </div>
                            <div className="min-w-0">
                              <div className="truncate font-medium">
                                {tx.description || tx.reference || 'Bank Transfer'}
                              </div>
                              {tx.reference && (
                                <div className="text-[10px] text-slate-400 font-mono truncate">
                                  {tx.reference}
                                </div>
                              )}
                            </div>
                          </div>
                        </td>

                        <td className="py-3.5 px-4 text-xs font-medium text-slate-600">
                          {tx.type || (isCredit ? 'Inflow' : 'Payout')}
                        </td>

                        <td className="py-3.5 px-4 text-xs">
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
                              setSelectedTransaction(tx);
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

              <div className="p-4 border-t border-slate-100">
                <Pagination
                  currentPage={currentPage}
                  totalPages={totalPages}
                  onPageChange={(p) => setCurrentPage(p)}
                  hasNextPage={currentPage < totalPages}
                  hasPrevPage={currentPage > 1}
                />
              </div>
            </>
          )}
        </Card>
      </div>

      <QuickTransferModal
        isOpen={isTransferOpen}
        onClose={() => setIsTransferOpen(false)}
        onSuccess={() => {
          setIsTransferOpen(false);
          refetchTxs();
        }}
      />

      <TransactionReceiptDrawer
        isOpen={!!selectedTransaction}
        onClose={() => setSelectedTransaction(null)}
        transaction={selectedTransaction}
      />
    </CustomerLayout>
  );
}
