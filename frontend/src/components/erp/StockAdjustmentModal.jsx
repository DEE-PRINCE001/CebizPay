import React, { useState } from 'react';
import Modal from '../common/Modal';
import Tabs from '../common/Tabs';
import Input from '../forms/Input';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { ArrowDownRight, ArrowUpRight, Sliders, Check } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Modal to record stock-in receipts, stock-out issues, and manual audit adjustments.
 */
export default function StockAdjustmentModal({
  isOpen,
  onClose,
  item,
  onSuccess
}) {
  const { showSuccess } = useToast();
  const [actionType, setActionType] = useState('stock-in'); // 'stock-in' | 'stock-out' | 'adjust'

  const [quantity, setQuantity] = useState('');
  const [unitCost, setUnitCost] = useState('');
  const [reference, setReference] = useState('');
  const [reason, setReason] = useState('');

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const tabs = [
    { id: 'stock-in', label: 'Receive (Stock In)', icon: ArrowDownRight },
    { id: 'stock-out', label: 'Issue (Stock Out)', icon: ArrowUpRight },
    { id: 'adjust', label: 'Manual Adjustment', icon: Sliders }
  ];

  if (!item) return null;

  const handleSubmit = async (e) => {
    e.preventDefault();
    const qty = parseFloat(quantity);
    const cost = parseFloat(unitCost) || 0;

    if (isNaN(qty) || qty <= 0) {
      setError('Please enter a valid positive quantity.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      if (actionType === 'stock-in') {
        await apiClient.post(`/org/inventory/items/${item.id}/stock-in`, {
          quantity: qty,
          unitCost: cost > 0 ? cost : item.averageCost || 0,
          reference: reference.trim() || undefined,
          reason: reason.trim() || 'Purchased stock replenishment'
        });
        showSuccess(`Received ${qty} ${item.unitOfMeasure || 'units'} into stock.`);
      } else if (actionType === 'stock-out') {
        await apiClient.post(`/org/inventory/items/${item.id}/stock-out`, {
          quantity: qty,
          reference: reference.trim() || undefined,
          reason: reason.trim() || 'Goods dispatched'
        });
        showSuccess(`Issued ${qty} ${item.unitOfMeasure || 'units'} from stock.`);
      } else if (actionType === 'adjust') {
        await apiClient.post(`/org/inventory/items/${item.id}/adjust`, {
          quantityDelta: qty,
          reference: reference.trim() || undefined,
          reason: reason.trim() || 'Inventory physical count audit',
          newAverageCost: cost > 0 ? cost : undefined
        });
        showSuccess(`Adjusted stock by ${qty} ${item.unitOfMeasure || 'units'}.`);
      }

      setQuantity('');
      setUnitCost('');
      setReference('');
      setReason('');
      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to record stock movement.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Stock Movement & Adjustment"
      subtitle={`Item: ${item.name} (${item.sku || 'SKU'}) • Current On Hand: ${item.quantityOnHand || 0} ${item.unitOfMeasure || 'units'}`}
      maxWidth="max-w-md"
    >
      <div className="space-y-4 pt-1">
        <Tabs
          variant="segmented"
          tabs={tabs}
          activeTab={actionType}
          onChange={(t) => {
            setActionType(t);
            setError(null);
          }}
        />

        {error && (
          <Alert variant="danger" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        <form onSubmit={handleSubmit} className="space-y-3.5">
          <Input
            label={actionType === 'adjust' ? 'Quantity Delta (±)' : 'Movement Quantity'}
            type="number"
            min="1"
            step="1"
            placeholder="e.g. 50"
            value={quantity}
            onChange={(e) => {
              setQuantity(e.target.value);
              if (error) setError(null);
            }}
            required
          />

          {actionType === 'stock-in' && (
            <Input
              label="Purchase Unit Cost (₦)"
              type="number"
              min="0"
              step="50"
              placeholder={`Current Avg: ₦${(item.averageCost || 0).toLocaleString()}`}
              value={unitCost}
              onChange={(e) => setUnitCost(e.target.value)}
            />
          )}

          <Input
            label="Reference / Invoice / PO #"
            placeholder="e.g. PO-8492, INV-102"
            value={reference}
            onChange={(e) => setReference(e.target.value)}
          />

          <Input
            label="Reason / Narration"
            placeholder="e.g. Supplier delivery, damaged goods, physical count"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
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
              Record Movement
            </Button>
          </div>
        </form>
      </div>
    </Modal>
  );
}
