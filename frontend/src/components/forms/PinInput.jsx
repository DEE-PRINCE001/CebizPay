import React, { useRef } from 'react';
import Label from './Label';
import FormError from './FormError';

/**
 * 4-digit Transaction PIN Input with auto-focus jumping and keydown navigation.
 * Uses design system color tokens from index.css.
 */
export default function PinInput({
  label = 'Transaction PIN',
  value = '',
  onChange,
  error,
  length = 4,
  masked = true,
  disabled = false,
  className = ''
}) {
  const inputsRef = useRef([]);

  const pinArray = value.split('').concat(Array(length).fill('')).slice(0, length);

  const handleChange = (e, index) => {
    const val = e.target.value;
    const digit = val.slice(-1);
    if (digit && !/^\d$/.test(digit)) return;

    const newPin = [...pinArray];
    newPin[index] = digit;
    const combined = newPin.join('');
    onChange && onChange(combined);

    if (digit && index < length - 1) {
      inputsRef.current[index + 1]?.focus();
    }
  };

  const handleKeyDown = (e, index) => {
    if (e.key === 'Backspace' && !pinArray[index] && index > 0) {
      inputsRef.current[index - 1]?.focus();
    }
  };

  const handlePaste = (e) => {
    e.preventDefault();
    const pastedData = e.clipboardData.getData('text').trim().slice(0, length);
    if (/^\d+$/.test(pastedData)) {
      onChange && onChange(pastedData);
      const nextFocus = Math.min(pastedData.length, length - 1);
      inputsRef.current[nextFocus]?.focus();
    }
  };

  return (
    <div className={`w-full ${className}`}>
      {label && <Label>{label}</Label>}
      <div className="flex items-center justify-center gap-3" onPaste={handlePaste}>
        {Array.from({ length }).map((_, i) => (
          <input
            key={i}
            ref={(el) => (inputsRef.current[i] = el)}
            type={masked ? 'password' : 'text'}
            inputMode="numeric"
            maxLength={1}
            value={pinArray[i] || ''}
            onChange={(e) => handleChange(e, i)}
            onKeyDown={(e) => handleKeyDown(e, i)}
            disabled={disabled}
            className={`w-12 h-12 text-center text-xl font-bold rounded-xl border transition-all focus:outline-none focus:ring-2 disabled:bg-slate-50 disabled:text-slate-400 ${
              error
                ? 'border-red-300 focus:ring-red-500 bg-red-50/20'
                : 'border-slate-200 focus:ring-brand-600 focus:border-brand-600 bg-white'
            }`}
          />
        ))}
      </div>
      {error && <FormError message={error} className="text-center justify-center mt-2" />}
    </div>
  );
}
