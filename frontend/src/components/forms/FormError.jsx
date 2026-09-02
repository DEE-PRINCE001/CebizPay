import React from 'react';
import { AlertCircle } from 'lucide-react';

/**
 * Inline Form Validation Error Message.
 */
export default function FormError({ message, className = '' }) {
  if (!message) return null;

  return (
    <div className={`flex items-center gap-1 text-xs text-red-600 font-medium mt-1.5 ${className}`}>
      <AlertCircle size={13} className="shrink-0" />
      <span>{message}</span>
    </div>
  );
}
