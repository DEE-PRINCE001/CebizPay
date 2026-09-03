import React, { useState } from 'react';
import ErpLayout from '../../layouts/ErpLayout';
import CreateOrderModal from '../../components/erp/CreateOrderModal';

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
import Select from '../../components/forms/Select';
import Skeleton from '../../components/common/Skeleton';
import EmptyState from '../../components/feedback/EmptyState';
import ErrorState from '../../components/feedback/ErrorState';

import { ShoppingCart, Plus, CheckCircle, PackageCheck, DollarSign, RefreshCw } from 'lucide-react';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useOrg } from '../../context/OrgContext';
import { useToast } from '../../hooks/useToast';
import apiClient from '../../services/api/client';
import { parseProblemDetails } from '../../utils/problemDetails';

const SALES_STATUSES = [
  { value: '', label: 'All Orders' },
  { value: 'Draft', label: 'Draft' },
  { value: 'Confirmed', label: 'Confirmed' },
  { value: 'Fulfilled', label: 'Fulfilled' },
  { value: 'PartiallyFulfilled', label: 'Partially Fulfilled' },
  { value: 'Cancelled', label: 'Cancelled' }
];

/**
 * Sales orders and fulfillment workspace.
 */
export default function SalesPage() {
  const { currentOrgId } = useOrg();
  const { showSuccess, showError } = useToast();

  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 20;

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [actionLoading, setActionLoading] = useState(false);

  const {
    data: ordersData,
    loading,
    error,
    refetch
  } = useApiQuery(
    () => {
      if (!currentOrgId) return Promise.resolve({ items: [], totalPages: 1, totalCount: 0 });
      return apiClient.get('/org/orders/sales', {
        params: {
          search: search.trim() || undefined,
          status: status || undefined,
          pageNumber: currentPage,
          pageSize
        }
      });
    },
    { deps: [currentOrgId, search, status, currentPage] }
  );

  const orders = ordersData?.items || [];
  const totalPages = ordersData?.totalPages || 1;
  const totalCount = ordersData?.totalCount || orders.length;

  const totalValue = orders.reduce((acc, o) => acc + (o.totalAmount || 0), 0);
  const confirmedCount = orders.filter((o) => o.status === 'Confirmed').length;

  const handleConfirmOrder = async (orderId) => {
    setActionLoading(true);
    try {
      await apiClient.post(`/org/orders/sales/${orderId}/confirm`);
      showSuccess('Sales order confirmed.');
      refetch();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      showError(parsed.message || 'Failed to confirm order.');
    } finally {
      setActionLoading(false);
    }
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
      return new Intl.DateTimeFormat('en-GB', {
        day: 'numeric',
        month: 'short',
        year: 'numeric'
      }).format(new Date(dateString));
    } catch {
      return dateString;
    }
  };

  const getStatusBadge = (s) => {
    const st = (s || '').toLowerCase();
    if (st === 'fulfilled') return <Badge variant="success" dot={true}>Fulfilled</Badge>;
    if (st === 'confirmed') return <Badge variant="brand" dot={true}>Confirmed</Badge>;
    if (st === 'partiallyfulfilled') return <Badge variant="warning" dot={true}>Partial</Badge>;
    if (st === 'cancelled') return <Badge variant="neutral">Cancelled</Badge>;
    return <Badge variant="neutral">{s || 'Draft'}</Badge>;
  };

  return (
    <ErpLayout
      title="ERP: Sales Orders"
      subtitle="Customer sales orders, fulfillment tracking, and dispatch status"
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
            onClick={() => setIsCreateOpen(true)}
          >
            New Sales Order
          </Button>
        </div>
      }
    >
      <div className="space-y-6">
        {/* Metric Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <StatCard
            icon={DollarSign}
            label="Total Sales Pipeline"
            value={formatAmount(totalValue)}
            loading={loading}
          />
          <StatCard
            icon={ShoppingCart}
            label="Total Sales Orders"
            value={totalCount.toString()}
            loading={loading}
          />
          <StatCard
            icon={PackageCheck}
            label="Confirmed for Dispatch"
            value={confirmedCount.toString()}
            loading={loading}
          />
        </div>

        {/* Search & Filter Toolbar */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <SearchInput
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onClear={() => setSearch('')}
            placeholder="Search by order # or customer..."
            className="w-full sm:max-w-xs"
          />

          <div className="flex items-center gap-2">
            <div className="w-44">
              <Select
                options={SALES_STATUSES}
                value={status}
                onChange={(e) => {
                  setStatus(e.target.value);
                  setCurrentPage(1);
                }}
              />
            </div>

            <TableExport
              label="Export"
              onExportCsv={() => {
                const csvContent =
                  'data:text/csv;charset=utf-8,' +
                  ['OrderNumber,Customer,OrderDate,ExpectedDate,TotalAmount,Status']
                    .concat(
                      orders.map(
                        (o) =>
                          `"${o.orderNumber || ''}","${o.customerName || ''}","${o.orderDate || ''}","${o.expectedFulfillmentDate || ''}",${o.totalAmount || 0},"${o.status || 'Draft'}"`
                      )
                    )
                    .join('\n');
                const encodedUri = encodeURI(csvContent);
                const link = document.createElement('a');
                link.setAttribute('href', encodedUri);
                link.setAttribute('download', `sales_orders_${new Date().toISOString().slice(0, 10)}.csv`);
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
              }}
            />
          </div>
        </div>

        {/* Orders Table */}
        <Card padding="p-0" className="overflow-hidden">
          {loading && (
            <div className="p-6 space-y-3">
              <Skeleton variant="table-row" count={6} />
            </div>
          )}

          {!loading && error && (
            <div className="p-6">
              <ErrorState
                title="Failed to load sales orders"
                message={error.message || 'Unable to retrieve sales records.'}
                onRetry={refetch}
              />
            </div>
          )}

          {!loading && !error && orders.length === 0 && (
            <div className="p-8">
              <EmptyState
                icon={ShoppingCart}
                title="No sales orders found"
                description="Create a sales order to fulfill client requests and generate sales invoices."
                actionLabel="Create Sales Order"
                onAction={() => setIsCreateOpen(true)}
              />
            </div>
          )}

          {!loading && !error && orders.length > 0 && (
            <>
              <Table>
                <TableHeader
                  columns={[
                    { label: 'Order #' },
                    { label: 'Customer' },
                    { label: 'Order Date' },
                    { label: 'Target Dispatch' },
                    { label: 'Total Value' },
                    { label: 'Status' },
                    { label: 'Actions', align: 'right' }
                  ]}
                />
                <tbody>
                  {orders.map((ord) => (
                    <TableRow key={ord.id}>
                      <td className="py-3 px-4 text-xs font-bold text-slate-900 font-mono">
                        {ord.orderNumber || 'SO-DRAFT'}
                      </td>
                      <td className="py-3 px-4 text-xs font-semibold text-slate-800">
                        {ord.customerName || 'Client'}
                      </td>
                      <td className="py-3 px-4 text-xs text-slate-500">
                        {formatDate(ord.orderDate)}
                      </td>
                      <td className="py-3 px-4 text-xs text-slate-500">
                        {formatDate(ord.expectedFulfillmentDate)}
                      </td>
                      <td className="py-3 px-4 text-xs font-mono font-bold text-slate-900">
                        {formatAmount(ord.totalAmount)}
                      </td>
                      <td className="py-3 px-4">
                        {getStatusBadge(ord.status)}
                      </td>
                      <td className="py-3 px-4 text-right">
                        {ord.status === 'Draft' && (
                          <button
                            type="button"
                            onClick={() => handleConfirmOrder(ord.id)}
                            disabled={actionLoading}
                            className="px-2.5 py-1 text-xs font-semibold text-brand-600 hover:bg-brand-50 rounded-lg transition"
                          >
                            Confirm
                          </button>
                        )}
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

      <CreateOrderModal
        isOpen={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
        type="sales"
        onSuccess={refetch}
      />
    </ErpLayout>
  );
}
