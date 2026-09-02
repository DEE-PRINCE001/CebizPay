import React from 'react';
import Card from './Card';

/**
 * Metric Stat Widget as observed in Dashboard.png (D085).
 * Features circular icon container, muted uppercase label, bold count, and optional trend tag.
 */
export default function StatCard({
  icon: Icon,
  label,
  value,
  trend,
  trendType = 'positive',
  className = '',
  onClick
}) {
  return (
    <Card padding="p-5" className={`flex flex-col justify-between ${className}`} onClick={onClick} hover={!!onClick}>
      <div className="flex items-center justify-between mb-3">
        {Icon && (
          <div className="w-10 h-10 rounded-full bg-slate-100 flex items-center justify-center text-slate-700">
            <Icon size={18} strokeWidth={1.75} />
          </div>
        )}
        {trend && (
          <span
            className={`text-xs font-semibold px-2 py-0.5 rounded-full ${
              trendType === 'positive'
                ? 'bg-emerald-50 text-emerald-700'
                : 'bg-red-50 text-red-700'
            }`}
          >
            {trend}
          </span>
        )}
      </div>
      <div>
        <p className="text-xs font-medium text-slate-500 mb-1">{label}</p>
        <p className="text-2xl font-bold text-slate-900 tracking-tight">{value}</p>
      </div>
    </Card>
  );
}
