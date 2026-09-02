import React from 'react';

/**
 * Split Metric Badge as observed in Invoice generator.png (D251).
 * Uses design system color tokens from index.css.
 */
export default function SplitBadge({
  label,
  count,
  active = false,
  onClick,
  className = ''
}) {
  const containerStyles = active
    ? 'border border-brand-500/30 ring-2 ring-brand-500/20'
    : 'border border-slate-200 hover:border-slate-300';

  const leftBg = active
    ? 'bg-brand-600 text-white'
    : 'bg-slate-100 text-slate-700';

  const rightBg = active
    ? 'bg-brand-100 text-brand-600'
    : 'bg-slate-200/70 text-slate-800';

  return (
    <button
      type="button"
      onClick={onClick}
      className={`inline-flex items-stretch rounded-xl overflow-hidden text-xs transition-all select-none ${containerStyles} ${className}`}
    >
      <span className={`px-3.5 py-1.5 font-medium ${leftBg}`}>
        {label}
      </span>
      <span className={`px-2.5 py-1.5 font-bold ${rightBg}`}>
        {count}
      </span>
    </button>
  );
}
