import React, { useState, useEffect } from 'react';
import Modal from '../common/Modal';
import PinInput from '../forms/PinInput';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import Skeleton from '../common/Skeleton';
import { ArrowDownLeft, AlertTriangle, CheckCircle2 } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Modal to preview and execute savings account withdrawal/liquidation.
 */
export default function WithdrawSavingsModal({
  isOpen,
  onClose,
  plan,
  onSuccess
}) {
  const { showSuccess } = useToast();

  const [pin, setPin] = useState('');
  const [preview, setPreview] = useState(null);
  const [previewLoading, setPreviewLoading] = useState(true);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (isOpen && plan?.id) {
      setPreviewLoading(true);
      setError(null);
      apiClient
        .post(`/work/savings/${plan.id}/withdraw/preview`)
        .then((res) => setPreview(res))
        .catch((err) => {
          const parsed = err.problemDetails || parseProblemDetails(err);
          setError(parsed.message || 'Unable to calculate withdrawal terms.');
        })
        .finally(() => setPreviewLoading(false));
    }
  }, [isOpen, plan]);

  const handleWithdraw = async (e) => {
    e.preventDefault();
    if (!plan?.id) return;

    if (pin.length < 4) {
      setError('Please enter your 4-digit transaction PIN.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const result = await apiClient.postFinancial(`/work/savings/${plan.id}/withdraw`, {});
      const payout = result?.payoutAmount || preview?.estimatedEarlyWithdrawalNetPayout || plan.principalBalance;

      showSuccess(`Withdrawn ₦${payout.toLocaleString()} to your main wallet.`);
      setPin('');
      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Withdrawal failed. Please check your PIN and try again.');
    } finally {
      setLoading(false);
    }
  };

  const formatAmount = (amt) => {
    return new Intl.NumberFormat('en-NG', {
      style: 'currency',
      currency: 'NGN',
      minimumFractionDigits: 2
    }).format(amt || 0);
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Withdraw from Savings"
      subtitle={`Plan: ${plan?.name || 'Savings Account'}`}
      maxWidth="max-w-md"
    >
      <form onSubmit={handleWithdraw} className="space-y-4 pt-1">
        {error && (
          <Alert variant="danger" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        {previewLoading && (
          <div className="space-y-2 p-2">
            <Skeleton variant="card" />
          </div>
        )}

        {!previewLoading && (
          <>
            {/* Liquidation Breakdown Card */}
            <div className="p-4 bg-slate-50 border border-slate-200/80 rounded-2xl space-y-2.5 text-xs">
              <div className="flex justify-between text-slate-600">
                <span>Principal Balance</span>
                <span className="font-bold text-slate-900">{formatAmount(plan?.principalBalance)}</span>
              </div>

              {preview?.estimatedTotalInterest > 0 && (
                <div className="flex justify-between text-slate-600">
                  <span>Accrued Interest</span>
                  <span className="font-bold text-status-success">+{formatAmount(preview.estimatedTotalInterest)}</span>
                </div>
              )}

              {preview?.estimatedEarlyWithdrawalPenalty > 0 && (
                <div className="flex justify-between text-status-danger font-medium border-t border-slate-100 pt-1.5">
                  <span className="flex items-center gap-1">
                    <AlertTriangle size={12} />
                    <span>Early Exit Penalty</span>
                  </span>
                  <span>-{formatAmount(preview.estimatedEarlyWithdrawalPenalty)}</span>
                </div>
              )}

              <div className="flex justify-between items-center font-bold text-slate-900 border-t border-slate-200 pt-2 text-sm">
                <span>Net Wallet Payout</span>
                <span className="font-mono text-brand-600">
                  {formatAmount(
                    preview?.estimatedEarlyWithdrawalNetPayout ||
                    preview?.estimatedMaturityPayout ||
                    plan?.principalBalance
                  )}
                </span>
              </div>
            </div>

            {/* PIN Authorization */}
            <div className="pt-1">
              <PinInput
                label="Authorize Liquidation with 4-Digit PIN"
                value={pin}
                onChange={(val) => {
                  setPin(val);
                  if (error) setError(null);
                }}
              />
            </div>

            <div className="flex items-center gap-3 pt-3 border-t border-slate-100">
              <Button
                variant="outline"
                size="md"
                onClick={onClose}
                disabled={loading}
                className="flex-1"
              >
                Cancel
              </Button>
              <Button
                type="submit"
                variant="primary"
                size="md"
                loading={loading}
                icon={ArrowDownLeft}
                className="flex-1"
              >
                Liquidate to Wallet
              </Button>
            </div>
          </>
        )}
      </form>
    </Modal>
  );
}
