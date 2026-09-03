import React, { useState } from 'react';
import Modal from '../common/Modal';
import Input from '../forms/Input';
import PinInput from '../forms/PinInput';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { PlusCircle } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Modal to deposit ad-hoc or scheduled contribution into an active savings plan.
 */
export default function DepositSavingsModal({
  isOpen,
  onClose,
  plan,
  onSuccess
}) {
  const { showSuccess } = useToast();

  const [amount, setAmount] = useState('');
  const [pin, setPin] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const handleSubmit = async (e) => {
    e.preventDefault();
    const num = parseFloat(amount);
    if (isNaN(num) || num < 500) {
      setError('Minimum deposit amount is ₦500.00');
      return;
    }

    if (pin.length < 4) {
      setError('Please enter your 4-digit transaction PIN.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      await apiClient.postFinancial(`/work/savings/${plan.id}/contribute`, {
        amount: num
      });

      showSuccess(`Deposited ₦${num.toLocaleString()} into savings.`);
      setAmount('');
      setPin('');
      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Deposit failed. Please check your wallet balance and PIN.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Top Up Savings Plan"
      subtitle={`Account: ${plan?.name || plan?.id || 'Savings Plan'}`}
      maxWidth="max-w-md"
    >
      <form onSubmit={handleSubmit} className="space-y-4 pt-1">
        {error && (
          <Alert variant="danger" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        <Input
          label="Deposit Amount (₦)"
          type="number"
          min="500"
          step="500"
          placeholder="e.g. 10000.00"
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
            icon={PlusCircle}
            className="flex-1"
          >
            Deposit Funds
          </Button>
        </div>
      </form>
    </Modal>
  );
}
