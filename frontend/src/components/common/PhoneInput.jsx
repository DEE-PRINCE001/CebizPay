import React, { useState, useEffect } from 'react';
import { ChevronDown, Phone } from 'lucide-react';

export const COUNTRY_CODES = [
  { code: '+234', country: 'Nigeria', flag: '🇳🇬', maxDigits: 10, placeholder: '803 123 4567' },
  { code: '+1', country: 'United States / Canada', flag: '🇺🇸', maxDigits: 10, placeholder: '202 555 0123' },
  { code: '+44', country: 'United Kingdom', flag: '🇬🇧', maxDigits: 10, placeholder: '7911 123456' },
  { code: '+233', country: 'Ghana', flag: '🇬🇭', maxDigits: 9, placeholder: '24 123 4567' },
  { code: '+254', country: 'Kenya', flag: '🇰🇪', maxDigits: 9, placeholder: '712 345 678' },
  { code: '+27', country: 'South Africa', flag: '🇿🇦', maxDigits: 9, placeholder: '82 123 4567' },
];

/**
 * Normalizes any phone string into standard E.164 international format (+234...).
 * Strips leading zeros when country code is applied.
 */
export function formatToInternational(countryCode, rawNumber) {
  if (!rawNumber) return '';
  const digits = rawNumber.replace(/\D/g, '');
  if (!digits) return '';

  // If already starts with country code digits without '+', e.g. 23480...
  const rawCode = countryCode.replace('+', '');
  if (digits.startsWith(rawCode)) {
    return `+${digits}`;
  }

  // Strip leading zero if present (e.g. 08031234567 -> 8031234567)
  const cleanLocal = digits.startsWith('0') ? digits.substring(1) : digits;
  return `${countryCode}${cleanLocal}`;
}

export default function PhoneInput({
  value = '',
  onChange,
  label = 'Phone Number',
  required = false,
  disabled = false,
  error = null,
  className = '',
  placeholder = null,
}) {
  // Parse initial country code and local number from value
  const parseValue = (val) => {
    if (!val) return { code: '+234', local: '' };
    const matched = COUNTRY_CODES.find((c) => val.startsWith(c.code));
    if (matched) {
      return { code: matched.code, local: val.substring(matched.code.length) };
    }
    // If starts with 0 or local digits
    const digits = val.replace(/\D/g, '');
    const cleanLocal = digits.startsWith('0') ? digits.substring(1) : digits;
    return { code: '+234', local: cleanLocal };
  };

  const parsed = parseValue(value);
  const [selectedCode, setSelectedCode] = useState(parsed.code);
  const [localNumber, setLocalNumber] = useState(parsed.local);

  useEffect(() => {
    if (value) {
      const p = parseValue(value);
      setSelectedCode(p.code);
      setLocalNumber(p.local);
    }
  }, [value]);

  const handleCountryChange = (e) => {
    const newCode = e.target.value;
    setSelectedCode(newCode);
    const full = formatToInternational(newCode, localNumber);
    onChange(full);
  };

  const handleLocalChange = (e) => {
    const input = e.target.value;
    // Strip non-digit characters
    let digits = input.replace(/\D/g, '');

    // Auto-strip leading 0 if user types 080...
    if (digits.startsWith('0')) {
      digits = digits.substring(1);
    }

    setLocalNumber(digits);
    const full = formatToInternational(selectedCode, digits);
    onChange(full);
  };

  const activeCountry = COUNTRY_CODES.find((c) => c.code === selectedCode) || COUNTRY_CODES[0];

  return (
    <div className={`space-y-1.5 ${className}`}>
      {label && (
        <label className="block font-semibold text-slate-700 text-xs">
          {label} {required && <span className="text-rose-500">*</span>}
        </label>
      )}

      <div className="flex rounded-xl border border-slate-200 bg-white focus-within:border-blue-600 focus-within:ring-2 focus-within:ring-blue-500/20 transition-all overflow-hidden shadow-2xs">
        {/* Country Code Selector */}
        <div className="flex items-center bg-slate-50 border-r border-slate-200 px-3 py-2 shrink-0">
          <span className="text-base mr-1.5">{activeCountry.flag}</span>
          <select
            value={selectedCode}
            onChange={handleCountryChange}
            disabled={disabled}
            className="bg-transparent font-mono text-xs font-bold text-slate-800 outline-hidden cursor-pointer"
          >
            {COUNTRY_CODES.map((c) => (
              <option key={c.code} value={c.code}>
                {c.code} ({c.country})
              </option>
            ))}
          </select>
        </div>

        {/* Local Number Input */}
        <div className="relative flex-1">
          <input
            type="tel"
            required={required}
            disabled={disabled}
            value={localNumber}
            onChange={handleLocalChange}
            placeholder={placeholder || activeCountry.placeholder}
            maxLength={activeCountry.maxDigits}
            className="w-full px-3.5 py-2.5 text-xs font-mono font-bold text-slate-900 bg-transparent placeholder:text-slate-400 placeholder:font-sans placeholder:font-normal outline-hidden"
          />
        </div>
      </div>

      {/* International E.164 Format Preview & Helper */}
      <div className="flex items-center justify-between text-[11px] text-slate-400">
        <span>International E.164 Format:</span>
        <span className="font-mono font-bold text-blue-600">
          {formatToInternational(selectedCode, localNumber) || `${selectedCode}...`}
        </span>
      </div>

      {error && <p className="text-[11px] text-rose-600 font-semibold">{error}</p>}
    </div>
  );
}
