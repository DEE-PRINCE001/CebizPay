import React, { useState, useEffect } from 'react';
import Card from '../common/Card';
import Input from '../forms/Input';
import Select from '../forms/Select';
import PinInput from '../forms/PinInput';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import SuccessModal from '../feedback/SuccessModal';
import { Wifi, Phone, Zap, Check } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useApiQuery } from '../../hooks/useApiQuery';
import { parseProblemDetails } from '../../utils/problemDetails';
import { useToast } from '../../hooks/useToast';

const NETWORKS = [
  { id: 'MTN', name: 'MTN', color: 'bg-amber-400 text-slate-900' },
  { id: 'AIRTEL', name: 'Airtel', color: 'bg-red-500 text-white' },
  { id: 'GLO', name: 'Glo', color: 'bg-emerald-600 text-white' },
  { id: '9MOBILE', name: '9mobile', color: 'bg-lime-700 text-white' }
];

/**
 * Mobile data bundle subscription form with catalog lookup.
 */
export default function DataBundleForm({ onSuccess, className = '' }) {
  const { showSuccess } = useToast();

  const [phoneNumber, setPhoneNumber] = useState('');
  const [network, setNetwork] = useState('MTN');
  const [selectedProductCode, setSelectedProductCode] = useState('');
  const [pin, setPin] = useState('');

  const [detecting, setDetecting] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [successData, setSuccessData] = useState(null);

  // Fetch available data bundles for the active network operator
  const {
    data: bundles,
    loading: bundlesLoading
  } = useApiQuery(
    () =>
      apiClient
        .get('/vas/data/bundles', { params: { network } })
        .catch(() => []),
    { deps: [network] }
  );

  const bundleList = Array.isArray(bundles) ? bundles : [];

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

  // Reset selected bundle on network change
  useEffect(() => {
    setSelectedProductCode('');
  }, [network]);

  const selectedBundle = bundleList.find(
    (b) => (b.productCode || b.code || b.id) === selectedProductCode
  );

  const bundleOptions = [
    { value: '', label: bundlesLoading ? 'Loading data bundles...' : 'Select a data plan' },
    ...bundleList.map((b) => ({
      value: b.productCode || b.code || b.id,
      label: `${b.name || b.description || b.plan} — ₦${(b.amount || b.price || 0).toLocaleString()} (${b.validity || '30 Days'})`
    }))
  ];

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!phoneNumber || phoneNumber.replace(/\D/g, '').length < 11) {
      setError('Please enter a valid 11-digit Nigerian phone number.');
      return;
    }

    if (!selectedBundle) {
      setError('Please select a data bundle plan.');
      return;
    }

    if (pin.length < 4) {
      setError('Please enter your 4-digit transaction PIN.');
      return;
    }

    const amount = selectedBundle.amount || selectedBundle.price || 0;

    setLoading(true);
    setError(null);

    try {
      const response = await apiClient.postFinancial('/vas/data', {
        phoneNumber: phoneNumber.trim(),
        network,
        productCode: selectedBundle.productCode || selectedBundle.code || selectedBundle.id,
        amount,
        transactionPin: pin
      });

      setSuccessData({
        plan: selectedBundle.name || selectedBundle.description || 'Data Bundle',
        amount,
        phoneNumber: phoneNumber.trim(),
        network,
        reference: response?.reference || response?.transactionId || 'Completed'
      });

      showSuccess(`Data bundle activated for ${phoneNumber}.`);
      setPhoneNumber('');
      setSelectedProductCode('');
      setPin('');
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Data purchase failed. Please check your wallet balance and PIN.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <>
      <Card padding="p-6 sm:p-7" className={`max-w-xl mx-auto bg-white ${className}`}>
        <div className="flex items-center gap-3 pb-4 mb-4 border-b border-slate-100">
          <div className="w-10 h-10 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center">
            <Wifi size={20} />
          </div>
          <div>
            <h3 className="text-base font-bold text-slate-900">Buy Data Bundle</h3>
            <p className="text-xs text-slate-500">Daily, weekly, monthly, and SME data packages</p>
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

          {/* Data Bundle Plan Selector */}
          <Select
            label="Select Data Plan"
            options={bundleOptions}
            value={selectedProductCode}
            onChange={(e) => {
              setSelectedProductCode(e.target.value);
              if (error) setError(null);
            }}
            disabled={bundlesLoading}
            required
          />

          {/* Selected Plan Summary Banner */}
          {selectedBundle && (
            <div className="p-3.5 bg-brand-50 border border-brand-100 rounded-2xl flex items-center justify-between text-xs">
              <div>
                <span className="font-bold text-slate-900">{selectedBundle.name || selectedBundle.description}</span>
                <span className="text-slate-500 block text-[11px]">Validity: {selectedBundle.validity || '30 Days'}</span>
              </div>
              <span className="font-mono font-bold text-sm text-brand-700">
                ₦{(selectedBundle.amount || selectedBundle.price || 0).toLocaleString()}
              </span>
            </div>
          )}

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
            Activate Data Bundle
          </Button>
        </form>
      </Card>

      {/* Success Modal */}
      {successData && (
        <SuccessModal
          isOpen={true}
          onClose={() => setSuccessData(null)}
          title="Data Bundle Subscribed"
          message={`Successfully activated ${successData.plan} (${successData.network}) for ${successData.phoneNumber}. Reference: ${successData.reference}`}
          buttonText="Done"
        />
      )}
    </>
  );
}
