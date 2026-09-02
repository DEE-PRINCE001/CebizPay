import React from 'react';
import { CheckCircle2, AlertCircle, AlertTriangle, Info, X } from 'lucide-react';

/**
 * Floating Notification Toast.
 * Uses design system color tokens from index.css.
 */
export default function Toast({
  id,
  type = 'info',
  title,
  message,
  onDismiss
}) {
  const configs = {
    success: {
      bg: 'bg-white border-emerald-200 text-slate-800 shadow-lg shadow-emerald-500/10',
      icon: CheckCircle2,
      iconColor: 'text-status-success'
    },
    error: {
      bg: 'bg-white border-red-200 text-slate-800 shadow-lg shadow-red-500/10',
      icon: AlertCircle,
      iconColor: 'text-status-danger'
    },
    warning: {
      bg: 'bg-white border-amber-200 text-slate-800 shadow-lg shadow-amber-500/10',
      icon: AlertTriangle,
      iconColor: 'text-status-warning'
    },
    info: {
      bg: 'bg-white border-blue-200 text-slate-800 shadow-lg shadow-blue-500/10',
      icon: Info,
      iconColor: 'text-brand-600'
    }
  };

  const config = configs[type] || configs.info;
  const Icon = config.icon;

  return (
    <div
      className={`flex items-start gap-3 p-4 rounded-2xl border ${config.bg} max-w-sm w-full pointer-events-auto animate-in slide-in-from-top-2 duration-150`}
    >
      <Icon size={18} className={`shrink-0 mt-0.5 ${config.iconColor}`} />
      <div className="flex-1 min-w-0">
        {title && <h5 className="text-xs font-bold text-slate-900 mb-0.5">{title}</h5>}
        <p className="text-xs text-slate-600 leading-relaxed break-words">{message}</p>
      </div>
      {onDismiss && (
        <button
          type="button"
          onClick={() => onDismiss(id)}
          className="text-slate-400 hover:text-slate-700 p-0.5 rounded-lg shrink-0"
        >
          <X size={14} />
        </button>
      )}
    </div>
  );
}
