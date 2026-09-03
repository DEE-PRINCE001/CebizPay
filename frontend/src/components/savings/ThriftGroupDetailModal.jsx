import React, { useState } from 'react';
import Modal from '../common/Modal';
import Badge from '../common/Badge';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import Skeleton from '../common/Skeleton';
import Table from '../tables/Table';
import TableHeader from '../tables/TableHeader';
import TableRow from '../tables/TableRow';
import { Users, Lock, KeyRound, Copy, Check, Calendar, ArrowRight, ShieldCheck } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Detailed view of an Ajo/Esusu rotational thrift circle with position selection.
 */
export default function ThriftGroupDetailModal({
  isOpen,
  onClose,
  group,
  onRefresh
}) {
  const { showSuccess, showError } = useToast();

  const [invitationCode, setInvitationCode] = useState(null);
  const [copied, setCopied] = useState(false);
  const [actionLoading, setActionLoading] = useState(false);
  const [actionError, setActionError] = useState(null);

  // 1. Fetch Members & Position Assignments
  const {
    data: membersData,
    loading: membersLoading,
    refetch: refetchMembers
  } = useApiQuery(
    () => {
      if (!group?.id) return Promise.resolve([]);
      return apiClient.get(`/work/thrift/${group.id}/members`);
    },
    { deps: [group?.id], enabled: isOpen && !!group?.id }
  );

  // 2. Fetch Rotation Cycles
  const {
    data: cyclesData,
    loading: cyclesLoading,
    refetch: refetchCycles
  } = useApiQuery(
    () => {
      if (!group?.id) return Promise.resolve([]);
      return apiClient.get(`/work/thrift/${group.id}/cycles`);
    },
    { deps: [group?.id], enabled: isOpen && !!group?.id }
  );

  const members = Array.isArray(membersData) ? membersData : [];
  const cycles = Array.isArray(cyclesData) ? cyclesData : [];

  if (!group) return null;

  const totalPool = (group.contributionAmount || 0) * (group.totalPositions || 1);

  // Generate Invite Code
  const handleGenerateInvite = async () => {
    setActionLoading(true);
    setActionError(null);
    try {
      const res = await apiClient.post(`/work/thrift/${group.id}/invite`, {});
      setInvitationCode(res?.invitationCode || 'INVITED');
      showSuccess('Invitation code generated.');
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setActionError(parsed.message || 'Failed to generate invitation code.');
    } finally {
      setActionLoading(false);
    }
  };

  // Copy Invite Code
  const handleCopyCode = () => {
    if (invitationCode) {
      navigator.clipboard.writeText(invitationCode);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  // Select Position Slot
  const handleSelectPosition = async (posNumber) => {
    setActionLoading(true);
    setActionError(null);
    try {
      await apiClient.post(`/work/thrift/${group.id}/position`, {
        position: posNumber
      });
      showSuccess(`Selected rotation position #${posNumber}.`);
      refetchMembers();
      if (onRefresh) onRefresh();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setActionError(parsed.message || 'Could not select this position slot.');
    } finally {
      setActionLoading(false);
    }
  };

  // Lock Positions
  const handleLockPositions = async () => {
    setActionLoading(true);
    setActionError(null);
    try {
      await apiClient.post(`/work/thrift/${group.id}/lock`);
      showSuccess('Thrift circle positions locked and activated.');
      if (onRefresh) onRefresh();
      onClose();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setActionError(parsed.message || 'Failed to lock positions.');
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

  const getStatusBadge = (status) => {
    const s = (status || '').toLowerCase();
    if (s === 'active' || s === 'running') return <Badge variant="success" dot={true}>Active</Badge>;
    if (s === 'openformembers' || s === 'open') return <Badge variant="brand" dot={true}>Open for Slots</Badge>;
    if (s === 'completed') return <Badge variant="neutral">Completed</Badge>;
    return <Badge variant="neutral">{status || 'Open'}</Badge>;
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={group.name}
      subtitle={`Total Rotation Pool: ${formatAmount(totalPool)} • ${group.frequency}`}
      maxWidth="max-w-2xl"
    >
      <div className="space-y-5 pt-1">
        {actionError && (
          <Alert variant="danger" onClose={() => setActionError(null)}>
            {actionError}
          </Alert>
        )}

        {/* Top Summary Banner */}
        <div className="p-4 bg-slate-50 border border-slate-200/80 rounded-2xl flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs">
          <div>
            <div className="flex items-center gap-2 mb-1">
              <span className="font-bold text-slate-900">{group.name}</span>
              {getStatusBadge(group.status)}
            </div>
            <span className="text-slate-500 block">
              Contribution: <strong className="text-slate-900 font-mono">{formatAmount(group.contributionAmount)}</strong> / {group.frequency}
            </span>
          </div>

          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              icon={KeyRound}
              loading={actionLoading}
              onClick={handleGenerateInvite}
            >
              Get Invite Code
            </Button>

            {group.status === 'OpenForMembers' && (
              <Button
                variant="primary"
                size="sm"
                icon={Lock}
                loading={actionLoading}
                onClick={handleLockPositions}
              >
                Lock Positions
              </Button>
            )}
          </div>
        </div>

        {/* Invite Code Popover if generated */}
        {invitationCode && (
          <div className="p-3 bg-brand-50 border border-brand-200 rounded-xl flex items-center justify-between text-xs">
            <div className="flex items-center gap-2">
              <KeyRound size={15} className="text-brand-600" />
              <span>Invite Code: <strong className="font-mono text-brand-700 text-sm">{invitationCode}</strong></span>
            </div>
            <button
              type="button"
              onClick={handleCopyCode}
              className="px-2.5 py-1 bg-white border border-brand-200 rounded-lg font-semibold text-brand-600 hover:bg-brand-50 flex items-center gap-1 transition"
            >
              {copied ? <Check size={13} /> : <Copy size={13} />}
              <span>{copied ? 'Copied' : 'Copy'}</span>
            </button>
          </div>
        )}

        {/* Position Slots Selector Grid */}
        <div className="space-y-2">
          <span className="text-xs font-bold text-slate-900 block">
            Rotation Positions ({members.length} / {group.totalPositions || 10} Occupied)
          </span>

          {membersLoading && <Skeleton variant="card" />}

          {!membersLoading && (
            <div className="grid grid-cols-2 sm:grid-cols-5 gap-2.5">
              {Array.from({ length: group.totalPositions || 10 }, (_, i) => i + 1).map((posNum) => {
                const assignedMember = members.find((m) => m.position === posNum);
                const isOccupied = !!assignedMember;

                return (
                  <div
                    key={posNum}
                    className={`p-3 rounded-xl border text-xs flex flex-col justify-between text-center transition ${
                      isOccupied
                        ? 'bg-slate-50 border-slate-200 text-slate-700'
                        : 'bg-white border-brand-200 text-brand-700 hover:border-brand-400'
                    }`}
                  >
                    <div>
                      <span className="font-bold block text-sm mb-1">Slot #{posNum}</span>
                      <span className="text-[10px] text-slate-400 block truncate">
                        {isOccupied ? (assignedMember.userId ? `Member` : 'Claimed') : 'Available'}
                      </span>
                    </div>

                    {!isOccupied && group.status === 'OpenForMembers' && (
                      <button
                        type="button"
                        onClick={() => handleSelectPosition(posNum)}
                        disabled={actionLoading}
                        className="mt-2 w-full py-1 text-[11px] font-bold bg-brand-600 hover:bg-brand-700 text-white rounded-lg transition"
                      >
                        Select
                      </button>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* Scheduled Rotation Cycles */}
        {cycles.length > 0 && (
          <div className="space-y-2">
            <span className="text-xs font-bold text-slate-900 block">Scheduled Rotation Cycles</span>
            <div className="max-h-48 overflow-y-auto border border-slate-200 rounded-2xl">
              <Table>
                <TableHeader
                  columns={[
                    { label: 'Cycle #' },
                    { label: 'Due Date' },
                    { label: 'Target Payout Position' },
                    { label: 'Pool Status' }
                  ]}
                />
                <tbody>
                  {cycles.map((c) => (
                    <TableRow key={c.id}>
                      <td className="py-2.5 px-4 text-xs font-bold text-slate-900">
                        Cycle {c.cycleNumber}
                      </td>
                      <td className="py-2.5 px-4 text-xs text-slate-500">
                        {formatDate(c.dueDateUtc)}
                      </td>
                      <td className="py-2.5 px-4 text-xs font-semibold text-slate-700">
                        Slot #{c.targetPayoutPosition}
                      </td>
                      <td className="py-2.5 px-4 text-xs">
                        <Badge variant={c.status === 'Completed' ? 'success' : 'warning'}>
                          {c.status}
                        </Badge>
                      </td>
                    </TableRow>
                  ))}
                </tbody>
              </Table>
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
}
