import React from 'react';
import Badge from '../common/Badge';
import { ShieldCheck, ShieldAlert, Shield } from 'lucide-react';

/**
 * Compliance verification tier and status badge.
 */
export default function ComplianceStatusBadge({
  tier = 'Tier1',
  status = 'Verified',
  className = ''
}) {
  const getTierLabel = (t) => {
    if (t === 'Tier3' || t === 3) return 'Tier 3 (Uncapped)';
    if (t === 'Tier2' || t === 2) return 'Tier 2 (Standard)';
    return 'Tier 1 (Basic)';
  };

  const getBadgeVariant = (s) => {
    const st = (s || '').toLowerCase();
    if (st === 'verified' || st === 'approved') return 'success';
    if (st === 'pending' || st === 'in_review') return 'warning';
    if (st === 'rejected' || st === 'restricted') return 'danger';
    return 'neutral';
  };

  return (
    <div className={`inline-flex items-center gap-2 ${className}`}>
      <div className="flex items-center gap-1 text-xs font-bold text-slate-900 bg-slate-100 px-2.5 py-1 rounded-lg">
        <Shield size={13} className="text-brand-600" />
        <span>{getTierLabel(tier)}</span>
      </div>
      <Badge variant={getBadgeVariant(status)} dot={true}>
        {status || 'Verified'}
      </Badge>
    </div>
  );
}
