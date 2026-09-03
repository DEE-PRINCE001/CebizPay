import React, { useState } from 'react';
import Modal from '../common/Modal';
import Tabs from '../common/Tabs';
import Input from '../forms/Input';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { CreditCard, Building, Copy, Check } from 'lucide-react';
import apiClient from '../../services/api/client';
import { parseProblemDetails } from '../../utils/problemDetails';
import { useToast } from '../../hooks/useToast';

/**
 * Wallet funding modal for virtual accounts and card checkout.
 */
export default function FundWalletModal({
  isOpen,
  onClose,
  virtualAccount = null
}) {
  const { showSuccess } = useToast();
  const [fundingType, setFundingType] = useState('virtual');
  const [cardAmount, setCardAmount] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [copied, setCopied] = useState(false);

  const fundingTabs = [
    { id: 'virtual', label: 'Bank Transfer (Auto-Fund)' },
    { id: 'card', label: 'Debit / Credit Card' }
  ];

  const handleCopy = (text, label) => {
    if (!text) return;
    navigator.clipboard.writeText(text);
    setCopied(true);
    showSuccess(`${label} copied to clipboard.`);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleCardFund = async (e) => {
    e.preventDefault();
    const amount = parseFloat(cardAmount);

    if (!amount || amount < 100) {
      setError('Minimum card funding amount is ₦100.00');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await apiClient.post('/funding/card/initialize', {
        amount,
        currency: 'NGN',
        returnUrl: window.location.href
      });

      if (response.checkoutUrl) {
        window.location.href = response.checkoutUrl;
      } else {
        showSuccess('Funding session initialized.');
        onClose();
      }
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to initialize card funding checkout.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Fund Wallet"
      subtitle="Choose your preferred funding method"
      maxWidth="max-w-md"
    >
      <div className="space-y-4 pt-1">
        <Tabs
          variant="segmented"
          tabs={fundingTabs}
          activeTab={fundingType}
          onChange={(tab) => {
            setFundingType(tab);
            setError(null);
          }}
          className="w-full"
        />

        {error && (
          <Alert variant="danger" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        {fundingType === 'virtual' ? (
          <div className="space-y-4 pt-2">
            <div className="p-4 bg-slate-50 border border-slate-200/80 rounded-2xl space-y-3">
              <div className="flex items-center gap-2 text-xs text-slate-500 font-semibold uppercase">
                <Building size={14} className="text-brand-600" />
                <span>Dedicated Virtual Account</span>
              </div>

              <div className="space-y-2">
                <div className="flex justify-between items-center text-xs">
                  <span className="text-slate-500">Bank Name</span>
                  <span className="font-bold text-slate-900">{virtualAccount?.bankName || 'Wema Bank'}</span>
                </div>
                <div className="flex justify-between items-center text-xs">
                  <span className="text-slate-500">Account Number</span>
                  <span className="font-mono font-bold text-base text-slate-900">
                    {virtualAccount?.accountNumber || 'Generating...'}
                  </span>
                </div>
                <div className="flex justify-between items-center text-xs">
                  <span className="text-slate-500">Beneficiary Name</span>
                  <span className="font-medium text-slate-900">{virtualAccount?.accountName || 'CebizPay Wallet'}</span>
                </div>
              </div>

              {virtualAccount?.accountNumber && (
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => handleCopy(virtualAccount.accountNumber, 'Account Number')}
                  className="w-full mt-2 bg-white"
                >
                  {copied ? <Check size={13} className="text-status-success" /> : <Copy size={13} />}
                  <span>{copied ? 'Account Number Copied' : 'Copy Account Number'}</span>
                </Button>
              )}
            </div>

            <p className="text-[11px] text-slate-500 text-center leading-relaxed">
              Transfer funds from any Nigerian mobile banking app or USSD to this account. Your CebizPay wallet will credit automatically within seconds.
            </p>

            <Button
              variant="primary"
              size="md"
              onClick={onClose}
              className="w-full"
            >
              Done
            </Button>
          </div>
        ) : (
          <form onSubmit={handleCardFund} className="space-y-4 pt-2">
            <Input
              label="Funding Amount (₦)"
              type="number"
              step="100"
              min="100"
              placeholder="e.g. 5000.00"
              helperText="Minimum deposit is ₦100.00. Processed via secure payment gateway."
              value={cardAmount}
              onChange={(e) => {
                setCardAmount(e.target.value);
                if (error) setError(null);
              }}
              required
            />

            <Button
              type="submit"
              variant="primary"
              size="md"
              loading={loading}
              icon={CreditCard}
              className="w-full"
            >
              Proceed to Gateway
            </Button>
          </form>
        )}
      </div>
    </Modal>
  );
}
