import React from 'react';

/**
 * Standard Form Label with optional required indicator.
 */
export default function Label({
  children,
  htmlFor,
  required = false,
  className = ''
}) {
  return (
    <label
      htmlFor={htmlFor}
      className={`block text-xs font-semibold text-slate-700 mb-1.5 uppercase tracking-wider ${className}`}
    >
      {children}
      {required && <span className="text-red-500 ml-1">*</span>}
    </label>
  );
}
