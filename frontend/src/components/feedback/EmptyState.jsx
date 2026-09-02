import React from 'react';
import { Inbox } from 'lucide-react';
import Button from '../common/Button';

/**
 * Empty Collection Placeholder Card for 0-record states.
 */
export default function EmptyState({
  icon: Icon = Inbox,
  title = 'No records found',
  description = 'There are currently no items in this section.',
  actionLabel,
  onAction,
  actionIcon,
  className = ''
}) {
  return (
    <div className={`flex flex-col items-center justify-center p-8 text-center bg-white rounded-2xl border border-slate-100 shadow-[0_2px_12px_rgba(0,0,0,0.04)] ${className}`}>
      <div className="w-14 h-14 rounded-full bg-slate-50 border border-slate-100 flex items-center justify-center text-slate-400 mb-4">
        <Icon size={24} strokeWidth={1.5} />
      </div>
      <h3 className="text-base font-bold text-slate-900 mb-1">{title}</h3>
      <p className="text-xs text-slate-500 max-w-sm mb-5">{description}</p>
      {actionLabel && onAction && (
        <Button
          variant="primary"
          size="sm"
          onClick={onAction}
          icon={actionIcon}
        >
          {actionLabel}
        </Button>
      )}
    </div>
  );
}
