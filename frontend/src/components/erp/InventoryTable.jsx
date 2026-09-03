import React, { useState } from 'react';
import Card from '../common/Card';
import Table from '../tables/Table';
import TableHeader from '../tables/TableHeader';
import TableRow from '../tables/TableRow';
import Badge from '../common/Badge';
import Skeleton from '../common/Skeleton';
import EmptyState from '../feedback/EmptyState';
import ErrorState from '../feedback/ErrorState';
import Pagination from '../tables/Pagination';
import ConfirmModal from '../feedback/ConfirmModal';
import { Package, Sliders, History, Edit2, Trash2, AlertTriangle, ArrowUpDown } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Inventory stock catalog and status table.
 */
export default function InventoryTable({
  items = [],
  loading = false,
  error = null,
  currentPage = 1,
  totalPages = 1,
  onPageChange,
  onRetry,
  onRefresh,
  onEditItem,
  onAdjustStock,
  onViewMovements,
  onAddItem,
  className = ''
}) {
  const { showSuccess, showError } = useToast();
  const [deletingItem, setDeletingItem] = useState(null);
  const [deleteLoading, setDeleteLoading] = useState(false);

  const getStockStatusBadge = (item) => {
    const qty = item.quantityOnHand || 0;
    const reorder = item.reorderLevel || 5;

    if (qty <= 0) {
      return <Badge variant="danger" dot={true}>Out of Stock</Badge>;
    }
    if (qty <= reorder) {
      return <Badge variant="warning" dot={true}>Low Stock ({qty})</Badge>;
    }
    return <Badge variant="success" dot={true}>In Stock ({qty})</Badge>;
  };

  const formatAmount = (amt) => {
    return new Intl.NumberFormat('en-NG', {
      style: 'currency',
      currency: 'NGN',
      minimumFractionDigits: 2
    }).format(amt || 0);
  };

  const handleDelete = async () => {
    if (!deletingItem) return;
    setDeleteLoading(true);
    try {
      await apiClient.delete(`/org/inventory/items/${deletingItem.id}`);
      showSuccess(`Item "${deletingItem.name}" deleted.`);
      setDeletingItem(null);
      if (onRefresh) onRefresh();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      showError(parsed.message || 'Could not delete item.');
    } finally {
      setDeleteLoading(false);
    }
  };

  return (
    <Card padding="p-0" className={`overflow-hidden ${className}`}>
      {loading && (
        <div className="p-6 space-y-3">
          <Skeleton variant="table-row" count={6} />
        </div>
      )}

      {!loading && error && (
        <div className="p-6">
          <ErrorState
            title="Failed to load inventory catalog"
            message={error.message || 'Unable to retrieve inventory stock items.'}
            onRetry={onRetry}
          />
        </div>
      )}

      {!loading && !error && items.length === 0 && (
        <div className="p-8">
          <EmptyState
            icon={Package}
            title="No inventory items found"
            description="Add stock items to your ERP inventory to track quantities, valuation, and sales."
            actionLabel="Add Inventory Item"
            onAction={onAddItem}
          />
        </div>
      )}

      {!loading && !error && items.length > 0 && (
        <>
          <Table>
            <TableHeader
              columns={[
                { label: 'Item & SKU' },
                { label: 'Category' },
                { label: 'Qty On Hand' },
                { label: 'Unit Cost' },
                { label: 'Selling Price' },
                { label: 'Stock Status' },
                { label: 'Actions', align: 'right' }
              ]}
            />
            <tbody>
              {items.map((item) => (
                <TableRow key={item.id}>
                  <td className="py-3 px-4">
                    <div className="flex items-center gap-2.5">
                      <div className="w-8 h-8 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center shrink-0">
                        <Package size={16} />
                      </div>
                      <div className="min-w-0">
                        <div className="font-bold text-xs text-slate-900 truncate">{item.name}</div>
                        <div className="text-[10px] text-slate-400 font-mono">{item.sku || 'SKU'}</div>
                      </div>
                    </div>
                  </td>

                  <td className="py-3 px-4 text-xs text-slate-600 font-medium">
                    {item.category || 'General'}
                  </td>

                  <td className="py-3 px-4 text-xs font-mono font-bold text-slate-900">
                    {item.quantityOnHand || 0} <span className="text-[11px] font-normal text-slate-500">{item.unitOfMeasure || 'pcs'}</span>
                  </td>

                  <td className="py-3 px-4 text-xs font-mono text-slate-600">
                    {formatAmount(item.averageCost)}
                  </td>

                  <td className="py-3 px-4 text-xs font-mono font-bold text-brand-700">
                    {formatAmount(item.sellingPrice)}
                  </td>

                  <td className="py-3 px-4">
                    {getStockStatusBadge(item)}
                  </td>

                  <td className="py-3 px-4 text-right">
                    <div className="flex items-center justify-end gap-1">
                      <button
                        type="button"
                        onClick={() => onAdjustStock && onAdjustStock(item)}
                        className="px-2 py-1 text-xs font-semibold text-brand-600 hover:bg-brand-50 rounded-lg transition"
                        title="Stock Movement"
                      >
                        Adjust
                      </button>

                      <button
                        type="button"
                        onClick={() => onViewMovements && onViewMovements(item)}
                        className="p-1.5 text-slate-400 hover:text-slate-700 hover:bg-slate-100 rounded-lg transition"
                        title="Movements History"
                      >
                        <History size={14} />
                      </button>

                      <button
                        type="button"
                        onClick={() => onEditItem && onEditItem(item)}
                        className="p-1.5 text-slate-400 hover:text-brand-600 hover:bg-slate-100 rounded-lg transition"
                        title="Edit Item"
                      >
                        <Edit2 size={14} />
                      </button>

                      <button
                        type="button"
                        onClick={() => setDeletingItem(item)}
                        className="p-1.5 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition"
                        title="Delete Item"
                      >
                        <Trash2 size={14} />
                      </button>
                    </div>
                  </td>
                </TableRow>
              ))}
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

      {/* Delete Confirmation */}
      {deletingItem && (
        <ConfirmModal
          isOpen={true}
          onClose={() => setDeletingItem(null)}
          onConfirm={handleDelete}
          title="Delete Inventory Item"
          message={`Are you sure you want to delete "${deletingItem.name}" (${deletingItem.sku})?`}
          confirmText="Delete Item"
          confirmVariant="danger"
          loading={deleteLoading}
        />
      )}
    </Card>
  );
}
