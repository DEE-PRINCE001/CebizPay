import React from 'react';
import Card from '../common/Card';
import Badge from '../common/Badge';
import Skeleton from '../common/Skeleton';
import EmptyState from '../feedback/EmptyState';
import ErrorState from '../feedback/ErrorState';
import Button from '../common/Button';
import { Users, Coins, ArrowUpRight, Calendar, UserPlus } from 'lucide-react';

/**
 * Visual grid list of rotational Thrift (Ajo / Esusu) circles.
 */
export default function ThriftCycleList({
  groups = [],
  loading = false,
  error = null,
  onRetry,
  onViewGroup,
  onCreateGroup,
  onJoinGroup,
  className = ''
}) {
  const getStatusBadge = (status) => {
    const s = (status || '').toLowerCase();
    if (s === 'active' || s === 'running') return <Badge variant="success" dot={true}>Active</Badge>;
    if (s === 'openformembers' || s === 'open') return <Badge variant="brand" dot={true}>Open for Slots</Badge>;
    if (s === 'completed') return <Badge variant="neutral">Completed</Badge>;
    return <Badge variant="neutral">{status || 'Open'}</Badge>;
  };

  const formatAmount = (amt) => {
    return new Intl.NumberFormat('en-NG', {
      style: 'currency',
      currency: 'NGN',
      minimumFractionDigits: 2
    }).format(amt || 0);
  };

  return (
    <div className={className}>
      {loading && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <Skeleton variant="card" count={3} />
        </div>
      )}

      {!loading && error && (
        <ErrorState
          title="Failed to load thrift circles"
          message={error.message || 'Unable to retrieve your Ajo/Esusu circles.'}
          onRetry={onRetry}
        />
      )}

      {!loading && !error && groups.length === 0 && (
        <EmptyState
          icon={Coins}
          title="No thrift circles found"
          description="Create or join an automated rotational thrift circle (Ajo/Esusu) to build community savings."
          actionLabel="Create Thrift Circle"
          onAction={onCreateGroup}
        />
      )}

      {!loading && !error && groups.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          {groups.map((grp) => {
            const totalPool = (grp.contributionAmount || 0) * (grp.totalPositions || 1);

            return (
              <Card
                key={grp.id}
                hoverEffect={true}
                padding="p-5"
                onClick={() => onViewGroup && onViewGroup(grp)}
                className="flex flex-col justify-between cursor-pointer group bg-white border border-slate-200/80"
              >
                <div className="space-y-4">
                  {/* Header */}
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-2xl bg-amber-50 text-amber-600 flex items-center justify-center shrink-0">
                        <Coins size={20} />
                      </div>
                      <div className="min-w-0">
                        <h4 className="font-bold text-sm text-slate-900 truncate group-hover:text-brand-600 transition">
                          {grp.name}
                        </h4>
                        <span className="text-[11px] text-slate-400 block">
                          {grp.frequency} • {grp.totalPositions || 10} Slots
                        </span>
                      </div>
                    </div>
                    {getStatusBadge(grp.status)}
                  </div>

                  {/* Pool Amount */}
                  <div>
                    <span className="text-slate-400 text-[11px] block mb-0.5">Total Rotation Payout</span>
                    <div className="text-2xl font-extrabold text-slate-900 font-sans tracking-tight">
                      {formatAmount(totalPool)}
                    </div>
                    <span className="text-[11px] text-slate-500 font-mono block mt-0.5">
                      {formatAmount(grp.contributionAmount)} / member
                    </span>
                  </div>

                  {/* Progress / Member Count */}
                  <div className="flex items-center justify-between text-xs text-slate-600 p-2.5 bg-slate-50 rounded-xl">
                    <div className="flex items-center gap-1.5">
                      <Users size={14} className="text-slate-400" />
                      <span>{grp.totalMembersCount || 0} / {grp.totalPositions || 10} Members</span>
                    </div>
                    <span className="font-semibold text-slate-900">
                      Cycle {grp.currentCycleNumber || 1}
                    </span>
                  </div>
                </div>

                {/* Footer */}
                <div className="pt-4 mt-4 border-t border-slate-100 flex items-center justify-end text-xs">
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      onViewGroup && onViewGroup(grp);
                    }}
                    className="font-semibold text-brand-600 hover:underline flex items-center gap-1"
                  >
                    <span>Inspect Circle</span>
                    <ArrowUpRight size={13} />
                  </button>
                </div>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
