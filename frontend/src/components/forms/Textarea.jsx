import React, { forwardRef } from 'react';
import Label from './Label';
import FormError from './FormError';

/**
 * Standard Form Textarea.
 * Uses design system color tokens from index.css.
 */
const Textarea = forwardRef(function Textarea({
  label,
  id,
  name,
  rows = 3,
  placeholder,
  value,
  onChange,
  onBlur,
  error,
  helperText,
  required = false,
  disabled = false,
  className = '',
  ...props
}, ref) {
  const textareaId = id || name;

  const errorStyles = error
    ? 'border-red-300 focus:ring-red-500 focus:border-red-500 bg-red-50/20'
    : 'border-slate-200 focus:ring-brand-600 focus:border-brand-600 bg-white';

  return (
    <div className="w-full">
      {label && (
        <Label htmlFor={textareaId} required={required}>
          {label}
        </Label>
      )}
      <textarea
        ref={ref}
        id={textareaId}
        name={name}
        rows={rows}
        value={value}
        onChange={onChange}
        onBlur={onBlur}
        disabled={disabled}
        placeholder={placeholder}
        required={required}
        className={`w-full p-4 rounded-xl border text-sm text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-offset-0 transition-all disabled:bg-slate-50 disabled:text-slate-400 disabled:cursor-not-allowed ${errorStyles} ${className}`}
        {...props}
      />
      {error && <FormError message={error} />}
      {!error && helperText && (
        <p className="text-xs text-slate-500 mt-1">{helperText}</p>
      )}
    </div>
  );
});

export default Textarea;
