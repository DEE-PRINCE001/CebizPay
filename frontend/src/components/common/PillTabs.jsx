import React from 'react';

/**
 * Topbar & Section Pill Tabs as observed in Dashboard.png (D085) and Staff.png (D331).
 * Uses design system color tokens from index.css.
 */
export default function PillTabs({
  tabs = [],
  activeTab,
  onChange,
  className = ''
}) {
  return (
    <div className={`flex items-center gap-2 overflow-x-auto no-scrollbar py-1 ${className}`}>
      {tabs.map((tab) => {
        const isActive = activeTab === tab.id;
        const Icon = tab.icon;

        return (
          <button
            key={tab.id}
            type="button"
            onClick={() => onChange && onChange(tab.id)}
            className={`inline-flex items-center gap-2 px-4 py-2 rounded-full text-sm font-medium transition-all whitespace-nowrap select-none ${
              isActive
                ? 'bg-brand-600 text-white shadow-xs shadow-brand-500/20'
                : 'bg-white border border-slate-200 text-slate-700 hover:bg-slate-50 hover:border-slate-300'
            }`}
          >
            {Icon && <Icon size={16} strokeWidth={isActive ? 2 : 1.75} />}
            <span>{tab.label}</span>
            {tab.badge !== undefined && (
              <span
                className={`text-xs px-2 py-0.5 rounded-full font-semibold ${
                  isActive
                    ? 'bg-white/20 text-white'
                    : 'bg-slate-100 text-slate-600'
                }`}
              >
                {tab.badge}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}
