import React, { useState, useEffect } from 'react';
import Modal from '../common/Modal';
import Input from '../forms/Input';
import Select from '../forms/Select';
import Textarea from '../forms/Textarea';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { ShoppingCart, Truck, Plus, Trash2, Calendar, FileText } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Modal to create Sales Orders or Purchase Orders.
 */
export default function CreateOrderModal({
  isOpen,
  onClose,
  type = 'sales', // 'sales' | 'purchase'
  onSuccess
}) {
  const { showSuccess } = useToast();
  const isSales = type === 'sales';

  const [counterparties, setCounterparties] = useState([]);
  const [selectedCounterpartyId, setSelectedCounterpartyId] = useState('');
  const [orderDate, setOrderDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [expectedDate, setExpectedDate] = useState(() => {
    const d = new Date();
    d.setDate(d.getDate() + 7);
    return d.toISOString().slice(0, 10);
  });
  const [notes, setNotes] = useState('');

  // Line items
  const [items, setItems] = useState([
    { itemName: '', quantity: 1, unitPrice: 0 }
  ]);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (isOpen) {
      const endpoint = isSales ? '/org/customers' : '/org/suppliers';
      apiClient
        .get(endpoint, { params: { pageSize: 100 } })
        .then((res) => {
          const list = res?.items || [];
          setCounterparties(
            list.map((c) => ({
              value: c.id,
              label: `${c.name} (${c.reference || (isSales ? 'CUST' : 'SUPP')})`
            }))
          );
          if (list.length > 0 && !selectedCounterpartyId) {
            setSelectedCounterpartyId(list[0].id);
          }
        })
        .catch(() => {});
    }
  }, [isOpen, isSales]);

  const handleItemChange = (index, field, value) => {
    setItems((prev) => {
      const copy = [...prev];
      copy[index] = { ...copy[index], [field]: value };
      return copy;
    });
  };

  const handleAddItem = () => {
    setItems((prev) => [...prev, { itemName: '', quantity: 1, unitPrice: 0 }]);
  };

  const handleRemoveItem = (index) => {
    if (items.length <= 1) return;
    setItems((prev) => prev.filter((_, i) => i !== index));
  };

  const totalAmount = items.reduce(
    (acc, it) => acc + (parseFloat(it.quantity) || 0) * (parseFloat(it.unitPrice) || 0),
    0
  );

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!selectedCounterpartyId) {
      setError(`Please select a ${isSales ? 'customer' : 'supplier'}.`);
      return;
    }

    const invalid = items.find((i) => !i.itemName.trim() || parseFloat(i.quantity) <= 0);
    if (invalid) {
      setError('Please provide valid items and positive quantities.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      if (isSales) {
        await apiClient.post('/org/orders/sales', {
          customerId: selectedCounterpartyId,
          orderDate: new Date(orderDate).toISOString(),
          expectedFulfillmentDate: new Date(expectedDate).toISOString(),
          currency: 'NGN',
          notes: notes.trim() || null,
          items: items.map((it) => ({
            itemName: it.itemName.trim(),
            quantity: parseFloat(it.quantity),
            unitPrice: parseFloat(it.unitPrice) || 0
          }))
        });
        showSuccess('Sales order created.');
      } else {
        await apiClient.post('/org/orders/purchase', {
          supplierId: selectedCounterpartyId,
          orderDate: new Date(orderDate).toISOString(),
          expectedDeliveryDate: new Date(expectedDate).toISOString(),
          currency: 'NGN',
          notes: notes.trim() || null,
          items: items.map((it) => ({
            itemName: it.itemName.trim(),
            quantity: parseFloat(it.quantity),
            unitPrice: parseFloat(it.unitPrice) || 0
          }))
        });
        showSuccess('Purchase order created.');
      }

      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || `Failed to create ${isSales ? 'sales' : 'purchase'} order.`);
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
      title={isSales ? 'Create Sales Order' : 'Create Purchase Order'}
      subtitle={isSales ? 'Register customer order for fulfillment' : 'Procure supplies and generate vendor purchase order'}
      maxWidth="max-w-2xl"
    >
      <form onSubmit={handleSubmit} className="space-y-4 pt-1">
        {error && (
          <Alert variant="danger" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
          <Select
            label={isSales ? 'Customer' : 'Supplier'}
            options={counterparties.length > 0 ? counterparties : [{ value: '', label: 'No options available' }]}
            value={selectedCounterpartyId}
            onChange={(e) => setSelectedCounterpartyId(e.target.value)}
            required
          />
          <Input
            label="Order Date"
            type="date"
            value={orderDate}
            onChange={(e) => setOrderDate(e.target.value)}
            icon={Calendar}
            required
          />
          <Input
            label={isSales ? 'Fulfillment Target' : 'Expected Delivery'}
            type="date"
            value={expectedDate}
            onChange={(e) => setExpectedDate(e.target.value)}
            icon={Calendar}
            required
          />
        </div>

        {/* Order Items */}
        <div className="space-y-2 pt-2">
          <div className="flex items-center justify-between">
            <label className="text-xs font-bold text-slate-900 block">Order Line Items</label>
            <button
              type="button"
              onClick={handleAddItem}
              className="text-xs font-bold text-brand-600 hover:text-brand-700 flex items-center gap-1"
            >
              <Plus size={13} />
              <span>Add Item</span>
            </button>
          </div>

          <div className="space-y-2 max-h-56 overflow-y-auto pr-1">
            {items.map((it, idx) => {
              const lineTotal = (parseFloat(it.quantity) || 0) * (parseFloat(it.unitPrice) || 0);
              return (
                <div key={idx} className="p-3 bg-slate-50 border border-slate-200/80 rounded-2xl flex items-center gap-2 text-xs">
                  <div className="flex-1">
                    <input
                      type="text"
                      placeholder="Item description / SKU"
                      value={it.itemName}
                      onChange={(e) => handleItemChange(idx, 'itemName', e.target.value)}
                      className="w-full bg-white border border-slate-200 rounded-xl px-2.5 py-1.5 text-xs font-semibold text-slate-900 focus:ring-1 focus:ring-brand-500"
                      required
                    />
                  </div>

                  <div className="w-20">
                    <input
                      type="number"
                      min="1"
                      placeholder="Qty"
                      value={it.quantity}
                      onChange={(e) => handleItemChange(idx, 'quantity', e.target.value)}
                      className="w-full bg-white border border-slate-200 rounded-xl px-2 py-1.5 text-xs text-center font-mono font-bold text-slate-900 focus:ring-1 focus:ring-brand-500"
                      required
                    />
                  </div>

                  <div className="w-28">
                    <input
                      type="number"
                      min="0"
                      step="100"
                      placeholder="Unit Cost (₦)"
                      value={it.unitPrice}
                      onChange={(e) => handleItemChange(idx, 'unitPrice', e.target.value)}
                      className="w-full bg-white border border-slate-200 rounded-xl px-2 py-1.5 text-xs font-mono font-bold text-slate-900 focus:ring-1 focus:ring-brand-500"
                      required
                    />
                  </div>

                  <div className="w-24 font-mono font-bold text-slate-900 text-right">
                    {formatAmount(lineTotal)}
                  </div>

                  <button
                    type="button"
                    onClick={() => handleRemoveItem(idx)}
                    disabled={items.length <= 1}
                    className="p-1 text-slate-400 hover:text-red-500 disabled:opacity-30"
                  >
                    <Trash2 size={14} />
                  </button>
                </div>
              );
            })}
          </div>
        </div>

        <div className="p-3.5 bg-brand-50/60 border border-brand-100 rounded-2xl flex items-center justify-between text-xs">
          <span className="font-bold text-slate-700">Total Estimated Order Value</span>
          <span className="font-mono text-base font-extrabold text-brand-700">{formatAmount(totalAmount)}</span>
        </div>

        <Textarea
          label="Notes / Special Instructions"
          rows={2}
          placeholder="Delivery terms, carrier information, or PO reference..."
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
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
            icon={isSales ? ShoppingCart : Truck}
            className="flex-1"
          >
            {isSales ? 'Create Sales Order' : 'Create Purchase Order'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
