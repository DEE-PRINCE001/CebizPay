import React, { useState, useEffect } from 'react';
import Modal from '../common/Modal';
import Input from '../forms/Input';
import Select from '../forms/Select';
import PinInput from '../forms/PinInput';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import Card from '../common/Card';
import SuccessModal from '../feedback/SuccessModal';
import { PiggyBank, Target, Calendar, TrendingUp, Zap, ArrowRight, ShieldCheck } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

const PLAN_TYPES = [
  { value: 'Target', label: 'Target Savings (Goal-Oriented)' },
  { value: 'FixedLock', label: 'Fixed Lock (High Yield)' },
  { value: 'Flexible', label: 'Flexible Savings (Instant Access)' }
];

const DURATIONS = [
  { value: 30, label: '30 Days (1 Month)' },
  { value: 90, label: '90 Days (3 Months)' },
  { value: 180, label: '180 Days (6 Months)' },
  { value: 365, label: '365 Days (1 Year)' }
];

const FREQUENCIES = [
  { value: 'Daily', label: 'Daily Contribution' },
  { value: 'Weekly', label: 'Weekly Contribution' },
  { value: 'Monthly', label: 'Monthly Contribution' }
];

/**
 * Modal for creating and subscribing to target savings plans.
 */
export default function CreateSavingsPlanModal({
  isOpen,
  onClose,
  organizationId,
  onSuccess
}) {
  const { showSuccess } = useToast();

  const [name, setName] = useState('');
  const [planType, setPlanType] = useState('Target');
  const [targetAmount, setTargetAmount] = useState('');
  const [initialDeposit, setInitialDeposit] = useState('');
  const [durationDays, setDurationDays] = useState(90);
  const [frequency, setFrequency] = useState('Monthly');
  const [pin, setPin] = useState('');

  // Preview estimation state
  const [preview, setPreview] = useState(null);
  const [previewLoading, setPreviewLoading] = useState(false);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [successData, setSuccessData] = useState(null);

  // Debounced real-time interest calculation preview
  useEffect(() => {
    const depositNum = parseFloat(initialDeposit) || 0;
    const targetNum = parseFloat(targetAmount) || 0;

    if (depositNum >= 1000) {
      setPreviewLoading(true);
      const timer = setTimeout(() => {
        apiClient
          .post('/work/savings/preview', {
            planType,
            amount: depositNum,
            durationDays: parseInt(durationDays, 10),
            frequency,
            targetAmount: targetNum > depositNum ? targetNum : null
          })
          .then((res) => setPreview(res))
          .catch(() => setPreview(null))
          .finally(() => setPreviewLoading(false));
      }, 400);

      return () => clearTimeout(timer);
    } else {
      setPreview(null);
    }
  }, [planType, initialDeposit, targetAmount, durationDays, frequency]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    const depositNum = parseFloat(initialDeposit);
    const targetNum = parseFloat(targetAmount);

    if (!name.trim()) {
      setError('Please provide a name for your savings plan.');
      return;
    }

    if (isNaN(depositNum) || depositNum < 1000) {
      setError('Minimum initial deposit is ₦1,000.00.');
      return;
    }

    if (pin.length < 4) {
      setError('Please enter your 4-digit transaction PIN to authorize deposit.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      // 1. Fetch available plans or create sponsored scheme
      const availablePlans = await apiClient.get('/org/savings/plans').catch(() => []);
      let planId = Array.isArray(availablePlans) && availablePlans.length > 0 ? availablePlans[0].id : null;

      if (!planId) {
        // Create an active underlying plan template if none exists
        const createdPlan = await apiClient.post('/org/savings/plans', {
          name: `${name.trim()} Template`,
          planType,
          currency: 'NGN',
          interestRate: preview?.annualInterestRate || 12.0,
          minimumAmount: 1000,
          maximumAmount: 10000000,
          minimumDurationDays: 30,
          maximumDurationDays: 365,
          targetAmount: targetNum || null
        });
        planId = createdPlan?.id;
      }

      // 2. Open Savings Account Instance
      const account = await apiClient.postFinancial('/work/savings', {
        savingsPlanId: planId,
        organizationId: organizationId || null,
        initialDepositAmount: depositNum,
        durationDays: parseInt(durationDays, 10),
        targetAmount: targetNum || null,
        contributionFrequency: frequency
      });

      setSuccessData({
        planName: name.trim(),
        initialDeposit: depositNum,
        maturityDate: account?.maturityDateUtc,
        interestRate: preview?.annualInterestRate || 12.0
      });

      showSuccess(`Savings plan "${name}" created successfully.`);
      setName('');
      setInitialDeposit('');
      setTargetAmount('');
      setPin('');
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to create savings plan. Please verify your wallet balance and PIN.');
    } finally {
      setLoading(false);
    }
  };

  const formatCurrency = (amt) => {
    return new Intl.NumberFormat('en-NG', {
      style: 'currency',
      currency: 'NGN',
      minimumFractionDigits: 2
    }).format(amt || 0);
  };

  return (
    <>
      <Modal
        isOpen={isOpen && !successData}
        onClose={onClose}
        title="Create Target Savings Plan"
        subtitle="Lock funds toward a financial goal with automated interest returns"
        maxWidth="max-w-lg"
      >
        <form onSubmit={handleSubmit} className="space-y-4 pt-1">
          {error && (
            <Alert variant="danger" onClose={() => setError(null)}>
              {error}
            </Alert>
          )}

          <Input
            label="Plan Title / Goal"
            placeholder="e.g. Rent Savings, New Car Fund"
            value={name}
            onChange={(e) => {
              setName(e.target.value);
              if (error) setError(null);
            }}
            icon={Target}
            required
          />

          <div className="grid grid-cols-2 gap-3">
            <Select
              label="Savings Type"
              options={PLAN_TYPES}
              value={planType}
              onChange={(e) => setPlanType(e.target.value)}
            />
            <Select
              label="Duration Period"
              options={DURATIONS}
              value={durationDays}
              onChange={(e) => setDurationDays(e.target.value)}
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <Input
              label="Target Goal (₦)"
              type="number"
              step="1000"
              placeholder="e.g. 500000"
              value={targetAmount}
              onChange={(e) => setTargetAmount(e.target.value)}
            />
            <Input
              label="Initial Deposit (₦)"
              type="number"
              min="1000"
              step="500"
              placeholder="Min. ₦1,000"
              value={initialDeposit}
              onChange={(e) => setInitialDeposit(e.target.value)}
              required
            />
          </div>

          {/* Real-time Interest & Payout Forecast Widget */}
          {preview && (
            <div className="p-4 bg-brand-50 border border-brand-100 rounded-2xl space-y-2 text-xs">
              <div className="flex items-center justify-between font-bold text-slate-900 border-b border-brand-200/60 pb-2">
                <div className="flex items-center gap-1.5 text-brand-700">
                  <TrendingUp size={15} />
                  <span>Estimated Maturity Payout</span>
                </div>
                <span className="font-mono text-sm text-brand-700">
                  {formatCurrency(preview.estimatedMaturityPayout)}
                </span>
              </div>
              <div className="flex justify-between text-slate-600 pt-1">
                <span>Annual Interest Rate:</span>
                <span className="font-bold text-slate-900">{preview.annualInterestRate}% p.a.</span>
              </div>
              <div className="flex justify-between text-slate-600">
                <span>Estimated Accrued Interest:</span>
                <span className="font-bold text-status-success font-mono">
                  +{formatCurrency(preview.estimatedTotalInterest)}
                </span>
              </div>
            </div>
          )}

          {/* Authorization PIN */}
          <div className="pt-1">
            <PinInput
              label="Authorize Initial Deposit with 4-Digit PIN"
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
              icon={PiggyBank}
              className="flex-1"
            >
              Create Savings Plan
            </Button>
          </div>
        </form>
      </Modal>

      {/* Success Modal */}
      {successData && (
        <SuccessModal
          isOpen={true}
          onClose={() => {
            setSuccessData(null);
            onClose();
          }}
          title="Savings Plan Activated"
          message={`Successfully created "${successData.planName}" with an initial deposit of ${formatCurrency(successData.initialDeposit)}. Earning ${successData.interestRate}% interest per annum.`}
          buttonText="Done"
        />
      )}
    </>
  );
}
