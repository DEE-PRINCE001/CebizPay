import React from 'react';
import { CheckCircle2, AlertTriangle, AlertCircle, Info, X } from 'lucide-react';

/**
 * Inline Status Alert Banner.
 * Uses design system color tokens from index.css.
 */
export default function Alert({
  variant = 'info',
  title,
  children,
  onClose,
  action,
  className = ''
}) {
  const configs = {
    success: {
      bg: 'bg-status-success-bg border-emerald-200/80 text-emerald-900',
      icon: CheckCircle2,
      iconColor: 'text-status-success'
    },
    danger: {
      bg: 'bg-status-danger-bg border-red-200/80 text-red-900',
      icon: AlertCircle,
      iconColor: 'text-status-danger'
    },
    warning: {
      bg: 'bg-status-warning-bg border-amber-200/80 text-amber-900',
      icon: AlertTriangle,
      iconColor: 'text-status-warning'
    },
    info: {
      bg: 'bg-status-info-bg border-blue-200/80 text-blue-900',
      icon: Info,
      iconColor: 'text-brand-600'
    }
  };

  const config = configs[variant] || configs.info;
  const Icon = config.icon;

  return (
    <div className={`flex items-start gap-3 p-4 rounded-2xl border text-xs leading-relaxed ${config.bg} ${className}`}>
      <Icon size={18} className={`shrink-0 mt-0.5 ${config.iconColor}`} />
      <div className="flex-1">
        {title && <h4 className="font-bold text-sm mb-0.5">{title}</h4>}
        <div>{children}</div>
        {action && <div className="mt-2.5">{action}</div>}
      </div>
      {onClose && (
        <button
          type="button"
          onClick={onClose}
          className="text-slate-400 hover:text-slate-700 p-0.5 rounded-lg"
          aria-label="Dismiss alert"
        >
          <X size={14} />
        </button>
      )}
    </div>
  );
}
