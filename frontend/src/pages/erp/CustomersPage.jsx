import React, { useState } from 'react';
import ErpLayout from '../../layouts/ErpLayout';
import AddCustomerModal from '../../components/erp/AddCustomerModal';

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

import { Users, Plus, Mail, Phone, Edit2, Trash2, RefreshCw } from 'lucide-react';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useOrg } from '../../context/OrgContext';
import { useToast } from '../../hooks/useToast';
import apiClient from '../../services/api/client';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Customers directory and CRM workspace.
 */
export default function CustomersPage() {
  const { currentOrgId } = useOrg();
  const { showSuccess, showError } = useToast();

  const [search, setSearch] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 20;

  const [isAddOpen, setIsAddOpen] = useState(false);
  const [editingCustomer, setEditingCustomer] = useState(null);
  const [deletingCustomer, setDeletingCustomer] = useState(null);
  const [deleteLoading, setDeleteLoading] = useState(false);

  const {
    data: customersData,
    loading,
    error,
    refetch
  } = useApiQuery(
    () => {
      if (!currentOrgId) return Promise.resolve({ items: [], totalPages: 1, totalCount: 0 });
      return apiClient.get('/org/customers', {
        params: {
          search: search.trim() || undefined,
          pageNumber: currentPage,
          pageSize
        }
      });
    },
    { deps: [currentOrgId, search, currentPage] }
  );

  const customers = customersData?.items || [];
  const totalPages = customersData?.totalPages || 1;
  const totalCount = customersData?.totalCount || customers.length;

  const handleDelete = async () => {
    if (!deletingCustomer) return;
    setDeleteLoading(true);
    try {
      await apiClient.delete(`/org/customers/${deletingCustomer.id}`);
      showSuccess(`Customer "${deletingCustomer.name}" removed.`);
      setDeletingCustomer(null);
      refetch();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      showError(parsed.message || 'Could not delete customer.');
    } finally {
      setDeleteLoading(false);
    }
  };

  return (
    <ErpLayout
      title="ERP: Customers Directory"
      subtitle="Client contact database, transaction history, and billing profiles"
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
            Add Customer
          </Button>
        </div>
      }
    >
      <div className="space-y-6">
        {/* Metric Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <StatCard
            icon={Users}
            label="Total Customers Registered"
            value={totalCount.toString()}
            loading={loading}
          />
          <StatCard
            icon={Users}
            label="Active Client Accounts"
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
            placeholder="Search by name, email, or phone..."
            className="w-full sm:max-w-xs"
          />

          <TableExport
            label="Export"
            onExportCsv={() => {
              const csvContent =
                'data:text/csv;charset=utf-8,' +
                ['Reference,Name,Email,Phone,Address,Status']
                  .concat(
                    customers.map(
                      (c) =>
                        `"${c.reference || ''}","${c.name || ''}","${c.email || ''}","${c.phone || ''}","${c.address || ''}","${c.status || 'Active'}"`
                    )
                  )
                  .join('\n');
              const encodedUri = encodeURI(csvContent);
              const link = document.createElement('a');
              link.setAttribute('href', encodedUri);
              link.setAttribute('download', `customers_${new Date().toISOString().slice(0, 10)}.csv`);
              document.body.appendChild(link);
              link.click();
              document.body.removeChild(link);
            }}
          />
        </div>

        {/* Customers Table */}
        <Card padding="p-0" className="overflow-hidden">
          {loading && (
            <div className="p-6 space-y-3">
              <Skeleton variant="table-row" count={6} />
            </div>
          )}

          {!loading && error && (
            <div className="p-6">
              <ErrorState
                title="Failed to load customer directory"
                message={error.message || 'Unable to retrieve client accounts.'}
                onRetry={refetch}
              />
            </div>
          )}

          {!loading && !error && customers.length === 0 && (
            <div className="p-8">
              <EmptyState
                icon={Users}
                title="No customers registered"
                description="Add your first client to issue invoices and track sales orders."
                actionLabel="Add Customer"
                onAction={() => setIsAddOpen(true)}
              />
            </div>
          )}

          {!loading && !error && customers.length > 0 && (
            <>
              <Table>
                <TableHeader
                  columns={[
                    { label: 'Customer & Ref' },
                    { label: 'Contact Details' },
                    { label: 'Billing Address' },
                    { label: 'Status' },
                    { label: 'Actions', align: 'right' }
                  ]}
                />
                <tbody>
                  {customers.map((c) => (
                    <TableRow key={c.id}>
                      <td className="py-3 px-4">
                        <div className="flex items-center gap-2.5">
                          <div className="w-8 h-8 rounded-full bg-brand-50 text-brand-600 flex items-center justify-center font-bold text-xs shrink-0">
                            {(c.name || 'C')[0].toUpperCase()}
                          </div>
                          <div>
                            <div className="font-bold text-xs text-slate-900">{c.name}</div>
                            <div className="text-[10px] text-slate-400 font-mono">{c.reference || 'CUST'}</div>
                          </div>
                        </div>
                      </td>

                      <td className="py-3 px-4 text-xs text-slate-600 space-y-0.5">
                        {c.email && <div className="text-slate-700">{c.email}</div>}
                        {c.phone && <div className="text-slate-400 text-[11px]">{c.phone}</div>}
                      </td>

                      <td className="py-3 px-4 text-xs text-slate-500">
                        <div className="truncate max-w-xs">{c.address || '—'}</div>
                      </td>

                      <td className="py-3 px-4">
                        <Badge variant={c.status === 'Inactive' ? 'neutral' : 'success'} dot={true}>
                          {c.status || 'Active'}
                        </Badge>
                      </td>

                      <td className="py-3 px-4 text-right">
                        <div className="flex items-center justify-end gap-1">
                          <button
                            type="button"
                            onClick={() => setEditingCustomer(c)}
                            className="p-1.5 text-slate-400 hover:text-brand-600 hover:bg-slate-100 rounded-lg transition"
                          >
                            <Edit2 size={14} />
                          </button>
                          <button
                            type="button"
                            onClick={() => setDeletingCustomer(c)}
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

      <AddCustomerModal
        isOpen={isAddOpen || !!editingCustomer}
        onClose={() => {
          setIsAddOpen(false);
          setEditingCustomer(null);
        }}
        editingCustomer={editingCustomer}
        onSuccess={refetch}
      />

      {deletingCustomer && (
        <ConfirmModal
          isOpen={true}
          onClose={() => setDeletingCustomer(null)}
          onConfirm={handleDelete}
          title="Delete Customer Account"
          message={`Are you sure you want to remove "${deletingCustomer.name}" from your customer directory?`}
          confirmText="Delete Customer"
          confirmVariant="danger"
          loading={deleteLoading}
        />
      )}
    </ErpLayout>
  );
}
