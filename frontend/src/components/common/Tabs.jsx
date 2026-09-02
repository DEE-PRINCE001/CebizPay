import React from 'react';

/**
 * Standard Segmented / Underlined Tabs.
 * Uses design system color tokens from index.css.
 */
export default function Tabs({
  tabs = [],
  activeTab,
  onChange,
  variant = 'underline',
  className = ''
}) {
  if (variant === 'segmented') {
    return (
      <div className={`inline-flex p-1 bg-slate-100 rounded-xl ${className}`}>
        {tabs.map((tab) => {
          const isActive = activeTab === tab.id;
          return (
            <button
              key={tab.id}
              type="button"
              onClick={() => onChange && onChange(tab.id)}
              className={`px-4 py-1.5 text-xs font-medium rounded-lg transition-all ${
                isActive
                  ? 'bg-white text-slate-900 shadow-xs'
                  : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              {tab.label}
            </button>
          );
        })}
      </div>
    );
  }

  return (
    <div className={`flex border-b border-slate-200 gap-6 ${className}`}>
      {tabs.map((tab) => {
        const isActive = activeTab === tab.id;
        return (
          <button
            key={tab.id}
            type="button"
            onClick={() => onChange && onChange(tab.id)}
            className={`pb-3 text-sm font-medium transition-all relative ${
              isActive
                ? 'text-brand-600 font-semibold'
                : 'text-slate-500 hover:text-slate-800'
            }`}
          >
            {tab.label}
            {isActive && (
              <span className="absolute bottom-0 left-0 right-0 h-0.5 bg-brand-600 rounded-full" />
            )}
          </button>
        );
      })}
    </div>
  );
}
