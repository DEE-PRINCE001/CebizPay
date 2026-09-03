import React from 'react';
import Card from '../common/Card';
import Badge from '../common/Badge';
import Skeleton from '../common/Skeleton';
import EmptyState from '../feedback/EmptyState';
import ErrorState from '../feedback/ErrorState';
import Button from '../common/Button';
import { PiggyBank, Target, Plus, ArrowUpRight, TrendingUp, Calendar } from 'lucide-react';

/**
 * Savings plans visual grid list.
 */
export default function SavingsPlanList({
  plans = [],
  loading = false,
  error = null,
  onRetry,
  onViewPlan,
  onCreatePlan,
  className = ''
}) {
  const getStatusBadge = (status) => {
    const s = (status || '').toLowerCase();
    if (s === 'active') return <Badge variant="success" dot={true}>Active</Badge>;
    if (s === 'matured') return <Badge variant="brand" dot={true}>Matured</Badge>;
    if (s === 'liquidated' || s === 'withdrawn') return <Badge variant="neutral">Liquidated</Badge>;
    return <Badge variant="neutral">{status || 'Active'}</Badge>;
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

  return (
    <div className={className}>
      {loading && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <Skeleton variant="card" count={3} />
        </div>
      )}

      {!loading && error && (
        <ErrorState
          title="Failed to load savings plans"
          message={error.message || 'Unable to retrieve your savings portfolio.'}
          onRetry={onRetry}
        />
      )}

      {!loading && !error && plans.length === 0 && (
        <EmptyState
          icon={PiggyBank}
          title="No active savings plans"
          description="Start building your savings by setting up a target or fixed locked savings scheme with automated interest."
          actionLabel="Create Savings Plan"
          onAction={onCreatePlan}
        />
      )}

      {!loading && !error && plans.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          {plans.map((plan) => {
            const principal = plan.principalBalance || 0;
            const target = plan.targetAmount || principal || 1;
            const progress = Math.min(Math.round((principal / target) * 100), 100);

            return (
              <Card
                key={plan.id}
                hoverEffect={true}
                padding="p-5"
                onClick={() => onViewPlan && onViewPlan(plan)}
                className="flex flex-col justify-between cursor-pointer group bg-white border border-slate-200/80"
              >
                <div className="space-y-4">
                  {/* Top Header */}
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-2xl bg-brand-50 text-brand-600 flex items-center justify-center shrink-0">
                        <PiggyBank size={20} />
                      </div>
                      <div className="min-w-0">
                        <h4 className="font-bold text-sm text-slate-900 truncate group-hover:text-brand-600 transition">
                          {plan.name || plan.planType || 'Target Savings'}
                        </h4>
                        <span className="text-[11px] text-slate-400 block">
                          Matures: {formatDate(plan.maturityDateUtc)}
                        </span>
                      </div>
                    </div>
                    {getStatusBadge(plan.status)}
                  </div>

                  {/* Balance & Interest */}
                  <div>
                    <span className="text-slate-400 text-[11px] block mb-0.5">Saved Balance</span>
                    <div className="text-2xl font-extrabold text-slate-900 font-sans tracking-tight">
                      {formatAmount(principal)}
                    </div>
                    {plan.targetAmount && (
                      <span className="text-[11px] text-slate-500 font-mono block mt-0.5">
                        Target: {formatAmount(plan.targetAmount)}
                      </span>
                    )}
                  </div>

                  {/* Progress Bar */}
                  {plan.targetAmount && (
                    <div className="space-y-1">
                      <div className="flex justify-between text-[11px] text-slate-500 font-medium">
                        <span>{progress}% Achieved</span>
                        <span>{formatAmount(principal)}</span>
                      </div>
                      <div className="w-full h-2 bg-slate-100 rounded-full overflow-hidden">
                        <div
                          className="h-full bg-brand-600 rounded-full transition-all duration-500"
                          style={{ width: `${progress}%` }}
                        />
                      </div>
                    </div>
                  )}
                </div>

                {/* Footer Yield Info */}
                <div className="pt-4 mt-4 border-t border-slate-100 flex items-center justify-between text-xs">
                  <div className="flex items-center gap-1.5 text-status-success font-semibold">
                    <TrendingUp size={14} />
                    <span>{plan.interestRateSnapshot || 12.0}% p.a.</span>
                  </div>
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      onViewPlan && onViewPlan(plan);
                    }}
                    className="font-semibold text-brand-600 hover:underline flex items-center gap-1"
                  >
                    <span>Manage</span>
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
