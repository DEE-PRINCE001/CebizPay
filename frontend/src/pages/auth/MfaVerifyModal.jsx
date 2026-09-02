import React, { useState } from 'react';
import { ShieldCheck, ArrowLeft } from 'lucide-react';
import Modal from '../../components/common/Modal';
import Button from '../../components/common/Button';
import FormError from '../../components/forms/FormError';
import Alert from '../../components/feedback/Alert';

/**
 * 6-digit MFA / Two-Factor Authentication Verification Modal.
 */
export default function MfaVerifyModal({
  isOpen,
  onClose,
  onVerify,
  email,
  loading = false,
  error = null
}) {
  const [code, setCode] = useState('');

  const handleSubmit = (e) => {
    e.preventDefault();
    if (code.length >= 6) {
      onVerify(code);
    }
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} maxWidth="max-w-sm" showClose={!loading}>
      <form onSubmit={handleSubmit} className="flex flex-col items-center text-center p-2">
        <div className="w-14 h-14 rounded-full bg-brand-50 text-brand-600 flex items-center justify-center mb-4 ring-8 ring-brand-50/50">
          <ShieldCheck size={28} />
        </div>

        <h3 className="text-lg font-bold text-slate-900 mb-1">Two-Factor Authentication</h3>
        <p className="text-xs text-slate-500 leading-relaxed mb-6">
          Enter the 6-digit verification code from your authenticator app or email for{' '}
          <span className="font-semibold text-slate-700">{email || 'your account'}</span>.
        </p>

        {error && (
          <Alert variant="danger" className="w-full mb-4 text-left">
            {error}
          </Alert>
        )}

        <div className="w-full mb-6">
          <input
            type="text"
            inputMode="numeric"
            autoFocus
            maxLength={6}
            value={code}
            onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
            placeholder="000000"
            className="w-full h-14 text-center text-2xl font-bold tracking-[0.4em] rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-brand-600 focus:border-brand-600 transition-all bg-white"
          />
        </div>

        <div className="flex items-center gap-3 w-full">
          <Button
            variant="outline"
            size="md"
            onClick={onClose}
            disabled={loading}
            icon={ArrowLeft}
            className="flex-1"
          >
            Back
          </Button>
          <Button
            type="submit"
            variant="primary"
            size="md"
            loading={loading}
            disabled={code.length < 6}
            className="flex-1"
          >
            Verify
          </Button>
        </div>
      </form>
    </Modal>
  );
}
