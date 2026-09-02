import React from 'react';

/**
 * Status and Categorization Pill Badge.
 * Uses design system color tokens from index.css.
 */
export default function Badge({
  children,
  variant = 'neutral',
  size = 'md',
  dot = false,
  className = ''
}) {
  const variants = {
    success: 'bg-status-success-bg text-emerald-700 border border-emerald-200/60',
    danger: 'bg-status-danger-bg text-red-700 border border-red-200/60',
    warning: 'bg-status-warning-bg text-amber-700 border border-amber-200/60',
    info: 'bg-status-info-bg text-blue-700 border border-blue-200/60',
    brand: 'bg-brand-50 text-brand-600 border border-blue-200/60',
    neutral: 'bg-slate-100 text-slate-700 border border-slate-200/60'
  };

  const dotColors = {
    success: 'bg-status-success',
    danger: 'bg-status-danger',
    warning: 'bg-status-warning',
    info: 'bg-status-info',
    brand: 'bg-brand-600',
    neutral: 'bg-slate-500'
  };

  const sizes = {
    sm: 'text-[11px] px-2 py-0.5',
    md: 'text-xs px-2.5 py-0.5',
    lg: 'text-sm px-3 py-1'
  };

  return (
    <span
      className={`inline-flex items-center gap-1.5 font-medium rounded-full ${variants[variant] || variants.neutral} ${sizes[size] || sizes.md} ${className}`}
    >
      {dot && <span className={`w-1.5 h-1.5 rounded-full ${dotColors[variant] || dotColors.neutral}`} />}
      {children}
    </span>
  );
}
