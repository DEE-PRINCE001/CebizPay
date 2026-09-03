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
import { Briefcase, Edit2, Trash2 } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Service offerings catalog table.
 */
export default function ServicesTable({
  services = [],
  loading = false,
  error = null,
  currentPage = 1,
  totalPages = 1,
  onPageChange,
  onRetry,
  onRefresh,
  onEditService,
  onAddService,
  className = ''
}) {
  const { showSuccess, showError } = useToast();
  const [deletingService, setDeletingService] = useState(null);
  const [deleteLoading, setDeleteLoading] = useState(false);

  const formatAmount = (amt) => {
    return new Intl.NumberFormat('en-NG', {
      style: 'currency',
      currency: 'NGN',
      minimumFractionDigits: 2
    }).format(amt || 0);
  };

  const handleDelete = async () => {
    if (!deletingService) return;
    setDeleteLoading(true);
    try {
      await apiClient.delete(`/org/services/${deletingService.id}`);
      showSuccess(`Service "${deletingService.name}" removed.`);
      setDeletingService(null);
      if (onRefresh) onRefresh();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      showError(parsed.message || 'Could not delete service.');
    } finally {
      setDeleteLoading(false);
    }
  };

  return (
    <Card padding="p-0" className={`overflow-hidden ${className}`}>
      {loading && (
        <div className="p-6 space-y-3">
          <Skeleton variant="table-row" count={5} />
        </div>
      )}

      {!loading && error && (
        <div className="p-6">
          <ErrorState
            title="Failed to load service catalog"
            message={error.message || 'Unable to retrieve billable service items.'}
            onRetry={onRetry}
          />
        </div>
      )}

      {!loading && !error && services.length === 0 && (
        <div className="p-8">
          <EmptyState
            icon={Briefcase}
            title="No services found"
            description="Add billable service offerings, rate cards, and consulting fees to invoice clients."
            actionLabel="Add Billable Service"
            onAction={onAddService}
          />
        </div>
      )}

      {!loading && !error && services.length > 0 && (
        <>
          <Table>
            <TableHeader
              columns={[
                { label: 'Service & Code' },
                { label: 'Description' },
                { label: 'Billing Rate / Price' },
                { label: 'Status' },
                { label: 'Actions', align: 'right' }
              ]}
            />
            <tbody>
              {services.map((svc) => (
                <TableRow key={svc.id}>
                  <td className="py-3 px-4">
                    <div className="flex items-center gap-2.5">
                      <div className="w-8 h-8 rounded-xl bg-purple-50 text-purple-600 flex items-center justify-center shrink-0">
                        <Briefcase size={16} />
                      </div>
                      <div className="min-w-0">
                        <div className="font-bold text-xs text-slate-900 truncate">{svc.name}</div>
                        <div className="text-[10px] text-slate-400 font-mono">{svc.code || 'SVC'}</div>
                      </div>
                    </div>
                  </td>

                  <td className="py-3 px-4 text-xs text-slate-500">
                    <div className="truncate max-w-xs">{svc.description || 'Standard service offering'}</div>
                  </td>

                  <td className="py-3 px-4 text-xs font-mono font-bold text-brand-700">
                    {formatAmount(svc.unitPrice)}
                  </td>

                  <td className="py-3 px-4">
                    <Badge variant={svc.status === 'Inactive' ? 'neutral' : 'success'} dot={true}>
                      {svc.status || 'Active'}
                    </Badge>
                  </td>

                  <td className="py-3 px-4 text-right">
                    <div className="flex items-center justify-end gap-1">
                      <button
                        type="button"
                        onClick={() => onEditService && onEditService(svc)}
                        className="p-1.5 text-slate-400 hover:text-brand-600 hover:bg-slate-100 rounded-lg transition"
                        title="Edit Service"
                      >
                        <Edit2 size={14} />
                      </button>

                      <button
                        type="button"
                        onClick={() => setDeletingService(svc)}
                        className="p-1.5 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition"
                        title="Delete Service"
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
      {deletingService && (
        <ConfirmModal
          isOpen={true}
          onClose={() => setDeletingService(null)}
          onConfirm={handleDelete}
          title="Delete Service Offering"
          message={`Are you sure you want to delete "${deletingService.name}" (${deletingService.code})?`}
          confirmText="Delete Service"
          confirmVariant="danger"
          loading={deleteLoading}
        />
      )}
    </Card>
  );
}
