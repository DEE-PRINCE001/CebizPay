import React, { useState } from 'react';
import Modal from '../common/Modal';
import Input from '../forms/Input';
import Select from '../forms/Select';
import Textarea from '../forms/Textarea';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { DollarSign, Calendar, Tag, CreditCard, Check } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

const EXPENSE_CATEGORIES = [
  { value: 7, label: 'General & Administrative' },
  { value: 1, label: 'Rent & Facilities' },
  { value: 2, label: 'Utilities & Power' },
  { value: 3, label: 'Salaries & Benefits' },
  { value: 4, label: 'Marketing & Advertising' },
  { value: 5, label: 'Logistics & Transport' },
  { value: 6, label: 'Repairs & Maintenance' }
];

const PAYMENT_METHODS = [
  { value: 1, label: 'Corporate Wallet' },
  { value: 2, label: 'Direct Bank Transfer' },
  { value: 4, label: 'Petty Cash' }
];

/**
 * Modal to record operating expenses.
 */
export default function AddExpenseModal({
  isOpen,
  onClose,
  onSuccess
}) {
  const { showSuccess } = useToast();

  const [category, setCategory] = useState(7);
  const [description, setDescription] = useState('');
  const [amount, setAmount] = useState('');
  const [expenseDate, setExpenseDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [paymentMethod, setPaymentMethod] = useState(1);
  const [reference, setReference] = useState('');

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const handleSubmit = async (e) => {
    e.preventDefault();
    const amt = parseFloat(amount);

    if (!description.trim()) {
      setError('Please provide a description for this expense.');
      return;
    }

    if (isNaN(amt) || amt <= 0) {
      setError('Please enter a valid expense amount.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      await apiClient.post('/org/expenses', {
        category: parseInt(category, 10),
        description: description.trim(),
        amount: amt,
        expenseDate: new Date(expenseDate).toISOString(),
        paymentMethod: parseInt(paymentMethod, 10),
        currency: 'NGN',
        reference: reference.trim() || undefined
      });

      showSuccess(`Recorded expense of ₦${amt.toLocaleString()}.`);
      setDescription('');
      setAmount('');
      setReference('');
      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to record expense.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Record Operating Expense"
      subtitle="Track corporate disbursements, cost centers, and tax deductible expenses"
      maxWidth="max-w-md"
    >
      <form onSubmit={handleSubmit} className="space-y-4 pt-1">
        {error && (
          <Alert variant="danger" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        <div className="grid grid-cols-2 gap-3">
          <Select
            label="Expense Category"
            options={EXPENSE_CATEGORIES}
            value={category}
            onChange={(e) => setCategory(e.target.value)}
          />
          <Input
            label="Expense Date"
            type="date"
            value={expenseDate}
            onChange={(e) => setExpenseDate(e.target.value)}
            icon={Calendar}
            required
          />
        </div>

        <Input
          label="Expense Amount (₦)"
          type="number"
          min="100"
          step="100"
          placeholder="e.g. 75000"
          value={amount}
          onChange={(e) => {
            setAmount(e.target.value);
            if (error) setError(null);
          }}
          required
        />

        <div className="grid grid-cols-2 gap-3">
          <Select
            label="Payment Channel"
            options={PAYMENT_METHODS}
            value={paymentMethod}
            onChange={(e) => setPaymentMethod(e.target.value)}
          />
          <Input
            label="Receipt / Reference #"
            placeholder="e.g. REC-8492"
            value={reference}
            onChange={(e) => setReference(e.target.value)}
          />
        </div>

        <Textarea
          label="Description / Purpose"
          rows={2}
          placeholder="Brief explanation of the business purpose..."
          value={description}
          onChange={(e) => {
            setDescription(e.target.value);
            if (error) setError(null);
          }}
          required
        />

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
            icon={Check}
            className="flex-1"
          >
            Save Expense
          </Button>
        </div>
      </form>
    </Modal>
  );
}
