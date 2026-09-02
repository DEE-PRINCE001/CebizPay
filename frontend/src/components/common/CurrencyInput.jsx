import React from 'react';

export default function CurrencyInput({
  value,
  onChange,
  currency = 'NGN',
  placeholder = '0.00',
  disabled = false,
  required = false,
  className = '',
  label = null,
  error = null,
  helperText = null,
}) {
  let symbol = '₦';
  if (currency === 'USD') symbol = '$';
  else if (currency === 'EUR') symbol = '€';
  else if (currency === 'GBP') symbol = '£';
  else if (currency === 'USDT') symbol = '₮';

  const handleChange = (e) => {
    const raw = e.target.value;
    // Allow numbers and decimal point
    const clean = raw.replace(/[^0-9.]/g, '');
    if ((clean.match(/\./g) || []).length > 1) return;
    onChange(clean);
  };

  return (
    <div className={`w-full ${className}`}>
      {label && (
        <label className="block text-xs font-semibold text-slate-700 mb-1.5">
          {label} {required && <span className="text-rose-500">*</span>}
        </label>
      )}
      <div className="relative">
        <div className="absolute inset-y-0 left-0 pl-3.5 flex items-center pointer-events-none text-slate-400 font-bold text-sm">
          {symbol}
        </div>
        <input
          type="text"
          inputMode="decimal"
          value={value}
          onChange={handleChange}
          disabled={disabled}
          placeholder={placeholder}
          className={`w-full pl-8 pr-4 py-2.5 text-sm font-semibold text-slate-900 bg-white border rounded-xl focus:ring-2 focus:ring-blue-500/20 outline-hidden transition-all placeholder:text-slate-300 ${
            error
              ? 'border-rose-300 focus:border-rose-500'
              : 'border-slate-200 focus:border-blue-600'
          } ${disabled ? 'bg-slate-50 text-slate-400 cursor-not-allowed' : ''}`}
        />
        <div className="absolute inset-y-0 right-0 pr-3 flex items-center pointer-events-none text-xs font-semibold text-slate-400">
          {currency}
        </div>
      </div>
      {error ? (
        <p className="text-[11px] text-rose-500 mt-1">{error}</p>
      ) : helperText ? (
        <p className="text-[11px] text-slate-400 mt-1">{helperText}</p>
      ) : null}
    </div>
  );
}
