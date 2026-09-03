import React, { useState } from 'react';
import Card from '../common/Card';
import Input from '../forms/Input';
import Select from '../forms/Select';
import PinInput from '../forms/PinInput';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import SuccessModal from '../feedback/SuccessModal';
import { Tv, Check } from 'lucide-react';
import { useToast } from '../../hooks/useToast';

const CABLE_PROVIDERS = [
  { id: 'DSTV', name: 'DStv' },
  { id: 'GOTV', name: 'GOtv' },
  { id: 'STARTIMES', name: 'StarTimes' },
  { id: 'SHOWMAX', name: 'Showmax' }
];

const PACKAGES = {
  DSTV: [
    { value: 'dstv-padi', label: 'DStv Padi — ₦2,950 / month' },
    { value: 'dstv-yanga', label: 'DStv Yanga — ₦4,200 / month' },
    { value: 'dstv-confam', label: 'DStv Confam — ₦7,400 / month' },
    { value: 'dstv-compact', label: 'DStv Compact — ₦12,500 / month' },
    { value: 'dstv-compact-plus', label: 'DStv Compact Plus — ₦19,800 / month' },
    { value: 'dstv-premium', label: 'DStv Premium — ₦29,500 / month' }
  ],
  GOTV: [
    { value: 'gotv-smallie', label: 'GOtv Smallie — ₦1,300 / month' },
    { value: 'gotv-jinja', label: 'GOtv Jinja — ₦2,700 / month' },
    { value: 'gotv-jolli', label: 'GOtv Jolli — ₦3,950 / month' },
    { value: 'gotv-max', label: 'GOtv Max — ₦5,700 / month' },
    { value: 'gotv-supa', label: 'GOtv Supa — ₦7,600 / month' }
  ],
  STARTIMES: [
    { value: 'nova', label: 'Nova Bouquet — ₦1,500 / month' },
    { value: 'basic', label: 'Basic Bouquet — ₦2,600 / month' },
    { value: 'smart', label: 'Smart Bouquet — ₦3,500 / month' },
    { value: 'classic', label: 'Classic Bouquet — ₦3,800 / month' },
    { value: 'super', label: 'Super Bouquet — ₦6,500 / month' }
  ],
  SHOWMAX: [
    { value: 'showmax-mobile', label: 'Showmax Mobile — ₦1,450 / month' },
    { value: 'showmax-standard', label: 'Showmax Standard — ₦2,900 / month' },
    { value: 'showmax-pro', label: 'Showmax Pro — ₦6,300 / month' }
  ]
};

/**
 * Cable TV subscription and renewal form.
 */
export default function CableTvForm({ onSuccess, className = '' }) {
  const { showSuccess } = useToast();

  const [provider, setProvider] = useState('DSTV');
  const [smartcardNumber, setSmartcardNumber] = useState('');
  const [selectedPackage, setSelectedPackage] = useState(PACKAGES.DSTV[0].value);
  const [pin, setPin] = useState('');

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [successData, setSuccessData] = useState(null);

  const availablePackages = PACKAGES[provider] || [];

  const handleProviderChange = (provId) => {
    setProvider(provId);
    if (PACKAGES[provId] && PACKAGES[provId].length > 0) {
      setSelectedPackage(PACKAGES[provId][0].value);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!smartcardNumber.trim() || smartcardNumber.trim().length < 10) {
      setError('Please enter a valid Smartcard or IUC number.');
      return;
    }

    if (pin.length < 4) {
      setError('Please enter your 4-digit transaction PIN.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const pkgObj = availablePackages.find((p) => p.value === selectedPackage);
      setSuccessData({
        provider,
        smartcardNumber,
        packageName: pkgObj?.label || selectedPackage
      });

      showSuccess('Cable TV subscription renewed successfully.');
      setSmartcardNumber('');
      setPin('');
      if (onSuccess) onSuccess();
    } catch {
      setError('Subscription renewal failed. Please verify your smartcard and PIN.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <>
      <Card padding="p-6 sm:p-7" className={`max-w-xl mx-auto bg-white ${className}`}>
        <div className="flex items-center gap-3 pb-4 mb-4 border-b border-slate-100">
          <div className="w-10 h-10 rounded-xl bg-purple-50 text-purple-600 flex items-center justify-center">
            <Tv size={20} />
          </div>
          <div>
            <h3 className="text-base font-bold text-slate-900">Cable TV Subscription</h3>
            <p className="text-xs text-slate-500">Instant decoder activation for DStv, GOtv, StarTimes, and Showmax</p>
          </div>
        </div>

        {error && (
          <Alert variant="danger" onClose={() => setError(null)} className="mb-4">
            {error}
          </Alert>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-2">
              Select Cable TV Provider
            </label>
            <div className="grid grid-cols-4 gap-2.5">
              {CABLE_PROVIDERS.map((prov) => {
                const isSelected = provider === prov.id;
                return (
                  <button
                    key={prov.id}
                    type="button"
                    onClick={() => handleProviderChange(prov.id)}
                    className={`py-3 px-2 rounded-xl border text-xs font-bold transition-all text-center select-none ${
                      isSelected
                        ? 'border-brand-600 bg-brand-50 text-brand-700 ring-2 ring-brand-500/20 shadow-xs'
                        : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50 hover:border-slate-300'
                    }`}
                  >
                    {prov.name}
                  </button>
                );
              })}
            </div>
          </div>

          <Input
            label="Smartcard / IUC Number"
            placeholder="Enter 10 or 11-digit smartcard number"
            value={smartcardNumber}
            onChange={(e) => {
              setSmartcardNumber(e.target.value);
              if (error) setError(null);
            }}
            required
          />

          <Select
            label="Select Package / Bouquet"
            options={availablePackages}
            value={selectedPackage}
            onChange={(e) => setSelectedPackage(e.target.value)}
            required
          />

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
            icon={Tv}
            className="w-full"
          >
            Renew Subscription
          </Button>
        </form>
      </Card>

      {/* Success Modal */}
      {successData && (
        <SuccessModal
          isOpen={true}
          onClose={() => setSuccessData(null)}
          title="Subscription Renewed"
          message={`Successfully renewed ${successData.packageName} for ${successData.provider} decoder #${successData.smartcardNumber}.`}
          buttonText="Done"
        />
      )}
    </>
  );
}
