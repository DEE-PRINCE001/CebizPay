import React, { useState, useEffect, useRef } from 'react';
import Modal from './Modal';
import { Lock, ShieldCheck, AlertCircle } from 'lucide-react';

export default function PinModal({
  isOpen,
  onClose,
  onConfirm,
  title = 'Authorize Financial Transaction',
  description = 'Enter your 4-digit Transaction PIN to authenticate and authorize this operation.',
  amount = null,
  recipient = null,
  isLoading = false
}) {
  const [pin, setPin] = useState(['', '', '', '']);
  const [error, setError] = useState('');
  const inputRefs = [useRef(null), useRef(null), useRef(null), useRef(null)];

  useEffect(() => {
    if (isOpen) {
      setPin(['', '', '', '']);
      setError('');
      setTimeout(() => {
        inputRefs[0].current?.focus();
      }, 100);
    }
  }, [isOpen]);

  const handleChange = (index, value) => {
    if (value.length > 1) {
      // Handle paste
      const digits = value.replace(/\D/g, '').slice(0, 4).split('');
      const newPin = [...pin];
      digits.forEach((d, i) => {
        if (i < 4) newPin[i] = d;
      });
      setPin(newPin);
      if (digits.length === 4) {
        inputRefs[3].current?.focus();
      }
      return;
    }

    const digit = value.replace(/\D/g, '');
    const newPin = [...pin];
    newPin[index] = digit;
    setPin(newPin);

    // Auto-advance
    if (digit && index < 3) {
      inputRefs[index + 1].current?.focus();
    }
  };

  const handleKeyDown = (index, e) => {
    if (e.key === 'Backspace' && !pin[index] && index > 0) {
      inputRefs[index - 1].current?.focus();
    }
    if (e.key === 'Enter') {
      handleSubmit();
    }
  };

  const handleSubmit = () => {
    const fullPin = pin.join('');
    if (fullPin.length !== 4) {
      setError('Please enter all 4 digits of your transaction PIN.');
      return;
    }
    setError('');
    onConfirm(fullPin);
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={title}
      maxWidth="max-w-md"
      footer={
        <div className="flex items-center justify-end gap-3 w-full">
          <button
            type="button"
            onClick={onClose}
            disabled={isLoading}
            className="px-4 py-2 text-sm font-medium text-slate-700 bg-white border border-slate-300 rounded-xl hover:bg-slate-50 transition-colors disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={handleSubmit}
            disabled={isLoading || pin.join('').length !== 4}
            className="px-5 py-2 text-sm font-semibold text-white bg-blue-600 rounded-xl hover:bg-blue-700 transition-colors flex items-center gap-2 shadow-xs disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {isLoading ? (
              <>
                <span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                Authenticating...
              </>
            ) : (
              <>
                <ShieldCheck className="w-4 h-4" />
                Authorize Action
              </>
            )}
          </button>
        </div>
      }
    >
      <div className="text-center py-2">
        <div className="w-12 h-12 rounded-2xl bg-blue-50 text-blue-600 flex items-center justify-center mx-auto mb-4 border border-blue-100">
          <Lock className="w-6 h-6" />
        </div>

        <p className="text-sm text-slate-600 mb-6">{description}</p>

        {(amount || recipient) && (
          <div className="bg-slate-50 rounded-xl p-3.5 mb-6 text-left border border-slate-200/80 text-xs flex flex-col gap-1.5">
            {amount && (
              <div className="flex justify-between">
                <span className="text-slate-500">Transaction Amount:</span>
                <span className="font-semibold text-slate-900">{amount}</span>
              </div>
            )}
            {recipient && (
              <div className="flex justify-between">
                <span className="text-slate-500">Beneficiary / Recipient:</span>
                <span className="font-semibold text-slate-900 truncate max-w-[200px]">{recipient}</span>
              </div>
            )}
          </div>
        )}

        {/* 4-digit PIN inputs */}
        <div className="flex items-center justify-center gap-3 my-4">
          {pin.map((digit, idx) => (
            <input
              key={idx}
              ref={inputRefs[idx]}
              type="password"
              inputMode="numeric"
              maxLength={1}
              value={digit}
              onChange={(e) => handleChange(idx, e.target.value)}
              onKeyDown={(e) => handleKeyDown(idx, e)}
              className="w-12 h-14 text-center text-2xl font-bold rounded-xl border border-slate-300 focus:border-blue-600 focus:ring-2 focus:ring-blue-500/20 outline-hidden transition-all bg-slate-50/50"
            />
          ))}
        </div>

        {error && (
          <div className="flex items-center justify-center gap-1.5 text-xs text-rose-600 mt-3 font-medium">
            <AlertCircle className="w-4 h-4" />
            {error}
          </div>
        )}

        <p className="text-[11px] text-slate-400 mt-4">
          Protected by platform-wide Argon2 PIN verification &amp; 3-attempt lockout security.
        </p>
      </div>
    </Modal>
  );
}
