import React, { useState, useEffect } from 'react';
import Modal from '../common/Modal';
import Input from '../forms/Input';
import Select from '../forms/Select';
import Textarea from '../forms/Textarea';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { Receipt, Plus, Trash2, Calendar, FileText, User } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Multi-line item invoice builder modal.
 */
export default function CreateInvoiceModal({
  isOpen,
  onClose,
  onSuccess
}) {
  const { showSuccess } = useToast();

  const [customers, setCustomers] = useState([]);
  const [customerId, setCustomerId] = useState('');
  const [issueDate, setIssueDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [dueDate, setDueDate] = useState(() => {
    const d = new Date();
    d.setDate(d.getDate() + 14);
    return d.toISOString().slice(0, 10);
  });
  const [applyVat, setApplyVat] = useState(true);
  const [notes, setNotes] = useState('');
  const [billingContact, setBillingContact] = useState('');

  // Line Items
  const [items, setItems] = useState([
    { itemName: '', description: '', quantity: 1, unitPrice: 0 }
  ]);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (isOpen) {
      apiClient
        .get('/org/customers', { params: { pageSize: 100 } })
        .then((res) => {
          const list = res?.items || [];
          setCustomers(list.map((c) => ({ value: c.id, label: `${c.name} (${c.reference || 'CUST'})` })));
          if (list.length > 0 && !customerId) {
            setCustomerId(list[0].id);
          }
        })
        .catch(() => {});
    }
  }, [isOpen]);

  const handleItemChange = (index, field, value) => {
    setItems((prev) => {
      const copy = [...prev];
      copy[index] = { ...copy[index], [field]: value };
      return copy;
    });
  };

  const handleAddItem = () => {
    setItems((prev) => [
      ...prev,
      { itemName: '', description: '', quantity: 1, unitPrice: 0 }
    ]);
  };

  const handleRemoveItem = (index) => {
    if (items.length <= 1) return;
    setItems((prev) => prev.filter((_, i) => i !== index));
  };

  const subtotal = items.reduce(
    (acc, it) => acc + (parseFloat(it.quantity) || 0) * (parseFloat(it.unitPrice) || 0),
    0
  );
  const vatAmount = applyVat ? subtotal * 0.075 : 0;
  const totalAmount = subtotal + vatAmount;

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!customerId) {
      setError('Please select a customer for this invoice.');
      return;
    }

    const invalidItem = items.find(
      (it) => !it.itemName.trim() || parseFloat(it.quantity) <= 0 || parseFloat(it.unitPrice) < 0
    );
    if (invalidItem) {
      setError('Please provide a valid item name, quantity, and unit price for all line items.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      await apiClient.post('/org/invoices', {
        customerId,
        issueDate: new Date(issueDate).toISOString(),
        dueDate: new Date(dueDate).toISOString(),
        applyVat,
        currency: 'NGN',
        notes: notes.trim() || null,
        billingContact: billingContact.trim() || null,
        items: items.map((it) => ({
          itemName: it.itemName.trim(),
          description: it.description.trim() || null,
          quantity: parseFloat(it.quantity),
          unitPrice: parseFloat(it.unitPrice)
        }))
      });

      showSuccess('Invoice created successfully.');
      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to create invoice.');
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
      title="Create Customer Invoice"
      subtitle="Issue itemized billable invoices with statutory 7.5% VAT calculation"
      maxWidth="max-w-2xl"
    >
      <form onSubmit={handleSubmit} className="space-y-4 pt-1">
        {error && (
          <Alert variant="danger" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        {/* Customer & Dates */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
          <Select
            label="Bill To Customer"
            options={customers.length > 0 ? customers : [{ value: '', label: 'No customers available' }]}
            value={customerId}
            onChange={(e) => setCustomerId(e.target.value)}
            required
          />
          <Input
            label="Invoice Date"
            type="date"
            value={issueDate}
            onChange={(e) => setIssueDate(e.target.value)}
            icon={Calendar}
            required
          />
          <Input
            label="Due Date"
            type="date"
            value={dueDate}
            onChange={(e) => setDueDate(e.target.value)}
            icon={Calendar}
            required
          />
        </div>

        {/* Line Items Table */}
        <div className="space-y-2 pt-2">
          <div className="flex items-center justify-between">
            <label className="text-xs font-bold text-slate-900 block">Itemized Line Items</label>
            <button
              type="button"
              onClick={handleAddItem}
              className="text-xs font-bold text-brand-600 hover:text-brand-700 flex items-center gap-1"
            >
              <Plus size={13} />
              <span>Add Line</span>
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
                      placeholder="Item / Service description"
                      value={it.itemName}
                      onChange={(e) => handleItemChange(idx, 'itemName', e.target.value)}
                      className="w-full bg-white border border-slate-200 rounded-xl px-2.5 py-1.5 text-xs font-semibold text-slate-900 focus:outline-hidden focus:ring-1 focus:ring-brand-500"
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
                      className="w-full bg-white border border-slate-200 rounded-xl px-2 py-1.5 text-xs text-center font-mono font-bold text-slate-900 focus:outline-hidden focus:ring-1 focus:ring-brand-500"
                      required
                    />
                  </div>

                  <div className="w-28">
                    <input
                      type="number"
                      min="0"
                      step="100"
                      placeholder="Price (₦)"
                      value={it.unitPrice}
                      onChange={(e) => handleItemChange(idx, 'unitPrice', e.target.value)}
                      className="w-full bg-white border border-slate-200 rounded-xl px-2 py-1.5 text-xs font-mono font-bold text-slate-900 focus:outline-hidden focus:ring-1 focus:ring-brand-500"
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

        {/* VAT & Totals Card */}
        <div className="p-4 bg-brand-50/60 border border-brand-100 rounded-2xl space-y-2 text-xs">
          <div className="flex items-center justify-between text-slate-600">
            <span>Subtotal</span>
            <span className="font-mono font-bold text-slate-900">{formatAmount(subtotal)}</span>
          </div>

          <div className="flex items-center justify-between border-t border-brand-100 pt-2 text-slate-600">
            <label className="flex items-center gap-2 cursor-pointer select-none">
              <input
                type="checkbox"
                checked={applyVat}
                onChange={(e) => setApplyVat(e.target.checked)}
                className="w-4 h-4 text-brand-600 rounded-md border-slate-300 focus:ring-brand-500"
              />
              <span className="font-semibold text-slate-700">Apply Statutory VAT (7.5%)</span>
            </label>
            <span className="font-mono font-bold text-slate-900">{formatAmount(vatAmount)}</span>
          </div>

          <div className="flex items-center justify-between border-t border-brand-200 pt-2 text-sm font-bold text-slate-900">
            <span>Grand Total Due</span>
            <span className="font-mono text-base font-extrabold text-brand-700">{formatAmount(totalAmount)}</span>
          </div>
        </div>

        <Textarea
          label="Invoice Notes / Payment Terms (optional)"
          rows={2}
          placeholder="Bank details, wire instructions, or client PO notes..."
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
            icon={Receipt}
            className="flex-1"
          >
            Issue Invoice
          </Button>
        </div>
      </form>
    </Modal>
  );
}
