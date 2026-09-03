import React, { useState } from 'react';
import Modal from '../common/Modal';
import Badge from '../common/Badge';
import Skeleton from '../common/Skeleton';
import EmptyState from '../feedback/EmptyState';
import ErrorState from '../feedback/ErrorState';
import Table from '../tables/Table';
import TableHeader from '../tables/TableHeader';
import TableRow from '../tables/TableRow';
import Pagination from '../tables/Pagination';
import { History, ArrowDownRight, ArrowUpRight, Sliders } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useApiQuery } from '../../hooks/useApiQuery';

/**
 * Stock movements ledger modal for tracking inventory transactions.
 */
export default function StockMovementsModal({
  isOpen,
  onClose,
  item
}) {
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 15;

  const {
    data: movementsData,
    loading,
    error,
    refetch
  } = useApiQuery(
    () => {
      if (!item?.id) return Promise.resolve({ items: [], totalPages: 1, totalCount: 0 });
      return apiClient.get(`/org/inventory/items/${item.id}/movements`, {
        params: {
          pageNumber: currentPage,
          pageSize
        }
      });
    },
    { deps: [item?.id, currentPage], enabled: isOpen && !!item?.id }
  );

  const movements = movementsData?.items || [];
  const totalPages = movementsData?.totalPages || 1;

  if (!item) return null;

  const getMovementBadge = (type) => {
    const t = (type || '').toLowerCase();
    if (t.includes('in') || t.includes('purchase')) {
      return (
        <Badge variant="success" dot={true}>
          Stock In
        </Badge>
      );
    }
    if (t.includes('out') || t.includes('sale') || t.includes('issue')) {
      return (
        <Badge variant="danger" dot={true}>
          Stock Out
        </Badge>
      );
    }
    return <Badge variant="neutral">Adjustment</Badge>;
  };

  const formatDate = (dateString) => {
    if (!dateString) return '—';
    try {
      return new Intl.DateTimeFormat('en-GB', {
        day: 'numeric',
        month: 'short',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
      }).format(new Date(dateString));
    } catch {
      return dateString;
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Stock Movements Ledger"
      subtitle={`Item: ${item.name} (${item.sku || 'SKU'}) • Total Movements: ${movementsData?.totalCount || movements.length}`}
      maxWidth="max-w-2xl"
    >
      <div className="space-y-4 pt-1">
        {loading && (
          <div className="space-y-2 p-2">
            <Skeleton variant="table-row" count={4} />
          </div>
        )}

        {!loading && error && (
          <ErrorState
            title="Failed to load movements"
            message={error.message || 'Unable to retrieve stock history.'}
            onRetry={refetch}
          />
        )}

        {!loading && !error && movements.length === 0 && (
          <EmptyState
            icon={History}
            title="No movements recorded"
            description="All stock-ins, goods dispatched, and manual count audits will appear here."
          />
        )}

        {!loading && !error && movements.length > 0 && (
          <>
            <div className="border border-slate-200/80 rounded-2xl overflow-hidden">
              <Table>
                <TableHeader
                  columns={[
                    { label: 'Movement' },
                    { label: 'Qty Delta' },
                    { label: 'Unit Cost' },
                    { label: 'Reference / Reason' },
                    { label: 'Date & Time' }
                  ]}
                />
                <tbody>
                  {movements.map((mov) => {
                    const isPositive = mov.quantityDelta > 0 || mov.movementType === 'In';
                    return (
                      <TableRow key={mov.id}>
                        <td className="py-2.5 px-4 text-xs">
                          {getMovementBadge(mov.movementType)}
                        </td>
                        <td className={`py-2.5 px-4 text-xs font-mono font-bold ${isPositive ? 'text-status-success' : 'text-slate-900'}`}>
                          {isPositive ? `+${mov.quantityDelta || mov.quantity}` : `${mov.quantityDelta || mov.quantity}`}
                        </td>
                        <td className="py-2.5 px-4 text-xs font-mono text-slate-700">
                          ₦{(mov.unitCost || 0).toLocaleString()}
                        </td>
                        <td className="py-2.5 px-4 text-xs text-slate-600">
                          <div className="truncate max-w-[150px]">{mov.reason || mov.reference || '—'}</div>
                        </td>
                        <td className="py-2.5 px-4 text-xs text-slate-500 whitespace-nowrap">
                          {formatDate(mov.createdAtUtc || mov.timestamp)}
                        </td>
                      </TableRow>
                    );
                  })}
                </tbody>
              </Table>
            </div>

            {totalPages > 1 && (
              <Pagination
                currentPage={currentPage}
                totalPages={totalPages}
                onPageChange={(p) => setCurrentPage(p)}
                hasNextPage={currentPage < totalPages}
                hasPrevPage={currentPage > 1}
              />
            )}
          </>
        )}
      </div>
    </Modal>
  );
}
