import React, { useState } from 'react';
import Card from '../common/Card';
import Input from '../forms/Input';
import Select from '../forms/Select';
import Tabs from '../common/Tabs';
import PinInput from '../forms/PinInput';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import SuccessModal from '../feedback/SuccessModal';
import { Zap, Building2, CheckCircle2 } from 'lucide-react';
import { useToast } from '../../hooks/useToast';

const DISCOS = [
  { value: 'IKEDC', label: 'Ikeja Electric (IKEDC)' },
  { value: 'EKEDC', label: 'Eko Electric (EKEDC)' },
  { value: 'AEDC', label: 'Abuja Electricity Distribution (AEDC)' },
  { value: 'IBEDC', label: 'Ibadan Electricity Distribution (IBEDC)' },
  { value: 'PHED', label: 'Port Harcourt Electric (PHED)' },
  { value: 'EEDC', label: 'Enugu Electricity Distribution (EEDC)' },
  { value: 'BEDC', label: 'Benin Electricity Distribution (BEDC)' },
  { value: 'KEDCO', label: 'Kano Electricity Distribution (KEDCO)' },
  { value: 'JEDC', label: 'Jos Electricity Distribution (JEDC)' },
  { value: 'KAEDCO', label: 'Kaduna Electric (KAEDCO)' }
];

const METER_TYPES = [
  { id: 'prepaid', label: 'Prepaid (Token)' },
  { id: 'postpaid', label: 'Postpaid (Bill)' }
];

/**
 * Electricity bill payment and token recharge form.
 */
export default function ElectricityForm({ onSuccess, className = '' }) {
  const { showSuccess } = useToast();

  const [disco, setDisco] = useState('IKEDC');
  const [meterType, setMeterType] = useState('prepaid');
  const [meterNumber, setMeterNumber] = useState('');
  const [amount, setAmount] = useState('');
  const [pin, setPin] = useState('');

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [successData, setSuccessData] = useState(null);

  const handleSubmit = async (e) => {
    e.preventDefault();
    const numAmount = parseFloat(amount);

    if (!meterNumber.trim() || meterNumber.trim().length < 8) {
      setError('Please enter a valid meter number.');
      return;
    }

    if (!numAmount || numAmount < 500) {
      setError('Minimum electricity recharge amount is ₦500.00');
      return;
    }

    if (pin.length < 4) {
      setError('Please enter your 4-digit transaction PIN.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      // Simulate token generation / integration
      const discoLabel = DISCOS.find((d) => d.value === disco)?.label || disco;
      const fakeToken = `${Math.floor(1000 + Math.random() * 9000)}-${Math.floor(1000 + Math.random() * 9000)}-${Math.floor(1000 + Math.random() * 9000)}-${Math.floor(1000 + Math.random() * 9000)}`;

      setSuccessData({
        disco: discoLabel,
        meterNumber,
        meterType,
        amount: numAmount,
        token: meterType === 'prepaid' ? fakeToken : null,
        units: `${(numAmount / 68).toFixed(1)} kWh`
      });

      showSuccess('Electricity payment processed successfully.');
      setMeterNumber('');
      setAmount('');
      setPin('');
      if (onSuccess) onSuccess();
    } catch {
      setError('Payment processing failed. Please verify your meter number and PIN.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <>
      <Card padding="p-6 sm:p-7" className={`max-w-xl mx-auto bg-white ${className}`}>
        <div className="flex items-center gap-3 pb-4 mb-4 border-b border-slate-100">
          <div className="w-10 h-10 rounded-xl bg-amber-50 text-amber-600 flex items-center justify-center">
            <Zap size={20} />
          </div>
          <div>
            <h3 className="text-base font-bold text-slate-900">Electricity Bill Payment</h3>
            <p className="text-xs text-slate-500">Instant token generation for prepaid meters and postpaid bill clearance</p>
          </div>
        </div>

        {error && (
          <Alert variant="danger" onClose={() => setError(null)} className="mb-4">
            {error}
          </Alert>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <Select
            label="Electricity Distribution Company (DISCO)"
            options={DISCOS}
            value={disco}
            onChange={(e) => setDisco(e.target.value)}
            required
          />

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1.5">
              Meter Type
            </label>
            <Tabs
              variant="segmented"
              tabs={METER_TYPES}
              activeTab={meterType}
              onChange={(t) => setMeterType(t)}
            />
          </div>

          <Input
            label="Meter / Account Number"
            placeholder="Enter 11 or 13-digit meter number"
            value={meterNumber}
            onChange={(e) => {
              setMeterNumber(e.target.value);
              if (error) setError(null);
            }}
            icon={Building2}
            required
          />

          <Input
            label="Recharge Amount (₦)"
            type="number"
            min="500"
            step="100"
            placeholder="e.g. 5000.00"
            value={amount}
            onChange={(e) => {
              setAmount(e.target.value);
              if (error) setError(null);
            }}
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
            icon={Zap}
            className="w-full"
          >
            Pay Electricity Bill
          </Button>
        </form>
      </Card>

      {/* Success Modal with Token */}
      {successData && (
        <SuccessModal
          isOpen={true}
          onClose={() => setSuccessData(null)}
          title="Electricity Bill Paid"
          message={`Successfully recharged ₦${successData.amount.toLocaleString()} for meter ${successData.meterNumber} (${successData.disco}). ${
            successData.token ? `TOKEN: ${successData.token} (${successData.units})` : 'Postpaid bill credited.'
          }`}
          buttonText="Done"
        />
      )}
    </>
  );
}
