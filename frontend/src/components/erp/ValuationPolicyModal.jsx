import React, { useState } from 'react';
import Modal from '../common/Modal';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { Scale, Check } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

const METHODS = [
  {
    id: 1, // WeightedAverage
    name: 'Weighted Average Cost (WAC)',
    description: 'Computes continuous moving average unit cost across all stock purchase batches. Recommended for standard corporate trading.'
  },
  {
    id: 2, // FIFO
    name: 'First-In, First-Out (FIFO)',
    description: 'Assumes oldest stock items are consumed and sold first. Recommended for perishable items and strict batch lot tracking.'
  }
];

/**
 * Modal to configure organization inventory valuation policy (WAC vs FIFO).
 */
export default function ValuationPolicyModal({
  isOpen,
  onClose,
  onChanged
}) {
  const { showSuccess } = useToast();
  const [selectedMethod, setSelectedMethod] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const { data: policyData, refetch } = useApiQuery(
    () => apiClient.get('/org/inventory/valuation-policy').catch(() => null),
    {
      enabled: isOpen,
      onSuccess: (data) => {
        if (data?.method) {
          const m = (data.method || '').toLowerCase();
          setSelectedMethod(m.includes('fifo') ? 2 : 1);
        }
      }
    }
  );

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      await apiClient.post('/org/inventory/valuation-policy', {
        method: selectedMethod
      });

      showSuccess('Inventory valuation policy updated.');
      refetch();
      if (onChanged) onChanged();
      onClose();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to update valuation policy.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Inventory Valuation Policy"
      subtitle="Configure legal and financial cost-of-goods-sold valuation accounting"
      maxWidth="max-w-md"
    >
      <form onSubmit={handleSubmit} className="space-y-4 pt-1">
        {error && (
          <Alert variant="danger" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        <div className="space-y-3">
          {METHODS.map((m) => {
            const isSelected = selectedMethod === m.id;
            return (
              <div
                key={m.id}
                onClick={() => setSelectedMethod(m.id)}
                className={`p-4 rounded-2xl border cursor-pointer transition select-none ${
                  isSelected
                    ? 'border-brand-600 bg-brand-50/60 ring-2 ring-brand-500/20 shadow-xs'
                    : 'border-slate-200 bg-white hover:border-slate-300'
                }`}
              >
                <div className="flex items-center justify-between mb-1">
                  <span className="font-bold text-xs text-slate-900">{m.name}</span>
                  <div
                    className={`w-4 h-4 rounded-full border flex items-center justify-center ${
                      isSelected
                        ? 'border-brand-600 bg-brand-600 text-white'
                        : 'border-slate-300 bg-white'
                    }`}
                  >
                    {isSelected && <Check size={10} strokeWidth={3} />}
                  </div>
                </div>
                <p className="text-[11px] text-slate-500 leading-relaxed">{m.description}</p>
              </div>
            );
          })}
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
            Apply Valuation Policy
          </Button>
        </div>
      </form>
    </Modal>
  );
}
