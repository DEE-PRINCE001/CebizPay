import React, { useState, useEffect } from 'react';
import Card from '../common/Card';
import Input from '../forms/Input';
import PinInput from '../forms/PinInput';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import SuccessModal from '../feedback/SuccessModal';
import { Phone, Zap, Smartphone, Check } from 'lucide-react';
import apiClient from '../../services/api/client';
import { parseProblemDetails } from '../../utils/problemDetails';
import { useToast } from '../../hooks/useToast';

const NETWORKS = [
  { id: 'MTN', name: 'MTN', color: 'bg-amber-400 text-slate-900' },
  { id: 'AIRTEL', name: 'Airtel', color: 'bg-red-500 text-white' },
  { id: 'GLO', name: 'Glo', color: 'bg-emerald-600 text-white' },
  { id: '9MOBILE', name: '9mobile', color: 'bg-lime-700 text-white' }
];

const QUICK_AMOUNTS = [100, 200, 500, 1000, 2000, 5000];

/**
 * Mobile airtime recharge form with operator auto-detection.
 */
export default function AirtimeForm({ onSuccess, className = '' }) {
  const { showSuccess } = useToast();

  const [phoneNumber, setPhoneNumber] = useState('');
  const [network, setNetwork] = useState('MTN');
  const [amount, setAmount] = useState('');
  const [pin, setPin] = useState('');

  const [detecting, setDetecting] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [successData, setSuccessData] = useState(null);

  // Auto-detect network operator when 11 digits are entered
  useEffect(() => {
    const cleanNumber = phoneNumber.replace(/\D/g, '');
    if (cleanNumber.length === 11) {
      setDetecting(true);
      apiClient
        .get('/vas/operators/detect', { params: { phoneNumber: cleanNumber } })
        .then((res) => {
          if (res?.operator) {
            const detected = res.operator.toUpperCase();
            if (NETWORKS.some((n) => n.id === detected)) {
              setNetwork(detected);
            }
          }
        })
        .catch(() => {})
        .finally(() => setDetecting(false));
    }
  }, [phoneNumber]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    const numAmount = parseFloat(amount);

    if (!phoneNumber || phoneNumber.replace(/\D/g, '').length < 11) {
      setError('Please enter a valid 11-digit Nigerian phone number.');
      return;
    }

    if (!numAmount || numAmount < 50) {
      setError('Minimum airtime recharge amount is ₦50.00');
      return;
    }

    if (pin.length < 4) {
      setError('Please enter your 4-digit transaction PIN.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await apiClient.postFinancial('/vas/airtime', {
        phoneNumber: phoneNumber.trim(),
        network,
        amount: numAmount,
        transactionPin: pin
      });

      setSuccessData({
        amount: numAmount,
        phoneNumber: phoneNumber.trim(),
        network,
        reference: response?.reference || response?.transactionId || 'Completed'
      });

      showSuccess(`Successfully recharged ₦${numAmount.toLocaleString()} to ${phoneNumber}.`);
      setPhoneNumber('');
      setAmount('');
      setPin('');
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Airtime purchase failed. Please check your wallet balance and PIN.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <>
      <Card padding="p-6 sm:p-7" className={`max-w-xl mx-auto bg-white ${className}`}>
        <div className="flex items-center gap-3 pb-4 mb-4 border-b border-slate-100">
          <div className="w-10 h-10 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center">
            <Smartphone size={20} />
          </div>
          <div>
            <h3 className="text-base font-bold text-slate-900">Buy Airtime</h3>
            <p className="text-xs text-slate-500">Instant recharge for all major Nigerian telecommunication networks</p>
          </div>
        </div>

        {error && (
          <Alert variant="danger" onClose={() => setError(null)} className="mb-4">
            {error}
          </Alert>
        )}

        <form onSubmit={handleSubmit} className="space-y-5">
          {/* Network Selector */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-2">
              Select Network Operator
            </label>
            <div className="grid grid-cols-4 gap-2.5">
              {NETWORKS.map((net) => {
                const isSelected = network === net.id;
                return (
                  <button
                    key={net.id}
                    type="button"
                    onClick={() => setNetwork(net.id)}
                    className={`relative flex flex-col items-center justify-center py-3 px-2 rounded-xl border text-xs font-bold transition-all select-none ${
                      isSelected
                        ? 'border-brand-600 bg-brand-50 text-brand-700 ring-2 ring-brand-500/20 shadow-xs'
                        : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50 hover:border-slate-300'
                    }`}
                  >
                    <span className={`w-3.5 h-3.5 rounded-full mb-1 ${net.color} flex items-center justify-center`}>
                      {isSelected && <Check size={8} strokeWidth={3} />}
                    </span>
                    <span>{net.name}</span>
                  </button>
                );
              })}
            </div>
            {detecting && (
              <p className="text-[11px] text-brand-600 mt-1.5 animate-pulse">
                Auto-detecting operator...
              </p>
            )}
          </div>

          {/* Recipient Phone Number */}
          <Input
            label="Recipient Phone Number"
            type="tel"
            inputMode="numeric"
            placeholder="0801 234 5678"
            maxLength={11}
            value={phoneNumber}
            onChange={(e) => {
              setPhoneNumber(e.target.value.replace(/\D/g, ''));
              if (error) setError(null);
            }}
            icon={Phone}
            required
          />

          {/* Quick Amount Chips */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-2">
              Select or Enter Amount (₦)
            </label>
            <div className="grid grid-cols-3 sm:grid-cols-6 gap-2 mb-3">
              {QUICK_AMOUNTS.map((amt) => {
                const isSelected = amount === amt.toString();
                return (
                  <button
                    key={amt}
                    type="button"
                    onClick={() => {
                      setAmount(amt.toString());
                      if (error) setError(null);
                    }}
                    className={`py-2 px-1 text-center text-xs font-semibold rounded-xl border transition-all ${
                      isSelected
                        ? 'bg-slate-900 text-white border-slate-900 shadow-xs'
                        : 'bg-white border-slate-200 text-slate-700 hover:bg-slate-50'
                    }`}
                  >
                    ₦{amt.toLocaleString()}
                  </button>
                );
              })}
            </div>

            <Input
              type="number"
              min="50"
              step="50"
              placeholder="Custom amount (e.g. 1500)"
              value={amount}
              onChange={(e) => {
                setAmount(e.target.value);
                if (error) setError(null);
              }}
              required
            />
          </div>

          {/* Transaction PIN */}
          <div className="pt-1">
            <PinInput
              label="Authorize with 4-Digit PIN"
              value={pin}
              onChange={(val) => {
                setPin(val);
                if (error) setError(null);
              }}
            />
          </div>

          <Button
            type="submit"
            variant="primary"
            size="md"
            loading={loading}
            icon={Zap}
            className="w-full"
          >
            Recharge Airtime
          </Button>
        </form>
      </Card>

      {/* Success Modal */}
      {successData && (
        <SuccessModal
          isOpen={true}
          onClose={() => setSuccessData(null)}
          title="Airtime Recharge Successful"
          message={`Successfully credited ₦${successData.amount.toLocaleString()} ${successData.network} airtime to ${successData.phoneNumber}. Reference: ${successData.reference}`}
          buttonText="Done"
        />
      )}
    </>
  );
}
