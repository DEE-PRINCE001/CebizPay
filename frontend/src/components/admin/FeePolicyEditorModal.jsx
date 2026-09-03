import React, { useState } from 'react';
import Modal from '../common/Modal';
import Input from '../forms/Input';
import Select from '../forms/Select';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { DollarSign, Percent, Scale, Check } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

const POLICY_TYPES = [
  { value: 'peer-transfer', label: 'Peer-to-Peer (Internal Wallet) Transfer' },
  { value: 'bank-transfer', label: 'Outward NIBSS Inter-Bank Transfer' }
];

const FEE_MODES = [
  { value: 2, label: 'Percentage Rate with Floor / Cap (₦)' },
  { value: 1, label: 'Zero-Fee (Free Scheme)' }
];

/**
 * Modal to configure and activate platform fee policy versions.
 */
export default function FeePolicyEditorModal({
  isOpen,
  onClose,
  defaultPolicyType = 'peer-transfer',
  onSuccess
}) {
  const { showSuccess } = useToast();

  const [policyType, setPolicyType] = useState(defaultPolicyType);
  const [mode, setMode] = useState(2);
  const [percentageRate, setPercentageRate] = useState('0.015'); // 1.5%
  const [minimumFee, setMinimumFee] = useState('20');
  const [maximumFee, setMaximumFee] = useState('2000');

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const isPercentage = parseInt(mode, 10) === 2;

  const handleSubmit = async (e) => {
    e.preventDefault();

    setLoading(true);
    setError(null);

    const rate = parseFloat(percentageRate);
    const min = parseFloat(minimumFee);
    const max = parseFloat(maximumFee);

    try {
      const endpoint = `/admin/fees/${policyType}`;
      await apiClient.post(endpoint, {
        mode: parseInt(mode, 10),
        percentageRate: isPercentage ? rate : undefined,
        minimumFee: isPercentage ? min : undefined,
        maximumFee: isPercentage ? max : undefined
      });

      showSuccess(`New ${policyType === 'peer-transfer' ? 'peer' : 'bank'} transfer fee policy activated.`);
      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to update fee policy.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Configure Fee Policy Version"
      subtitle="Creates and atomically activates a new platform pricing version"
      maxWidth="max-w-md"
    >
      <form onSubmit={handleSubmit} className="space-y-4 pt-1">
        {error && (
          <Alert variant="danger" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        <Select
          label="Transaction Channel"
          options={POLICY_TYPES}
          value={policyType}
          onChange={(e) => setPolicyType(e.target.value)}
        />

        <Select
          label="Pricing Mode"
          options={FEE_MODES}
          value={mode}
          onChange={(e) => setMode(e.target.value)}
        />

        {isPercentage && (
          <>
            <Input
              label="Percentage Rate (e.g. 0.015 = 1.5%)"
              type="number"
              step="0.001"
              min="0"
              max="1"
              value={percentageRate}
              onChange={(e) => setPercentageRate(e.target.value)}
              icon={Percent}
              required
            />

            <div className="grid grid-cols-2 gap-3">
              <Input
                label="Minimum Fee Floor (₦)"
                type="number"
                min="0"
                step="5"
                value={minimumFee}
                onChange={(e) => setMinimumFee(e.target.value)}
                required
              />
              <Input
                label="Maximum Fee Cap (₦)"
                type="number"
                min="0"
                step="50"
                value={maximumFee}
                onChange={(e) => setMaximumFee(e.target.value)}
                required
              />
            </div>
          </>
        )}

        <div className="p-3 bg-slate-50 border border-slate-100 rounded-xl text-xs text-slate-500">
          <span className="font-semibold text-slate-700 block mb-1">Audit Trail & Immutability:</span>
          Activating this policy will archive the previous version and immediately apply new rates to all subsequent transactions.
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
            icon={Scale}
            className="flex-1"
          >
            Activate Policy
          </Button>
        </div>
      </form>
    </Modal>
  );
}
