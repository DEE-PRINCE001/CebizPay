import React, { useState } from 'react';
import AdminLayout from '../../layouts/AdminLayout';

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

import { History, Shield, RefreshCw, KeyRound } from 'lucide-react';
import { useApiQuery } from '../../hooks/useApiQuery';
import apiClient from '../../services/api/client';

/**
 * SuperAdmin platform immutable audit logs repository.
 */
export default function AdminAuditLogsPage() {
  const [action, setAction] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 25;

  const {
    data: auditData,
    loading,
    error,
    refetch
  } = useApiQuery(
    () => apiClient.get('/admin/audit-logs', {
      params: {
        action: action.trim() || undefined,
        pageNumber: currentPage,
        pageSize
      }
    }),
    { deps: [action, currentPage] }
  );

  const logs = auditData?.items || [];
  const totalPages = auditData?.totalPages || 1;
  const totalCount = auditData?.totalCount || logs.length;

  const formatDate = (dateString) => {
    if (!dateString) return '—';
    try {
      return new Intl.DateTimeFormat('en-GB', {
        day: 'numeric',
        month: 'short',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
      }).format(new Date(dateString));
    } catch {
      return dateString;
    }
  };

  return (
    <AdminLayout
      title="Platform Immutable Audit Trail"
      subtitle="Cryptographically verified administrative and tenant action event logs"
      headerAction={
        <Button
          variant="outline"
          size="sm"
          icon={RefreshCw}
          onClick={refetch}
          className="hidden sm:inline-flex"
        >
          Refresh Logs
        </Button>
      }
    >
      <div className="space-y-6">
        {/* Search & Export Toolbar */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <SearchInput
            value={action}
            onChange={(e) => setSearch(e.target.value)}
            onClear={() => setAction('')}
            placeholder="Filter by action (e.g. USER_LOGIN, FEE_UPDATED)..."
            className="w-full sm:max-w-xs"
          />

          <TableExport
            label="Export Audit Trail"
            onExportCsv={() => {
              const csvContent =
                'data:text/csv;charset=utf-8,' +
                ['Action,ResourceType,ResourceId,ActorId,CorrelationId,Timestamp']
                  .concat(
                    logs.map(
                      (l) =>
                        `"${l.action || ''}","${l.resourceType || ''}","${l.resourceId || ''}","${l.actorId || ''}","${l.correlationId || ''}","${l.timestampUtc || l.createdAtUtc || ''}"`
                    )
                  )
                  .join('\n');
              const encodedUri = encodeURI(csvContent);
              const link = document.createElement('a');
              link.setAttribute('href', encodedUri);
              link.setAttribute('download', `audit_trail_${new Date().toISOString().slice(0, 10)}.csv`);
              document.body.appendChild(link);
              link.click();
              document.body.removeChild(link);
            }}
          />
        </div>

        {/* Audit Log Table */}
        <Card padding="p-0" className="overflow-hidden">
          {loading && (
            <div className="p-6 space-y-3">
              <Skeleton variant="table-row" count={6} />
            </div>
          )}

          {!loading && error && (
            <div className="p-6">
              <ErrorState
                title="Failed to load audit logs"
                message={error.message || 'Unable to retrieve audit trail entries.'}
                onRetry={refetch}
              />
            </div>
          )}

          {!loading && !error && logs.length === 0 && (
            <div className="p-8">
              <EmptyState
                icon={History}
                title="No audit entries matching filter"
                description="Platform administrative actions, ledger changes, and compliance updates will appear here."
              />
            </div>
          )}

          {!loading && !error && logs.length > 0 && (
            <>
              <Table>
                <TableHeader
                  columns={[
                    { label: 'Action' },
                    { label: 'Resource Target' },
                    { label: 'Actor ID' },
                    { label: 'Correlation ID' },
                    { label: 'Timestamp (UTC)' }
                  ]}
                />
                <tbody>
                  {logs.map((log, idx) => (
                    <TableRow key={log.id || idx}>
                      <td className="py-3 px-4 text-xs font-bold text-slate-900 font-mono">
                        {log.action}
                      </td>
                      <td className="py-3 px-4 text-xs text-slate-700">
                        {log.resourceType} {log.resourceId ? <span className="text-slate-400 font-mono text-[11px]">({log.resourceId.slice(0, 8)}...)</span> : ''}
                      </td>
                      <td className="py-3 px-4 text-xs font-mono text-slate-600">
                        {log.actorId ? `${log.actorId.slice(0, 8)}...` : 'System'}
                      </td>
                      <td className="py-3 px-4 text-xs font-mono text-slate-400">
                        {log.correlationId ? `${log.correlationId.slice(0, 8)}...` : '—'}
                      </td>
                      <td className="py-3 px-4 text-xs text-slate-500 whitespace-nowrap">
                        {formatDate(log.timestampUtc || log.createdAtUtc)}
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
    </AdminLayout>
  );
}
