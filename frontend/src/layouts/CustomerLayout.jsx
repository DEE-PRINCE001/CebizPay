import React from 'react';
import Topbar from '../components/navigation/Topbar';
import CustomerNav from '../components/navigation/CustomerNav';

/**
 * Primary authenticated customer shell layout.
 */
export default function CustomerLayout({
  children,
  title,
  subtitle,
  headerAction,
  className = ''
}) {
  return (
    <div className="min-h-screen bg-canvas-bg flex flex-col">
      {/* Top Header */}
      <Topbar />

      {/* Main Container */}
      <div className="max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-4 sm:py-6 flex-1 flex flex-col space-y-5">
        {/* Navigation Pill Tabs */}
        <div className="border-b border-slate-200/80 pb-2">
          <CustomerNav />
        </div>

        {/* View Header */}
        {(title || headerAction) && (
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2">
            <div>
              {title && <h1 className="text-xl sm:text-2xl font-bold text-slate-900 tracking-tight">{title}</h1>}
              {subtitle && <p className="text-xs text-slate-500 mt-1">{subtitle}</p>}
            </div>
            {headerAction && (
              <div className="flex items-center gap-3 shrink-0">
                {headerAction}
              </div>
            )}
          </div>
        )}

        {/* View Content */}
        <main className={`flex-1 ${className}`}>
          {children}
        </main>
      </div>
    </div>
  );
}
