import React, { useState } from 'react';
import ErpLayout from '../../layouts/ErpLayout';
import AddExpenseModal from '../../components/erp/AddExpenseModal';

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

import { DollarSign, Plus, CheckCircle, Clock, Tag, RefreshCw } from 'lucide-react';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useOrg } from '../../context/OrgContext';
import { useToast } from '../../hooks/useToast';
import apiClient from '../../services/api/client';
import { parseProblemDetails } from '../../utils/problemDetails';

const EXPENSE_CATEGORIES = [
  { value: '', label: 'All Categories' },
  { value: '1', label: 'Rent & Facilities' },
  { value: '2', label: 'Utilities & Power' },
  { value: '3', label: 'Salaries & Benefits' },
  { value: '4', label: 'Marketing & Advertising' },
  { value: '5', label: 'Logistics & Transport' },
  { value: '6', label: 'Repairs & Maintenance' },
  { value: '7', label: 'General & Admin' }
];

const EXPENSE_STATUSES = [
  { value: '', label: 'All Statuses' },
  { value: 'Draft', label: 'Draft / Pending' },
  { value: 'Approved', label: 'Approved' },
  { value: 'Paid', label: 'Paid / Settled' },
  { value: 'Cancelled', label: 'Cancelled' }
];

/**
 * Operating expenses and disbursements workspace.
 */
export default function ExpensesPage() {
  const { currentOrgId } = useOrg();
  const { showSuccess, showError } = useToast();

  const [search, setSearch] = useState('');
  const [category, setCategory] = useState('');
  const [status, setStatus] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 20;

  const [isAddOpen, setIsAddOpen] = useState(false);
  const [actionLoading, setActionLoading] = useState(false);

  const {
    data: expensesData,
    loading,
    error,
    refetch
  } = useApiQuery(
    () => {
      if (!currentOrgId) return Promise.resolve({ items: [], totalPages: 1, totalCount: 0 });
      return apiClient.get('/org/expenses', {
        params: {
          search: search.trim() || undefined,
          category: category ? parseInt(category, 10) : undefined,
          status: status || undefined,
          pageNumber: currentPage,
          pageSize
        }
      });
    },
    { deps: [currentOrgId, search, category, status, currentPage] }
  );

  const expenses = expensesData?.items || [];
  const totalPages = expensesData?.totalPages || 1;
  const totalCount = expensesData?.totalCount || expenses.length;

  const totalSpent = expenses.reduce((acc, e) => acc + (e.amount || 0), 0);
  const approvedCount = expenses.filter((e) => e.status === 'Approved' || e.status === 'Paid').length;

  const handleApprove = async (id) => {
    setActionLoading(true);
    try {
      await apiClient.post(`/org/expenses/${id}/approve`);
      showSuccess('Operating expense approved.');
      refetch();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      showError(parsed.message || 'Failed to approve expense.');
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
    if (st === 'paid') return <Badge variant="success" dot={true}>Paid</Badge>;
    if (st === 'approved') return <Badge variant="brand" dot={true}>Approved</Badge>;
    if (st === 'cancelled') return <Badge variant="neutral">Cancelled</Badge>;
    return <Badge variant="warning" dot={true}>{s || 'Pending'}</Badge>;
  };

  return (
    <ErpLayout
      title="ERP: Expense Management"
      subtitle="Corporate disbursements, cost centers, and tax deductible expenses"
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
            Record Expense
          </Button>
        </div>
      }
    >
      <div className="space-y-6">
        {/* Metric Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <StatCard
            icon={DollarSign}
            label="Total Expenses Incurred"
            value={formatAmount(totalSpent)}
            loading={loading}
          />
          <StatCard
            icon={Tag}
            label="Total Expense Records"
            value={totalCount.toString()}
            loading={loading}
          />
          <StatCard
            icon={CheckCircle}
            label="Approved & Settled"
            value={approvedCount.toString()}
            loading={loading}
          />
        </div>

        {/* Search & Filter Toolbar */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <SearchInput
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onClear={() => setSearch('')}
            placeholder="Search expense description..."
            className="w-full sm:max-w-xs"
          />

          <div className="flex flex-wrap items-center gap-2">
            <div className="w-44">
              <Select
                options={EXPENSE_CATEGORIES}
                value={category}
                onChange={(e) => {
                  setCategory(e.target.value);
                  setCurrentPage(1);
                }}
              />
            </div>

            <div className="w-36">
              <Select
                options={EXPENSE_STATUSES}
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
                  ['Date,Category,Description,Amount,Status']
                    .concat(
                      expenses.map(
                        (e) =>
                          `"${e.expenseDate || ''}","${e.categoryName || e.category || ''}","${e.description || ''}",${e.amount || 0},"${e.status || 'Draft'}"`
                      )
                    )
                    .join('\n');
                const encodedUri = encodeURI(csvContent);
                const link = document.createElement('a');
                link.setAttribute('href', encodedUri);
                link.setAttribute('download', `expenses_${new Date().toISOString().slice(0, 10)}.csv`);
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
              }}
            />
          </div>
        </div>

        {/* Expenses Table */}
        <Card padding="p-0" className="overflow-hidden">
          {loading && (
            <div className="p-6 space-y-3">
              <Skeleton variant="table-row" count={6} />
            </div>
          )}

          {!loading && error && (
            <div className="p-6">
              <ErrorState
                title="Failed to load expenses"
                message={error.message || 'Unable to retrieve expense entries.'}
                onRetry={refetch}
              />
            </div>
          )}

          {!loading && !error && expenses.length === 0 && (
            <div className="p-8">
              <EmptyState
                icon={DollarSign}
                title="No expenses recorded"
                description="Record your corporate operating expenses and attach invoice receipts."
                actionLabel="Record Expense"
                onAction={() => setIsAddOpen(true)}
              />
            </div>
          )}

          {!loading && !error && expenses.length > 0 && (
            <>
              <Table>
                <TableHeader
                  columns={[
                    { label: 'Expense Date' },
                    { label: 'Category' },
                    { label: 'Description' },
                    { label: 'Amount' },
                    { label: 'Status' },
                    { label: 'Actions', align: 'right' }
                  ]}
                />
                <tbody>
                  {expenses.map((exp) => (
                    <TableRow key={exp.id}>
                      <td className="py-3 px-4 text-xs text-slate-500">
                        {formatDate(exp.expenseDate)}
                      </td>
                      <td className="py-3 px-4 text-xs font-semibold text-slate-800">
                        {exp.categoryName || exp.category || 'General'}
                      </td>
                      <td className="py-3 px-4 text-xs text-slate-600">
                        <div className="truncate max-w-xs">{exp.description}</div>
                      </td>
                      <td className="py-3 px-4 text-xs font-mono font-bold text-slate-900">
                        {formatAmount(exp.amount)}
                      </td>
                      <td className="py-3 px-4">
                        {getStatusBadge(exp.status)}
                      </td>
                      <td className="py-3 px-4 text-right">
                        {exp.status === 'Draft' && (
                          <button
                            type="button"
                            onClick={() => handleApprove(exp.id)}
                            disabled={actionLoading}
                            className="px-2.5 py-1 text-xs font-semibold text-brand-600 hover:bg-brand-50 rounded-lg transition"
                          >
                            Approve
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

      <AddExpenseModal
        isOpen={isAddOpen}
        onClose={() => setIsAddOpen(false)}
        onSuccess={refetch}
      />
    </ErpLayout>
  );
}
