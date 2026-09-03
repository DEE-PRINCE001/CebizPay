import React, { useState } from 'react';
import AdminLayout from '../../layouts/AdminLayout';
import FeePolicyEditorModal from '../../components/admin/FeePolicyEditorModal';

import Table from '../../components/tables/Table';
import TableHeader from '../../components/tables/TableHeader';
import TableRow from '../../components/tables/TableRow';
import Card from '../../components/common/Card';
import Badge from '../../components/common/Badge';
import StatCard from '../../components/common/StatCard';
import Button from '../../components/common/Button';
import Skeleton from '../../components/common/Skeleton';
import EmptyState from '../../components/feedback/EmptyState';
import ErrorState from '../../components/feedback/ErrorState';

import { Scale, Plus, DollarSign, Percent, RefreshCw } from 'lucide-react';
import { useApiQuery } from '../../hooks/useApiQuery';
import apiClient from '../../services/api/client';

/**
 * SuperAdmin platform fee matrix and transaction pricing policy workspace.
 */
export default function AdminFeesPage() {
  const [isEditorOpen, setIsEditorOpen] = useState(false);
  const [defaultPolicyType, setDefaultPolicyType] = useState('peer-transfer');

  // 1. Fetch Peer Transfer Policies
  const {
    data: peerPoliciesData,
    loading: peerLoading,
    error: peerError,
    refetch: refetchPeer
  } = useApiQuery(() => apiClient.get('/admin/fees/peer-transfer').catch(() => []));

  // 2. Fetch Bank Transfer Policies
  const {
    data: bankPoliciesData,
    loading: bankLoading,
    error: bankError,
    refetch: refetchBank
  } = useApiQuery(() => apiClient.get('/admin/fees/bank-transfer').catch(() => []));

  const peerPolicies = Array.isArray(peerPoliciesData) ? peerPoliciesData : [];
  const bankPolicies = Array.isArray(bankPoliciesData) ? bankPoliciesData : [];

  const activePeer = peerPolicies.find((p) => p.isActive) || peerPolicies[0];
  const activeBank = bankPolicies.find((p) => p.isActive) || bankPolicies[0];

  const handleRefreshAll = () => {
    refetchPeer();
    refetchBank();
  };

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

  return (
    <AdminLayout
      title="Platform Fee Matrix & Pricing Policies"
      subtitle="Versioned transaction fee schedules, percentage rates, floors, and caps"
      headerAction={
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            icon={RefreshCw}
            onClick={handleRefreshAll}
          >
            Refresh
          </Button>
          <Button
            variant="primary"
            size="sm"
            icon={Plus}
            onClick={() => {
              setDefaultPolicyType('peer-transfer');
              setIsEditorOpen(true);
            }}
          >
            New Fee Policy Version
          </Button>
        </div>
      }
    >
      <div className="space-y-6">
        {/* Active Policy Status Banner */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <Card padding="p-5" className="bg-white border border-slate-200/80 space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-xs text-slate-500 font-semibold">Active Peer-to-Peer Policy</span>
              <Badge variant="success" dot={true}>Active</Badge>
            </div>
            <div className="text-2xl font-extrabold text-slate-900 font-sans">
              {activePeer?.percentageRate ? `${(activePeer.percentageRate * 100).toFixed(1)}%` : '1.5%'}
            </div>
            <p className="text-xs text-slate-500">
              Floor: ₦{(activePeer?.minimumFee || 20).toLocaleString()} • Cap: ₦{(activePeer?.maximumFee || 2000).toLocaleString()}
            </p>
          </Card>

          <Card padding="p-5" className="bg-white border border-slate-200/80 space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-xs text-slate-500 font-semibold">Active Inter-Bank Transfer Policy</span>
              <Badge variant="success" dot={true}>Active</Badge>
            </div>
            <div className="text-2xl font-extrabold text-slate-900 font-sans">
              {activeBank?.percentageRate ? `${(activeBank.percentageRate * 100).toFixed(1)}%` : '1.5%'}
            </div>
            <p className="text-xs text-slate-500">
              Floor: ₦{(activeBank?.minimumFee || 25).toLocaleString()} • Cap: ₦{(activeBank?.maximumFee || 2500).toLocaleString()}
            </p>
          </Card>
        </div>

        {/* Peer Policies History */}
        <div className="space-y-3">
          <h3 className="text-xs font-bold text-slate-900">Peer-to-Peer Fee Policies History</h3>
          <Card padding="p-0" className="overflow-hidden">
            {peerLoading && (
              <div className="p-4 space-y-2">
                <Skeleton variant="table-row" count={3} />
              </div>
            )}

            {!peerLoading && peerPolicies.length > 0 && (
              <Table>
                <TableHeader
                  columns={[
                    { label: 'Version' },
                    { label: 'Rate (%)' },
                    { label: 'Min Floor' },
                    { label: 'Max Cap' },
                    { label: 'Created By' },
                    { label: 'Status' }
                  ]}
                />
                <tbody>
                  {peerPolicies.map((p, idx) => (
                    <TableRow key={p.id || idx}>
                      <td className="py-2.5 px-4 text-xs font-bold text-slate-900 font-mono">
                        v{p.version || idx + 1}
                      </td>
                      <td className="py-2.5 px-4 text-xs font-bold text-slate-800">
                        {p.percentageRate ? `${(p.percentageRate * 100).toFixed(1)}%` : 'Free'}
                      </td>
                      <td className="py-2.5 px-4 text-xs font-mono text-slate-600">
                        ₦{(p.minimumFee || 0).toLocaleString()}
                      </td>
                      <td className="py-2.5 px-4 text-xs font-mono text-slate-600">
                        ₦{(p.maximumFee || 0).toLocaleString()}
                      </td>
                      <td className="py-2.5 px-4 text-xs text-slate-500 font-mono">
                        {p.createdByUserId ? `${p.createdByUserId.slice(0, 8)}...` : 'SuperAdmin'}
                      </td>
                      <td className="py-2.5 px-4">
                        <Badge variant={p.isActive ? 'success' : 'neutral'} dot={p.isActive}>
                          {p.isActive ? 'Active' : 'Archived'}
                        </Badge>
                      </td>
                    </TableRow>
                  ))}
                </tbody>
              </Table>
            )}
          </Card>
        </div>

        {/* Bank Transfer Policies History */}
        <div className="space-y-3">
          <h3 className="text-xs font-bold text-slate-900">Bank Transfer Fee Policies History</h3>
          <Card padding="p-0" className="overflow-hidden">
            {bankLoading && (
              <div className="p-4 space-y-2">
                <Skeleton variant="table-row" count={3} />
              </div>
            )}

            {!bankLoading && bankPolicies.length > 0 && (
              <Table>
                <TableHeader
                  columns={[
                    { label: 'Version' },
                    { label: 'Rate (%)' },
                    { label: 'Min Floor' },
                    { label: 'Max Cap' },
                    { label: 'Created By' },
                    { label: 'Status' }
                  ]}
                />
                <tbody>
                  {bankPolicies.map((p, idx) => (
                    <TableRow key={p.id || idx}>
                      <td className="py-2.5 px-4 text-xs font-bold text-slate-900 font-mono">
                        v{p.version || idx + 1}
                      </td>
                      <td className="py-2.5 px-4 text-xs font-bold text-slate-800">
                        {p.percentageRate ? `${(p.percentageRate * 100).toFixed(1)}%` : 'Free'}
                      </td>
                      <td className="py-2.5 px-4 text-xs font-mono text-slate-600">
                        ₦{(p.minimumFee || 0).toLocaleString()}
                      </td>
                      <td className="py-2.5 px-4 text-xs font-mono text-slate-600">
                        ₦{(p.maximumFee || 0).toLocaleString()}
                      </td>
                      <td className="py-2.5 px-4 text-xs text-slate-500 font-mono">
                        {p.createdByUserId ? `${p.createdByUserId.slice(0, 8)}...` : 'SuperAdmin'}
                      </td>
                      <td className="py-2.5 px-4">
                        <Badge variant={p.isActive ? 'success' : 'neutral'} dot={p.isActive}>
                          {p.isActive ? 'Active' : 'Archived'}
                        </Badge>
                      </td>
                    </TableRow>
                  ))}
                </tbody>
              </Table>
            )}
          </Card>
        </div>
      </div>

      <FeePolicyEditorModal
        isOpen={isEditorOpen}
        onClose={() => setIsEditorOpen(false)}
        defaultPolicyType={defaultPolicyType}
        onSuccess={handleRefreshAll}
      />
    </AdminLayout>
  );
}
