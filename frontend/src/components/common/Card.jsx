import React from 'react';

/**
 * Standard Surface Card with rounded-2xl (16px) corners and subtle elevation.
 */
export default function Card({
  children,
  className = '',
  padding = 'p-6',
  header,
  footer,
  onClick,
  hover = false,
  ...props
}) {
  const hoverStyles = hover ? 'hover:shadow-md hover:border-slate-200 transition-all cursor-pointer' : '';

  return (
    <div
      className={`bg-white rounded-2xl border border-slate-100 shadow-[0_2px_12px_rgba(0,0,0,0.04)] ${hoverStyles} ${className}`}
      onClick={onClick}
      {...props}
    >
      {header && (
        <div className="px-6 py-4 border-b border-slate-100 flex items-center justify-between">
          {header}
        </div>
      )}
      <div className={padding}>
        {children}
      </div>
      {footer && (
        <div className="px-6 py-3.5 bg-slate-50/50 rounded-b-2xl border-t border-slate-100">
          {footer}
        </div>
      )}
    </div>
  );
}
