import React, { useState } from 'react';
import AdminLayout from '../../layouts/AdminLayout';
import EddCaseReviewModal from '../../components/admin/EddCaseReviewModal';

import Table from '../../components/tables/Table';
import TableHeader from '../../components/tables/TableHeader';
import TableRow from '../../components/tables/TableRow';
import Card from '../../components/common/Card';
import Badge from '../../components/common/Badge';
import StatCard from '../../components/common/StatCard';
import Button from '../../components/common/Button';
import SearchInput from '../../components/forms/SearchInput';
import Skeleton from '../../components/common/Skeleton';
import EmptyState from '../../components/feedback/EmptyState';
import ErrorState from '../../components/feedback/ErrorState';

import { ShieldAlert, CheckCircle2, XCircle, FileText, RefreshCw, Eye } from 'lucide-react';
import { useApiQuery } from '../../hooks/useApiQuery';
import apiClient from '../../services/api/client';

/**
 * SuperAdmin compliance oversight and EDD case queue.
 */
export default function AdminCompliancePage() {
  const [search, setSearch] = useState('');
  const [selectedCase, setSelectedCase] = useState(null);

  const {
    data: eddData,
    loading,
    error,
    refetch
  } = useApiQuery(() => apiClient.get('/admin/compliance/edd/cases').catch(() => []));

  const eddCases = Array.isArray(eddData) ? eddData : [];
  const filteredCases = eddCases.filter((c) =>
    (c.subjectId || '').toLowerCase().includes(search.toLowerCase()) ||
    (c.triggerReason || '').toLowerCase().includes(search.toLowerCase())
  );

  const pendingCount = eddCases.filter((c) => c.status !== 'Approved' && c.status !== 'Rejected').length;
  const approvedCount = eddCases.filter((c) => c.status === 'Approved').length;

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

  const getStatusBadge = (status) => {
    const s = (status || '').toLowerCase();
    if (s === 'approved') return <Badge variant="success" dot={true}>Approved</Badge>;
    if (s === 'rejected') return <Badge variant="danger" dot={true}>Rejected</Badge>;
    return <Badge variant="warning" dot={true}>{status || 'Pending'}</Badge>;
  };

  return (
    <AdminLayout
      title="Compliance & Risk Oversight"
      subtitle="Enhanced Due Diligence (EDD) investigations and regulatory AML screening queue"
      headerAction={
        <Button
          variant="outline"
          size="sm"
          icon={RefreshCw}
          onClick={refetch}
        >
          Refresh Queue
        </Button>
      }
    >
      <div className="space-y-6">
        {/* Metric Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <StatCard
            icon={ShieldAlert}
            label="Total EDD Inquiries"
            value={eddCases.length.toString()}
            loading={loading}
          />
          <StatCard
            icon={FileText}
            label="Pending Officer Review"
            value={pendingCount.toString()}
            loading={loading}
          />
          <StatCard
            icon={CheckCircle2}
            label="Approved Clearances"
            value={approvedCount.toString()}
            loading={loading}
          />
        </div>

        {/* Search */}
        <div className="flex items-center justify-between gap-3">
          <SearchInput
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onClear={() => setSearch('')}
            placeholder="Search by subject ID or trigger reason..."
            className="w-full sm:max-w-xs"
          />
        </div>

        {/* EDD Table */}
        <Card padding="p-0" className="overflow-hidden">
          {loading && (
            <div className="p-6 space-y-3">
              <Skeleton variant="table-row" count={5} />
            </div>
          )}

          {!loading && error && (
            <div className="p-6">
              <ErrorState
                title="Failed to load compliance queue"
                message={error.message || 'Unable to retrieve EDD cases.'}
                onRetry={refetch}
              />
            </div>
          )}

          {!loading && !error && filteredCases.length === 0 && (
            <div className="p-8">
              <EmptyState
                icon={ShieldAlert}
                title="No compliance cases pending"
                description="All automated AML screenings and individual/corporate verification triggers have been reviewed."
              />
            </div>
          )}

          {!loading && !error && filteredCases.length > 0 && (
            <Table>
              <TableHeader
                columns={[
                  { label: 'Subject ID' },
                  { label: 'Subject Type' },
                  { label: 'Trigger Reason' },
                  { label: 'Triggered Date' },
                  { label: 'Status' },
                  { label: 'Actions', align: 'right' }
                ]}
              />
              <tbody>
                {filteredCases.map((c) => (
                  <TableRow key={c.id}>
                    <td className="py-3 px-4 text-xs font-bold text-slate-900 font-mono">
                      {c.subjectId || 'User Subject'}
                    </td>
                    <td className="py-3 px-4 text-xs text-slate-600">
                      {c.subjectType || 'Individual'}
                    </td>
                    <td className="py-3 px-4 text-xs text-slate-700">
                      <div className="truncate max-w-xs">{c.triggerReason || 'AML / High Volume Risk Spike'}</div>
                    </td>
                    <td className="py-3 px-4 text-xs text-slate-500">
                      {formatDate(c.createdAtUtc)}
                    </td>
                    <td className="py-3 px-4">
                      {getStatusBadge(c.status)}
                    </td>
                    <td className="py-3 px-4 text-right">
                      <button
                        type="button"
                        onClick={() => setSelectedCase(c)}
                        className="px-2.5 py-1 text-xs font-semibold text-brand-600 hover:bg-brand-50 rounded-lg transition inline-flex items-center gap-1"
                      >
                        <Eye size={13} />
                        <span>Review</span>
                      </button>
                    </td>
                  </TableRow>
                ))}
              </tbody>
            </Table>
          )}
        </Card>
      </div>

      <EddCaseReviewModal
        isOpen={!!selectedCase}
        onClose={() => setSelectedCase(null)}
        eddCase={selectedCase}
        onSuccess={refetch}
      />
    </AdminLayout>
  );
}
