import React, { useState } from 'react';
import ErpLayout from '../../layouts/ErpLayout';
import CreateInvoiceModal from '../../components/erp/CreateInvoiceModal';
import InvoiceDetailsDrawer from '../../components/erp/InvoiceDetailsDrawer';

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

import { Receipt, Plus, DollarSign, Send, CheckCircle2, Clock, RefreshCw } from 'lucide-react';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useOrg } from '../../context/OrgContext';
import apiClient from '../../services/api/client';

const INVOICE_STATUSES = [
  { value: '', label: 'All Invoices' },
  { value: 'Draft', label: 'Draft' },
  { value: 'Issued', label: 'Issued' },
  { value: 'Paid', label: 'Paid' },
  { value: 'PartiallyPaid', label: 'Partially Paid' },
  { value: 'Overdue', label: 'Overdue' },
  { value: 'Cancelled', label: 'Cancelled' }
];

/**
 * Invoices management workspace.
 */
export default function InvoicesPage() {
  const { currentOrgId } = useOrg();

  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 20;

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [selectedInvoice, setSelectedInvoice] = useState(null);

  const {
    data: invoicesData,
    loading,
    error,
    refetch
  } = useApiQuery(
    () => {
      if (!currentOrgId) return Promise.resolve({ items: [], totalPages: 1, totalCount: 0 });
      return apiClient.get('/org/invoices', {
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

  const invoices = invoicesData?.items || [];
  const totalPages = invoicesData?.totalPages || 1;
  const totalCount = invoicesData?.totalCount || invoices.length;

  const totalInvoiced = invoices.reduce((acc, inv) => acc + (inv.totalAmount || 0), 0);
  const totalPaid = invoices.reduce((acc, inv) => acc + (inv.amountPaid || 0), 0);
  const outstanding = totalInvoiced - totalPaid;

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

  const getStatusBadge = (invStatus) => {
    const s = (invStatus || '').toLowerCase();
    if (s === 'paid') return <Badge variant="success" dot={true}>Paid</Badge>;
    if (s === 'partiallypaid') return <Badge variant="warning" dot={true}>Partially Paid</Badge>;
    if (s === 'issued') return <Badge variant="brand" dot={true}>Issued</Badge>;
    if (s === 'overdue') return <Badge variant="danger" dot={true}>Overdue</Badge>;
    if (s === 'cancelled') return <Badge variant="neutral">Cancelled</Badge>;
    return <Badge variant="neutral">{invStatus || 'Draft'}</Badge>;
  };

  return (
    <ErpLayout
      title="ERP: Invoices & Billing"
      subtitle="Client billing, statutory VAT calculations, and instant settlement tracking"
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
            Create Invoice
          </Button>
        </div>
      }
    >
      <div className="space-y-6">
        {/* Top Metric Cards */}
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          <StatCard
            icon={DollarSign}
            label="Total Invoiced"
            value={formatAmount(totalInvoiced)}
            loading={loading}
          />
          <StatCard
            icon={CheckCircle2}
            label="Total Collected"
            value={formatAmount(totalPaid)}
            loading={loading}
          />
          <StatCard
            icon={Clock}
            label="Outstanding Balance"
            value={formatAmount(outstanding)}
            loading={loading}
          />
          <StatCard
            icon={Receipt}
            label="Total Invoices"
            value={totalCount.toString()}
            loading={loading}
          />
        </div>

        {/* Search & Status Filter */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <SearchInput
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onClear={() => setSearch('')}
            placeholder="Search by invoice # or customer..."
            className="w-full sm:max-w-xs"
          />

          <div className="flex items-center gap-2">
            <div className="w-44">
              <Select
                options={INVOICE_STATUSES}
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
                  ['InvoiceNumber,Customer,IssueDate,DueDate,TotalAmount,AmountPaid,Status']
                    .concat(
                      invoices.map(
                        (i) =>
                          `"${i.invoiceNumber || ''}","${i.customerName || ''}","${i.issueDate || ''}","${i.dueDate || ''}",${i.totalAmount || 0},${i.amountPaid || 0},"${i.status || 'Draft'}"`
                      )
                    )
                    .join('\n');
                const encodedUri = encodeURI(csvContent);
                const link = document.createElement('a');
                link.setAttribute('href', encodedUri);
                link.setAttribute('download', `invoices_${new Date().toISOString().slice(0, 10)}.csv`);
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
              }}
            />
          </div>
        </div>

        {/* Invoices Table Card */}
        <Card padding="p-0" className="overflow-hidden">
          {loading && (
            <div className="p-6 space-y-3">
              <Skeleton variant="table-row" count={6} />
            </div>
          )}

          {!loading && error && (
            <div className="p-6">
              <ErrorState
                title="Failed to load invoices"
                message={error.message || 'Unable to retrieve invoices list.'}
                onRetry={refetch}
              />
            </div>
          )}

          {!loading && !error && invoices.length === 0 && (
            <div className="p-8">
              <EmptyState
                icon={Receipt}
                title="No invoices found"
                description="Issue your first invoice to bill customers with statutory VAT calculations."
                actionLabel="Create Invoice"
                onAction={() => setIsCreateOpen(true)}
              />
            </div>
          )}

          {!loading && !error && invoices.length > 0 && (
            <>
              <Table>
                <TableHeader
                  columns={[
                    { label: 'Invoice #' },
                    { label: 'Customer' },
                    { label: 'Issue Date' },
                    { label: 'Due Date' },
                    { label: 'Total Amount' },
                    { label: 'Status' },
                    { label: 'Actions', align: 'right' }
                  ]}
                />
                <tbody>
                  {invoices.map((inv) => (
                    <TableRow key={inv.id} onClick={() => setSelectedInvoice(inv)} className="cursor-pointer">
                      <td className="py-3 px-4 text-xs font-bold text-slate-900 font-mono">
                        {inv.invoiceNumber || 'INV-DRAFT'}
                      </td>
                      <td className="py-3 px-4 text-xs font-semibold text-slate-800">
                        {inv.customerName || 'Client'}
                      </td>
                      <td className="py-3 px-4 text-xs text-slate-500">
                        {formatDate(inv.issueDate)}
                      </td>
                      <td className="py-3 px-4 text-xs text-slate-500">
                        {formatDate(inv.dueDate)}
                      </td>
                      <td className="py-3 px-4 text-xs font-mono font-bold text-slate-900">
                        {formatAmount(inv.totalAmount)}
                      </td>
                      <td className="py-3 px-4">
                        {getStatusBadge(inv.status)}
                      </td>
                      <td className="py-3 px-4 text-right">
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            setSelectedInvoice(inv);
                          }}
                          className="px-2.5 py-1 text-xs font-semibold text-brand-600 hover:bg-brand-50 rounded-lg transition"
                        >
                          View
                        </button>
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

      {/* Modals */}
      <CreateInvoiceModal
        isOpen={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
        onSuccess={refetch}
      />

      <InvoiceDetailsDrawer
        isOpen={!!selectedInvoice}
        onClose={() => setSelectedInvoice(null)}
        invoice={selectedInvoice}
        onRefresh={refetch}
      />
    </ErpLayout>
  );
}
