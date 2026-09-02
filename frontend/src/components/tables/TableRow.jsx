import React from 'react';

/**
 * Interactive Data Table Row with subtle hover effects.
 */
export default function TableRow({
  children,
  onClick,
  className = '',
  selected = false
}) {
  const hoverStyles = onClick ? 'hover:bg-slate-50/80 cursor-pointer' : 'hover:bg-slate-50/40';
  const selectedStyles = selected ? 'bg-blue-50/40' : '';

  return (
    <tr
      onClick={onClick}
      className={`border-b border-slate-100 transition-colors ${hoverStyles} ${selectedStyles} ${className}`}
    >
      {children}
    </tr>
  );
}
