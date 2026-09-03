import React, { useState, useRef, useEffect } from 'react';
import { Filter, ChevronDown, Check } from 'lucide-react';
import Button from '../common/Button';

/**
 * Filter popover menu for data tables.
 */
export default function TableFilter({
  options = [],
  selectedValues = [],
  onSelect,
  onReset,
  label = 'Filter',
  className = ''
}) {
  const [isOpen, setIsOpen] = useState(false);
  const popoverRef = useRef(null);

  useEffect(() => {
    function handleClickOutside(event) {
      if (popoverRef.current && !popoverRef.current.contains(event.target)) {
        setIsOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const hasActiveFilters = selectedValues.length > 0;

  return (
    <div className={`relative inline-block text-left ${className}`} ref={popoverRef}>
      <Button
        variant="primary"
        size="sm"
        onClick={() => setIsOpen(!isOpen)}
        className="gap-1.5"
      >
        <Filter size={14} />
        <span>{label}</span>
        {hasActiveFilters && (
          <span className="w-2 h-2 rounded-full bg-white" />
        )}
        <ChevronDown size={14} />
      </Button>

      {isOpen && (
        <div className="absolute right-0 mt-2 w-56 rounded-2xl bg-white shadow-xl border border-slate-100 p-3 z-30 animate-in fade-in zoom-in-95">
          <div className="flex items-center justify-between pb-2 mb-2 border-b border-slate-100">
            <span className="text-xs font-semibold text-slate-700">Filter By</span>
            {hasActiveFilters && onReset && (
              <button
                type="button"
                onClick={() => {
                  onReset();
                  setIsOpen(false);
                }}
                className="text-[11px] text-brand-600 hover:underline font-medium"
              >
                Reset
              </button>
            )}
          </div>
          <div className="space-y-1 max-h-60 overflow-y-auto">
            {options.map((opt) => {
              const isSelected = selectedValues.includes(opt.value);
              return (
                <button
                  key={opt.value}
                  type="button"
                  onClick={() => {
                    onSelect && onSelect(opt.value);
                  }}
                  className={`w-full flex items-center justify-between px-3 py-2 text-xs rounded-xl transition-colors ${
                    isSelected
                      ? 'bg-brand-50 text-brand-600 font-medium'
                      : 'text-slate-700 hover:bg-slate-50'
                  }`}
                >
                  <span>{opt.label}</span>
                  {isSelected && <Check size={14} className="text-brand-600" />}
                </button>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
