import React from 'react';

/**
 * Data Table Header with muted uppercase typography.
 */
export default function TableHeader({
  columns = [],
  children,
  className = ''
}) {
  if (children) {
    return (
      <thead className={`border-b border-slate-100 ${className}`}>
        {children}
      </thead>
    );
  }

  return (
    <thead className={`border-b border-slate-100 ${className}`}>
      <tr>
        {columns.map((col, idx) => (
          <th
            key={col.key || idx}
            className={`py-3.5 px-4 text-xs font-semibold text-slate-500 uppercase tracking-wider ${col.align === 'right' ? 'text-right' : col.align === 'center' ? 'text-center' : 'text-left'} ${col.className || ''}`}
          >
            {col.label}
          </th>
        ))}
      </tr>
    </thead>
  );
}
