import React from 'react';
import Modal from '../common/Modal';
import Badge from '../common/Badge';
import Button from '../common/Button';
import { PiggyBank, Target, Calendar, TrendingUp, PlusCircle, ArrowDownLeft } from 'lucide-react';

/**
 * Detailed breakdown modal for a specific savings plan.
 */
export default function SavingsPlanDetailModal({
  isOpen,
  onClose,
  plan,
  onDeposit,
  onWithdraw
}) {
  if (!plan) return null;

  const principal = plan.principalBalance || 0;
  const target = plan.targetAmount || principal || 1;
  const progressPercent = Math.min(Math.round((principal / target) * 100), 100);
  const accruedInterest = plan.accruedInterest || 0;

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

  const formatAmount = (amt) => {
    return new Intl.NumberFormat('en-NG', {
      style: 'currency',
      currency: 'NGN',
      minimumFractionDigits: 2
    }).format(amt || 0);
  };

  const getStatusBadge = (status) => {
    const s = (status || '').toLowerCase();
    if (s === 'active') return <Badge variant="success" dot={true}>Active</Badge>;
    if (s === 'matured') return <Badge variant="brand" dot={true}>Matured</Badge>;
    if (s === 'withdrawn' || s === 'liquidated') return <Badge variant="neutral">Liquidated</Badge>;
    return <Badge variant="neutral">{status || 'Active'}</Badge>;
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={plan.name || plan.planType || 'Savings Plan Details'}
      subtitle={`Created on ${formatDate(plan.createdAtUtc || plan.startDateUtc)}`}
      maxWidth="max-w-lg"
    >
      <div className="space-y-5 pt-1">
        {/* Status Header Bar */}
        <div className="flex items-center justify-between p-4 bg-brand-50 border border-brand-100 rounded-2xl">
          <div>
            <span className="text-xs text-brand-600 font-semibold block">Accumulated Balance</span>
            <span className="text-2xl font-extrabold text-brand-900 font-sans">
              {formatAmount(principal)}
            </span>
          </div>
          <div>{getStatusBadge(plan.status)}</div>
        </div>

        {/* Goal Progress Track */}
        {plan.targetAmount && (
          <div className="space-y-1.5">
            <div className="flex justify-between text-xs font-semibold text-slate-700">
              <span>Goal Progress ({progressPercent}%)</span>
              <span className="text-slate-500 font-mono">
                {formatAmount(principal)} of {formatAmount(plan.targetAmount)}
              </span>
            </div>
            <div className="w-full h-2.5 bg-slate-100 rounded-full overflow-hidden">
              <div
                className="h-full bg-brand-600 transition-all duration-500 rounded-full"
                style={{ width: `${progressPercent}%` }}
              />
            </div>
          </div>
        )}

        {/* Breakdown Key-Value Grid */}
        <div className="grid grid-cols-2 gap-3 text-xs">
          <div className="p-3 bg-slate-50 border border-slate-100 rounded-xl space-y-0.5">
            <span className="text-slate-400 block text-[11px]">Accrued Interest</span>
            <span className="font-bold text-status-success font-mono">
              +{formatAmount(accruedInterest)}
            </span>
          </div>

          <div className="p-3 bg-slate-50 border border-slate-100 rounded-xl space-y-0.5">
            <span className="text-slate-400 block text-[11px]">Interest Yield</span>
            <span className="font-bold text-slate-900">
              {plan.interestRateSnapshot || 12.0}% p.a.
            </span>
          </div>

          <div className="p-3 bg-slate-50 border border-slate-100 rounded-xl space-y-0.5">
            <span className="text-slate-400 block text-[11px]">Maturity Date</span>
            <span className="font-bold text-slate-900">
              {formatDate(plan.maturityDateUtc)}
            </span>
          </div>

          <div className="p-3 bg-slate-50 border border-slate-100 rounded-xl space-y-0.5">
            <span className="text-slate-400 block text-[11px]">Contribution Cycle</span>
            <span className="font-bold text-slate-900">
              {plan.contributionFrequency || 'Ad-hoc'}
            </span>
          </div>
        </div>

        {/* Action Buttons */}
        <div className="flex items-center gap-3 pt-2 border-t border-slate-100">
          <Button
            variant="outline"
            size="md"
            icon={ArrowDownLeft}
            onClick={() => {
              onClose();
              if (onWithdraw) onWithdraw(plan);
            }}
            className="flex-1"
          >
            Withdraw
          </Button>
          <Button
            variant="primary"
            size="md"
            icon={PlusCircle}
            onClick={() => {
              onClose();
              if (onDeposit) onDeposit(plan);
            }}
            className="flex-1"
          >
            Top Up Savings
          </Button>
        </div>
      </div>
    </Modal>
  );
}
