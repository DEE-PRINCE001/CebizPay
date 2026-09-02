import React, { forwardRef } from 'react';
import { ChevronDown } from 'lucide-react';
import Label from './Label';
import FormError from './FormError';

/**
 * Standard Form Select Dropdown with custom chevron.
 * Uses design system color tokens from index.css.
 */
const Select = forwardRef(function Select({
  label,
  id,
  name,
  options = [],
  value,
  onChange,
  onBlur,
  error,
  helperText,
  required = false,
  disabled = false,
  placeholder = 'Select an option',
  className = '',
  ...props
}, ref) {
  const selectId = id || name;

  const errorStyles = error
    ? 'border-red-300 focus:ring-red-500 focus:border-red-500 bg-red-50/20'
    : 'border-slate-200 focus:ring-brand-600 focus:border-brand-600 bg-white';

  return (
    <div className="w-full">
      {label && (
        <Label htmlFor={selectId} required={required}>
          {label}
        </Label>
      )}
      <div className="relative">
        <select
          ref={ref}
          id={selectId}
          name={name}
          value={value}
          onChange={onChange}
          onBlur={onBlur}
          disabled={disabled}
          required={required}
          className={`w-full py-2.5 pl-4 pr-10 rounded-xl border text-sm text-slate-900 appearance-none focus:outline-none focus:ring-2 focus:ring-offset-0 transition-all disabled:bg-slate-50 disabled:text-slate-400 disabled:cursor-not-allowed ${errorStyles} ${className}`}
          {...props}
        >
          {placeholder && (
            <option value="" disabled>
              {placeholder}
            </option>
          )}
          {options.map((opt) => {
            const optVal = typeof opt === 'object' ? opt.value : opt;
            const optLabel = typeof opt === 'object' ? opt.label : opt;
            return (
              <option key={optVal} value={optVal}>
                {optLabel}
              </option>
            );
          })}
        </select>
        <div className="absolute right-3.5 top-1/2 -translate-y-1/2 text-slate-400 pointer-events-none">
          <ChevronDown size={16} />
        </div>
      </div>
      {error && <FormError message={error} />}
      {!error && helperText && (
        <p className="text-xs text-slate-500 mt-1">{helperText}</p>
      )}
    </div>
  );
});

export default Select;
