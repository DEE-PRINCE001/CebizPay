import React, { useState } from 'react';
import Modal from '../common/Modal';
import Input from '../forms/Input';
import Select from '../forms/Select';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { Users, Calendar, Coins, Check } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

const FREQUENCIES = [
  { value: 'Weekly', label: 'Weekly Contributions' },
  { value: 'Monthly', label: 'Monthly Contributions' },
  { value: 'Daily', label: 'Daily Contributions' }
];

const POSITIONS_COUNT = [
  { value: 5, label: '5 Members / Cycles' },
  { value: 10, label: '10 Members / Cycles' },
  { value: 12, label: '12 Members / Cycles' }
];

/**
 * Modal to create an Ajo / Esusu rotational thrift circle.
 */
export default function CreateThriftGroupModal({
  isOpen,
  onClose,
  organizationId,
  onSuccess
}) {
  const { showSuccess } = useToast();

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [contributionAmount, setContributionAmount] = useState('');
  const [frequency, setFrequency] = useState('Monthly');
  const [totalPositions, setTotalPositions] = useState(10);
  const [startDate, setStartDate] = useState(() => {
    const d = new Date();
    d.setDate(d.getDate() + 7);
    return d.toISOString().slice(0, 10);
  });
  const [deadline, setDeadline] = useState(() => {
    const d = new Date();
    d.setDate(d.getDate() + 5);
    return d.toISOString().slice(0, 10);
  });

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const totalPool = (parseFloat(contributionAmount) || 0) * parseInt(totalPositions, 10);

  const handleSubmit = async (e) => {
    e.preventDefault();
    const amount = parseFloat(contributionAmount);

    if (!name.trim()) {
      setError('Please provide a name for the thrift circle.');
      return;
    }

    if (isNaN(amount) || amount < 1000) {
      setError('Minimum contribution amount is ₦1,000.00');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      await apiClient.post('/work/thrift', {
        organizationId: organizationId || null,
        name: name.trim(),
        description: description.trim() || null,
        currency: 'NGN',
        contributionAmount: amount,
        frequency,
        totalPositions: parseInt(totalPositions, 10),
        startDateUtc: new Date(startDate).toISOString(),
        positionSelectionDeadlineUtc: new Date(deadline).toISOString()
      });

      showSuccess(`Thrift circle "${name}" created successfully.`);
      setName('');
      setDescription('');
      setContributionAmount('');
      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to create thrift circle.');
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
      title="Create Rotational Thrift (Ajo)"
      subtitle="Organize automated peer savings circles with guaranteed rotation payouts"
      maxWidth="max-w-lg"
    >
      <form onSubmit={handleSubmit} className="space-y-4 pt-1">
        {error && (
          <Alert variant="danger" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        <Input
          label="Thrift Circle Name"
          placeholder="e.g. Finance Team Ajo, Q4 Rotational Pool"
          value={name}
          onChange={(e) => {
            setName(e.target.value);
            if (error) setError(null);
          }}
          icon={Users}
          required
        />

        <div className="grid grid-cols-2 gap-3">
          <Input
            label="Contribution per Cycle (₦)"
            type="number"
            min="1000"
            step="1000"
            placeholder="e.g. 50000"
            value={contributionAmount}
            onChange={(e) => setContributionAmount(e.target.value)}
            required
          />
          <Select
            label="Rotation Frequency"
            options={FREQUENCIES}
            value={frequency}
            onChange={(e) => setFrequency(e.target.value)}
          />
        </div>

        <div className="grid grid-cols-2 gap-3">
          <Select
            label="Total Members / Slots"
            options={POSITIONS_COUNT}
            value={totalPositions}
            onChange={(e) => setTotalPositions(e.target.value)}
          />
          <Input
            label="Circle Start Date"
            type="date"
            value={startDate}
            onChange={(e) => setStartDate(e.target.value)}
            icon={Calendar}
            required
          />
        </div>

        {/* Pool Summary Box */}
        {totalPool > 0 && (
          <div className="p-3.5 bg-brand-50 border border-brand-100 rounded-2xl flex items-center justify-between text-xs">
            <div>
              <span className="font-bold text-slate-900 block">Total Payout per Rotation</span>
              <span className="text-slate-500 text-[11px]">
                {totalPositions} members × {formatAmount(parseFloat(contributionAmount) || 0)}
              </span>
            </div>
            <span className="font-mono font-extrabold text-sm text-brand-700">
              {formatAmount(totalPool)}
            </span>
          </div>
        )}

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
            icon={Coins}
            className="flex-1"
          >
            Launch Circle
          </Button>
        </div>
      </form>
    </Modal>
  );
}
