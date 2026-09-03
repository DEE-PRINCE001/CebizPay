import React from 'react';
import { Search, X } from 'lucide-react';

/**
 * Search input field with leading icon and clear button.
 */
export default function SearchInput({
  value,
  onChange,
  onClear,
  placeholder = 'Search...',
  className = '',
  disabled = false,
  ...props
}) {
  return (
    <div className={`relative inline-flex items-center w-full max-w-xs ${className}`}>
      <Search size={15} className="absolute left-3.5 text-slate-400 pointer-events-none" />
      <input
        type="text"
        value={value}
        onChange={onChange}
        disabled={disabled}
        placeholder={placeholder}
        className="w-full pl-9 pr-8 py-2 bg-white border border-slate-200 rounded-full text-xs text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-brand-600 focus:border-transparent transition-all disabled:bg-slate-50"
        {...props}
      />
      {value && onClear && (
        <button
          type="button"
          onClick={onClear}
          className="absolute right-2.5 text-slate-400 hover:text-slate-600 p-0.5 rounded-full"
        >
          <X size={13} />
        </button>
      )}
    </div>
  );
}
