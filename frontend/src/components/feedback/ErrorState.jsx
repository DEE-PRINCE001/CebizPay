import React from 'react';
import { AlertCircle, RefreshCw } from 'lucide-react';
import Button from '../common/Button';

/**
 * Failed Request Error Card with retry button.
 */
export default function ErrorState({
  title = 'Failed to load data',
  message = 'An error occurred while fetching information from the server.',
  onRetry,
  className = ''
}) {
  return (
    <div className={`flex flex-col items-center justify-center p-8 text-center bg-red-50/30 rounded-2xl border border-red-100 ${className}`}>
      <div className="w-12 h-12 rounded-full bg-red-100 flex items-center justify-center text-red-600 mb-3">
        <AlertCircle size={22} />
      </div>
      <h3 className="text-sm font-bold text-slate-900 mb-1">{title}</h3>
      <p className="text-xs text-slate-500 max-w-sm mb-4">{message}</p>
      {onRetry && (
        <Button
          variant="outline"
          size="sm"
          onClick={onRetry}
          icon={RefreshCw}
          className="bg-white"
        >
          Try Again
        </Button>
      )}
    </div>
  );
}
