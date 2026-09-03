import React from 'react';
import AdminLayout from '../../layouts/AdminLayout';
import StatCard from '../../components/common/StatCard';
import Card from '../../components/common/Card';
import Badge from '../../components/common/Badge';
import Button from '../../components/common/Button';
import Skeleton from '../../components/common/Skeleton';
import Table from '../../components/tables/Table';
import TableHeader from '../../components/tables/TableHeader';
import TableRow from '../../components/tables/TableRow';
import { Building2, Users, Receipt, Wallet, ShieldAlert, Scale, ArrowUpRight, RefreshCw, Layers } from 'lucide-react';
import { useApiQuery } from '../../hooks/useApiQuery';
import apiClient from '../../services/api/client';
import { Link } from 'react-router-dom';
import { ROUTES } from '../../constants/routes';

/**
 * SuperAdmin platform oversight dashboard.
 */
export default function AdminDashboardPage() {
  // 1. Fetch Audit Logs
  const {
    data: auditData,
    loading: auditLoading,
    refetch: refetchAudit
  } = useApiQuery(() => apiClient.get('/admin/audit-logs', { params: { pageSize: 5 } }).catch(() => null));

  // 2. Fetch EDD Cases
  const {
    data: eddData,
    loading: eddLoading,
    refetch: refetchEdd
  } = useApiQuery(() => apiClient.get('/admin/compliance/edd/cases').catch(() => []));

  // 3. Fetch Active Peer Fee Policy
  const {
    data: peerFeePolicy,
    refetch: refetchFee
  } = useApiQuery(() => apiClient.get('/admin/fees/peer-transfer/active').catch(() => null));

  const auditLogs = auditData?.items || [];
  const eddCases = Array.isArray(eddData) ? eddData : [];

  const handleRefreshAll = () => {
    refetchAudit();
    refetchEdd();
    refetchFee();
  };

  const formatDate = (dateString) => {
    if (!dateString) return '—';
    try {
      return new Intl.DateTimeFormat('en-GB', {
        day: 'numeric',
        month: 'short',
        hour: '2-digit',
        minute: '2-digit'
      }).format(new Date(dateString));
    } catch {
      return dateString;
    }
  };

  return (
    <AdminLayout
      title="Platform Oversight Command Console"
      subtitle="Multi-tenant settlement, compliance queue, and central ledger infrastructure"
      headerAction={
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            icon={RefreshCw}
            onClick={handleRefreshAll}
          >
            Refresh Platform
          </Button>
          <Link
            to={ROUTES.ADMIN_FEES}
            className="px-3.5 py-1.5 bg-brand-600 hover:bg-brand-700 text-white font-semibold text-xs rounded-full flex items-center gap-1.5 transition"
          >
            <Scale size={14} />
            <span>Manage Fees</span>
          </Link>
        </div>
      }
    >
      <div className="space-y-6">
        {/* Metric Cards */}
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          <StatCard
            icon={Building2}
            label="Corporate Tenants"
            value="142"
            trend="+8% this mo"
          />
          <StatCard
            icon={Users}
            label="Platform Users"
            value="12,480"
            trend="+15% this mo"
          />
          <StatCard
            icon={Receipt}
            label="24h Settlement Volume"
            value="₦482.5M"
          />
          <StatCard
            icon={ShieldAlert}
            label="Pending EDD Cases"
            value={eddCases.length.toString()}
            loading={eddLoading}
          />
        </div>

        {/* Mid-Grid: Fee Status + EDD Queue */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Active Pricing Card */}
          <Card padding="p-5" className="bg-white border border-slate-200/80 space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <div className="flex items-center gap-2.5">
                <div className="w-8 h-8 rounded-xl bg-purple-50 text-purple-600 flex items-center justify-center">
                  <Scale size={16} />
                </div>
                <div>
                  <h4 className="font-bold text-xs text-slate-900">Active Fee Matrix</h4>
                  <p className="text-[11px] text-slate-400">Live transaction fee parameters</p>
                </div>
              </div>
              <Link to={ROUTES.ADMIN_FEES} className="text-xs font-semibold text-brand-600 hover:underline flex items-center gap-1">
                <span>Configure</span>
                <ArrowUpRight size={13} />
              </Link>
            </div>

            <div className="grid grid-cols-2 gap-3 text-xs">
              <div className="p-3 bg-slate-50 border border-slate-100 rounded-xl space-y-1">
                <span className="text-slate-400 block text-[11px]">Peer-to-Peer Transfer</span>
                <span className="font-bold text-slate-900 block font-mono">
                  {peerFeePolicy?.percentageRate ? `${(peerFeePolicy.percentageRate * 100).toFixed(1)}%` : '1.5%'}
                </span>
                <span className="text-[10px] text-slate-400 block">
                  Floor ₦{peerFeePolicy?.minimumFee || 20} • Cap ₦{peerFeePolicy?.maximumFee || 2000}
                </span>
              </div>

              <div className="p-3 bg-slate-50 border border-slate-100 rounded-xl space-y-1">
                <span className="text-slate-400 block text-[11px]">NIBSS Bank Transfer</span>
                <span className="font-bold text-slate-900 block font-mono">1.5%</span>
                <span className="text-[10px] text-slate-400 block">Floor ₦25 • Cap ₦2,500</span>
              </div>
            </div>
          </Card>

          {/* Pending Compliance EDD Cases */}
          <Card padding="p-5" className="bg-white border border-slate-200/80 space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <div className="flex items-center gap-2.5">
                <div className="w-8 h-8 rounded-xl bg-amber-50 text-amber-600 flex items-center justify-center">
                  <ShieldAlert size={16} />
                </div>
                <div>
                  <h4 className="font-bold text-xs text-slate-900">Compliance Queue</h4>
                  <p className="text-[11px] text-slate-400">Enhanced Due Diligence reviews</p>
                </div>
              </div>
              <Link to={ROUTES.ADMIN_COMPLIANCE} className="text-xs font-semibold text-brand-600 hover:underline flex items-center gap-1">
                <span>View All</span>
                <ArrowUpRight size={13} />
              </Link>
            </div>

            {eddCases.length === 0 ? (
              <div className="text-center py-6 text-xs text-slate-400">
                No outstanding compliance cases requiring review.
              </div>
            ) : (
              <div className="space-y-2">
                {eddCases.slice(0, 3).map((c) => (
                  <div key={c.id} className="p-2.5 bg-slate-50 rounded-xl flex items-center justify-between text-xs">
                    <div>
                      <span className="font-bold text-slate-900 block truncate max-w-xs">{c.subjectId || 'User Subject'}</span>
                      <span className="text-[10px] text-slate-400 block">{formatDate(c.createdAtUtc)}</span>
                    </div>
                    <Badge variant="warning">{c.status || 'Pending'}</Badge>
                  </div>
                ))}
              </div>
            )}
          </Card>
        </div>

        {/* Real-time System Audit Trail */}
        <Card padding="p-0" className="overflow-hidden">
          <div className="p-4 border-b border-slate-100 flex items-center justify-between">
            <h4 className="font-bold text-xs text-slate-900">Recent Platform Audit Events</h4>
            <Link to={ROUTES.ADMIN_AUDIT_LOGS} className="text-xs font-semibold text-brand-600 hover:underline flex items-center gap-1">
              <span>Full Audit Trail</span>
              <ArrowUpRight size={13} />
            </Link>
          </div>

          {auditLoading && (
            <div className="p-4 space-y-2">
              <Skeleton variant="table-row" count={4} />
            </div>
          )}

          {!auditLoading && auditLogs.length > 0 && (
            <Table>
              <TableHeader
                columns={[
                  { label: 'Action' },
                  { label: 'Resource' },
                  { label: 'Actor' },
                  { label: 'Timestamp' }
                ]}
              />
              <tbody>
                {auditLogs.map((log, idx) => (
                  <TableRow key={log.id || idx}>
                    <td className="py-2.5 px-4 text-xs font-bold text-slate-900 font-mono">
                      {log.action}
                    </td>
                    <td className="py-2.5 px-4 text-xs text-slate-600">
                      {log.resourceType} {log.resourceId ? `(${log.resourceId.slice(0, 8)}...)` : ''}
                    </td>
                    <td className="py-2.5 px-4 text-xs text-slate-500 font-mono">
                      {log.actorId ? `${log.actorId.slice(0, 8)}...` : 'System'}
                    </td>
                    <td className="py-2.5 px-4 text-xs text-slate-400">
                      {formatDate(log.timestampUtc || log.createdAtUtc)}
                    </td>
                  </TableRow>
                ))}
              </tbody>
            </Table>
          )}
        </Card>
      </div>
    </AdminLayout>
  );
}
