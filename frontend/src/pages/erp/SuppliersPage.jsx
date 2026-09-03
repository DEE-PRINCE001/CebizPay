import React, { useState } from 'react';
import ErpLayout from '../../layouts/ErpLayout';
import AddSupplierModal from '../../components/erp/AddSupplierModal';

import Table from '../../components/tables/Table';
import TableHeader from '../../components/tables/TableHeader';
import TableRow from '../../components/tables/TableRow';
import Pagination from '../../components/tables/Pagination';
import TableExport from '../../components/tables/TableExport';
import Card from '../../components/common/Card';
import Badge from '../../components/common/Badge';
import StatCard from '../../components/common/StatCard';
import Button from '../../components/common/Button';
import SearchInput from '../../components/forms/SearchInput';
import Skeleton from '../../components/common/Skeleton';
import EmptyState from '../../components/feedback/EmptyState';
import ErrorState from '../../components/feedback/ErrorState';
import ConfirmModal from '../../components/feedback/ConfirmModal';

import { Truck, Plus, Mail, Phone, Edit2, Trash2, RefreshCw } from 'lucide-react';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useOrg } from '../../context/OrgContext';
import { useToast } from '../../hooks/useToast';
import apiClient from '../../services/api/client';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Suppliers directory and procurement vendor workspace.
 */
export default function SuppliersPage() {
  const { currentOrgId } = useOrg();
  const { showSuccess, showError } = useToast();

  const [search, setSearch] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 20;

  const [isAddOpen, setIsAddOpen] = useState(false);
  const [editingSupplier, setEditingSupplier] = useState(null);
  const [deletingSupplier, setDeletingSupplier] = useState(null);
  const [deleteLoading, setDeleteLoading] = useState(false);

  const {
    data: suppliersData,
    loading,
    error,
    refetch
  } = useApiQuery(
    () => {
      if (!currentOrgId) return Promise.resolve({ items: [], totalPages: 1, totalCount: 0 });
      return apiClient.get('/org/suppliers', {
        params: {
          search: search.trim() || undefined,
          pageNumber: currentPage,
          pageSize
        }
      });
    },
    { deps: [currentOrgId, search, currentPage] }
  );

  const suppliers = suppliersData?.items || [];
  const totalPages = suppliersData?.totalPages || 1;
  const totalCount = suppliersData?.totalCount || suppliers.length;

  const handleDelete = async () => {
    if (!deletingSupplier) return;
    setDeleteLoading(true);
    try {
      await apiClient.delete(`/org/suppliers/${deletingSupplier.id}`);
      showSuccess(`Supplier "${deletingSupplier.name}" removed.`);
      setDeletingSupplier(null);
      refetch();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      showError(parsed.message || 'Could not delete supplier.');
    } finally {
      setDeleteLoading(false);
    }
  };

  return (
    <ErpLayout
      title="ERP: Suppliers & Vendors"
      subtitle="Vendor directory, procurement records, and tax identifiers"
      headerAction={
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            icon={RefreshCw}
            onClick={refetch}
            className="hidden sm:inline-flex"
          >
            Refresh
          </Button>
          <Button
            variant="primary"
            size="sm"
            icon={Plus}
            onClick={() => setIsAddOpen(true)}
          >
            Add Supplier
          </Button>
        </div>
      }
    >
      <div className="space-y-6">
        {/* Metric Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <StatCard
            icon={Truck}
            label="Registered Vendors"
            value={totalCount.toString()}
            loading={loading}
          />
          <StatCard
            icon={Truck}
            label="Active Supply Lines"
            value={totalCount.toString()}
            loading={loading}
          />
        </div>

        {/* Search & Export Toolbar */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <SearchInput
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onClear={() => setSearch('')}
            placeholder="Search by vendor name, email, or TIN..."
            className="w-full sm:max-w-xs"
          />

          <TableExport
            label="Export"
            onExportCsv={() => {
              const csvContent =
                'data:text/csv;charset=utf-8,' +
                ['Reference,Name,Email,Phone,TIN,Address,Status']
                  .concat(
                    suppliers.map(
                      (s) =>
                        `"${s.reference || ''}","${s.name || ''}","${s.email || ''}","${s.phone || ''}","${s.taxIdentifier || ''}","${s.address || ''}","${s.status || 'Active'}"`
                    )
                  )
                  .join('\n');
              const encodedUri = encodeURI(csvContent);
              const link = document.createElement('a');
              link.setAttribute('href', encodedUri);
              link.setAttribute('download', `suppliers_${new Date().toISOString().slice(0, 10)}.csv`);
              document.body.appendChild(link);
              link.click();
              document.body.removeChild(link);
            }}
          />
        </div>

        {/* Suppliers Table */}
        <Card padding="p-0" className="overflow-hidden">
          {loading && (
            <div className="p-6 space-y-3">
              <Skeleton variant="table-row" count={6} />
            </div>
          )}

          {!loading && error && (
            <div className="p-6">
              <ErrorState
                title="Failed to load supplier directory"
                message={error.message || 'Unable to retrieve vendor accounts.'}
                onRetry={refetch}
              />
            </div>
          )}

          {!loading && !error && suppliers.length === 0 && (
            <div className="p-8">
              <EmptyState
                icon={Truck}
                title="No suppliers registered"
                description="Add registered suppliers to create purchase orders and track deliveries."
                actionLabel="Add Supplier"
                onAction={() => setIsAddOpen(true)}
              />
            </div>
          )}

          {!loading && !error && suppliers.length > 0 && (
            <>
              <Table>
                <TableHeader
                  columns={[
                    { label: 'Vendor & Ref' },
                    { label: 'Contact Details' },
                    { label: 'TIN' },
                    { label: 'Address' },
                    { label: 'Status' },
                    { label: 'Actions', align: 'right' }
                  ]}
                />
                <tbody>
                  {suppliers.map((s) => (
                    <TableRow key={s.id}>
                      <td className="py-3 px-4">
                        <div className="flex items-center gap-2.5">
                          <div className="w-8 h-8 rounded-full bg-purple-50 text-purple-600 flex items-center justify-center font-bold text-xs shrink-0">
                            {(s.name || 'S')[0].toUpperCase()}
                          </div>
                          <div>
                            <div className="font-bold text-xs text-slate-900">{s.name}</div>
                            <div className="text-[10px] text-slate-400 font-mono">{s.reference || 'SUPP'}</div>
                          </div>
                        </div>
                      </td>

                      <td className="py-3 px-4 text-xs text-slate-600 space-y-0.5">
                        {s.email && <div className="text-slate-700">{s.email}</div>}
                        {s.phone && <div className="text-slate-400 text-[11px]">{s.phone}</div>}
                      </td>

                      <td className="py-3 px-4 text-xs font-mono text-slate-600">
                        {s.taxIdentifier || '—'}
                      </td>

                      <td className="py-3 px-4 text-xs text-slate-500">
                        <div className="truncate max-w-xs">{s.address || '—'}</div>
                      </td>

                      <td className="py-3 px-4">
                        <Badge variant={s.status === 'Inactive' ? 'neutral' : 'success'} dot={true}>
                          {s.status || 'Active'}
                        </Badge>
                      </td>

                      <td className="py-3 px-4 text-right">
                        <div className="flex items-center justify-end gap-1">
                          <button
                            type="button"
                            onClick={() => setEditingSupplier(s)}
                            className="p-1.5 text-slate-400 hover:text-brand-600 hover:bg-slate-100 rounded-lg transition"
                          >
                            <Edit2 size={14} />
                          </button>
                          <button
                            type="button"
                            onClick={() => setDeletingSupplier(s)}
                            className="p-1.5 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition"
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
                    onPageChange={(p) => setCurrentPage(p)}
                    hasNextPage={currentPage < totalPages}
                    hasPrevPage={currentPage > 1}
                  />
                </div>
              )}
            </>
          )}
        </Card>
      </div>

      <AddSupplierModal
        isOpen={isAddOpen || !!editingSupplier}
        onClose={() => {
          setIsAddOpen(false);
          setEditingSupplier(null);
        }}
        editingSupplier={editingSupplier}
        onSuccess={refetch}
      />

      {deletingSupplier && (
        <ConfirmModal
          isOpen={true}
          onClose={() => setDeletingSupplier(null)}
          onConfirm={handleDelete}
          title="Delete Supplier Account"
          message={`Are you sure you want to remove "${deletingSupplier.name}" from your suppliers directory?`}
          confirmText="Delete Supplier"
          confirmVariant="danger"
          loading={deleteLoading}
        />
      )}
    </ErpLayout>
  );
}
