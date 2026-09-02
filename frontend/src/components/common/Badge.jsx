import React from 'react';

const STATUS_CONFIGS = {
  // Common states
  VERIFIED: { bg: 'bg-emerald-50', text: 'text-emerald-700', border: 'border-emerald-200', label: 'Verified' },
  APPROVED: { bg: 'bg-emerald-50', text: 'text-emerald-700', border: 'border-emerald-200', label: 'Approved' },
  ACTIVE: { bg: 'bg-emerald-50', text: 'text-emerald-700', border: 'border-emerald-200', label: 'Active' },
  SUCCESS: { bg: 'bg-emerald-50', text: 'text-emerald-700', border: 'border-emerald-200', label: 'Success' },
  PAID: { bg: 'bg-emerald-50', text: 'text-emerald-700', border: 'border-emerald-200', label: 'Paid' },
  COMPLETED: { bg: 'bg-emerald-50', text: 'text-emerald-700', border: 'border-emerald-200', label: 'Completed' },
  IN_STOCK: { bg: 'bg-emerald-50', text: 'text-emerald-700', border: 'border-emerald-200', label: 'In Stock' },

  PENDING: { bg: 'bg-amber-50', text: 'text-amber-700', border: 'border-amber-200', label: 'Pending' },
  IN_REVIEW: { bg: 'bg-amber-50', text: 'text-amber-700', border: 'border-amber-200', label: 'In Review' },
  UNDER_REVIEW: { bg: 'bg-amber-50', text: 'text-amber-700', border: 'border-amber-200', label: 'Under Review' },
  PROCESSING: { bg: 'bg-amber-50', text: 'text-amber-700', border: 'border-amber-200', label: 'Processing' },
  LOW_STOCK: { bg: 'bg-amber-50', text: 'text-amber-700', border: 'border-amber-200', label: 'Low Stock' },
  EDD_REQUIRED: { bg: 'bg-amber-50', text: 'text-amber-700', border: 'border-amber-200', label: 'EDD Required' },
  DRAFT: { bg: 'bg-slate-100', text: 'text-slate-700', border: 'border-slate-200', label: 'Draft' },

  REJECTED: { bg: 'bg-rose-50', text: 'text-rose-700', border: 'border-rose-200', label: 'Rejected' },
  SUSPENDED: { bg: 'bg-rose-50', text: 'text-rose-700', border: 'border-rose-200', label: 'Suspended' },
  FAILED: { bg: 'bg-rose-50', text: 'text-rose-700', border: 'border-rose-200', label: 'Failed' },
  CANCELLED: { bg: 'bg-rose-50', text: 'text-rose-700', border: 'border-rose-200', label: 'Cancelled' },
  OUT_OF_STOCK: { bg: 'bg-rose-50', text: 'text-rose-700', border: 'border-rose-200', label: 'Out of Stock' },
  OVERDUE: { bg: 'bg-rose-50', text: 'text-rose-700', border: 'border-rose-200', label: 'Overdue' },

  // Tier Levels
  TIER_1: { bg: 'bg-blue-50', text: 'text-blue-700', border: 'border-blue-200', label: 'Tier 1' },
  TIER_2: { bg: 'bg-indigo-50', text: 'text-indigo-700', border: 'border-indigo-200', label: 'Tier 2' },
  TIER_3: { bg: 'bg-purple-50', text: 'text-purple-700', border: 'border-purple-200', label: 'Tier 3 (Full)' },

  // Risk Ratings
  LOW: { bg: 'bg-emerald-50', text: 'text-emerald-700', border: 'border-emerald-200', label: 'Low Risk' },
  MEDIUM: { bg: 'bg-amber-50', text: 'text-amber-700', border: 'border-amber-200', label: 'Medium Risk' },
  HIGH: { bg: 'bg-rose-50', text: 'text-rose-700', border: 'border-rose-200', label: 'High Risk' },
};

export default function Badge({ status, label, variant = 'default', size = 'md', className = '' }) {
  const normalizedKey = String(status || '').toUpperCase().replace(/\s+/g, '_');
  const config = STATUS_CONFIGS[normalizedKey] || {
    bg: 'bg-slate-100',
    text: 'text-slate-700',
    border: 'border-slate-200',
    label: label || status || 'Unknown'
  };

  const displayText = label || config.label || status;

  const sizeClasses = {
    sm: 'text-[11px] px-2 py-0.5',
    md: 'text-xs px-2.5 py-1',
    lg: 'text-sm px-3 py-1.5'
  }[size] || 'text-xs px-2.5 py-1';

  return (
    <span
      className={`inline-flex items-center gap-1.5 font-medium rounded-full border ${config.bg} ${config.text} ${config.border} ${sizeClasses} ${className}`}
    >
      <span className="w-1.5 h-1.5 rounded-full bg-current opacity-70 shrink-0" />
      {displayText}
    </span>
  );
}
