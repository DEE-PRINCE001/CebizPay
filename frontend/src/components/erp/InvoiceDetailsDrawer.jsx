import React, { useState } from 'react';
import Drawer from '../common/Drawer';
import Badge from '../common/Badge';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import PinInput from '../forms/PinInput';
import Table from '../tables/Table';
import TableHeader from '../tables/TableHeader';
import TableRow from '../tables/TableRow';
import { Receipt, Send, CheckCircle2, XCircle, CreditCard, User, Calendar, DollarSign, ArrowDownLeft } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Invoice itemized breakdown and settlement drawer.
 */
export default function InvoiceDetailsDrawer({
  isOpen,
  onClose,
  invoice,
  onRefresh
}) {
  const { showSuccess, showError } = useToast();

  const [paymentMode, setPaymentMode] = useState(false);
  const [paymentAmount, setPaymentAmount] = useState('');
  const [settlementMethod, setSettlementMethod] = useState(1); // 1: Wallet, 2: BankTransfer, 4: Cash
  const [pin, setPin] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  if (!invoice) return null;

  const total = invoice.totalAmount || 0;
  const paid = invoice.amountPaid || 0;
  const balanceDue = total - paid;
  const items = invoice.items || [];

  const getStatusBadge = (status) => {
    const s = (status || '').toLowerCase();
    if (s === 'paid') return <Badge variant="success" dot={true}>Paid in Full</Badge>;
    if (s === 'partiallypaid') return <Badge variant="warning" dot={true}>Partially Paid</Badge>;
    if (s === 'issued') return <Badge variant="brand" dot={true}>Issued</Badge>;
    if (s === 'overdue') return <Badge variant="danger" dot={true}>Overdue</Badge>;
    if (s === 'cancelled') return <Badge variant="neutral">Cancelled</Badge>;
    return <Badge variant="neutral">{status || 'Draft'}</Badge>;
  };

  const formatAmount = (amt) => {
    return new Intl.NumberFormat('en-NG', {
      style: 'currency',
      currency: 'NGN',
      minimumFractionDigits: 2
    }).format(amt || 0);
  };

  const formatDate = (dateString) => {
    if (!dateString) return '—';
    try {
      return new Intl.DateTimeFormat('en-GB', {
        day: 'numeric',
        month: 'short',
        year: 'numeric'
      }).format(new Date(dateString));
    } catch {
      return dateString;
    }
  };

  // 1. Issue Invoice
  const handleIssue = async () => {
    setLoading(true);
    setError(null);
    try {
      await apiClient.post(`/org/invoices/${invoice.id}/issue`);
      showSuccess(`Invoice ${invoice.invoiceNumber || ''} issued.`);
      if (onRefresh) onRefresh();
      onClose();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to issue invoice.');
    } finally {
      setLoading(false);
    }
  };

  // 2. Record Payment
  const handlePaymentSubmit = async (e) => {
    e.preventDefault();
    const amt = parseFloat(paymentAmount) || balanceDue;

    if (settlementMethod === 1 && pin.length < 4) {
      setError('Please enter your 4-digit PIN for corporate wallet settlement.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      await apiClient.postFinancial(`/org/invoices/${invoice.id}/payments`, {
        amount: amt,
        settlementMethod,
        pin: settlementMethod === 1 ? pin : undefined,
        reference: `INV-PAY-${Date.now()}`
      });

      showSuccess(`Payment of ₦${amt.toLocaleString()} recorded.`);
      setPaymentMode(false);
      setPin('');
      if (onRefresh) onRefresh();
      onClose();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Payment recording failed.');
    } finally {
      setLoading(false);
    }
  };

  // 3. Cancel Invoice
  const handleCancel = async () => {
    setLoading(true);
    setError(null);
    try {
      await apiClient.post(`/org/invoices/${invoice.id}/cancel`);
      showSuccess('Invoice cancelled.');
      if (onRefresh) onRefresh();
      onClose();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to cancel invoice.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Drawer
      isOpen={isOpen}
      onClose={() => {
        setPaymentMode(false);
        onClose();
      }}
      title={invoice.invoiceNumber || 'Invoice Details'}
      subtitle={`Customer: ${invoice.customerName || 'Direct Client'}`}
      size="md"
    >
      <div className="space-y-5">
        {error && (
          <Alert variant="danger" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        {/* Top Status Header */}
        <div className="p-4 bg-slate-50 border border-slate-200/80 rounded-2xl flex items-center justify-between">
          <div>
            <span className="text-[11px] text-slate-400 block mb-0.5">Total Amount</span>
            <span className="text-2xl font-extrabold font-sans text-slate-900">
              {formatAmount(total)}
            </span>
          </div>
          <div>{getStatusBadge(invoice.status)}</div>
        </div>

        {/* Invoice Metadata Grid */}
        <div className="grid grid-cols-2 gap-3 text-xs">
          <div className="p-3 bg-slate-50 border border-slate-100 rounded-xl space-y-0.5">
            <span className="text-slate-400 block text-[11px]">Issue Date</span>
            <span className="font-bold text-slate-800">{formatDate(invoice.issueDate)}</span>
          </div>

          <div className="p-3 bg-slate-50 border border-slate-100 rounded-xl space-y-0.5">
            <span className="text-slate-400 block text-[11px]">Due Date</span>
            <span className="font-bold text-slate-800">{formatDate(invoice.dueDate)}</span>
          </div>

          <div className="p-3 bg-slate-50 border border-slate-100 rounded-xl space-y-0.5">
            <span className="text-slate-400 block text-[11px]">Amount Paid</span>
            <span className="font-bold text-status-success font-mono">
              {formatAmount(paid)}
            </span>
          </div>

          <div className="p-3 bg-slate-50 border border-slate-100 rounded-xl space-y-0.5">
            <span className="text-slate-400 block text-[11px]">Balance Due</span>
            <span className="font-bold text-brand-700 font-mono">
              {formatAmount(balanceDue)}
            </span>
          </div>
        </div>

        {/* Line Items Breakdown Table */}
        <div className="space-y-2">
          <span className="text-xs font-bold text-slate-900 block">Itemized Breakdown</span>
          <div className="border border-slate-200/80 rounded-2xl overflow-hidden">
            <Table>
              <TableHeader
                columns={[
                  { label: 'Item' },
                  { label: 'Qty' },
                  { label: 'Price' },
                  { label: 'Total', align: 'right' }
                ]}
              />
              <tbody>
                {items.map((it, idx) => (
                  <TableRow key={it.id || idx}>
                    <td className="py-2.5 px-3 text-xs font-medium text-slate-900">
                      {it.itemName}
                    </td>
                    <td className="py-2.5 px-3 text-xs font-mono text-slate-600">
                      {it.quantity}
                    </td>
                    <td className="py-2.5 px-3 text-xs font-mono text-slate-600">
                      {formatAmount(it.unitPrice)}
                    </td>
                    <td className="py-2.5 px-3 text-xs font-mono font-bold text-slate-900 text-right">
                      {formatAmount(it.lineTotal || it.quantity * it.unitPrice)}
                    </td>
                  </TableRow>
                ))}
              </tbody>
            </Table>
          </div>
        </div>

        {/* Settlement Payment Mode */}
        {paymentMode ? (
          <form onSubmit={handlePaymentSubmit} className="p-4 bg-brand-50/60 border border-brand-200 rounded-2xl space-y-3 pt-3">
            <span className="text-xs font-bold text-slate-900 block">Record Invoice Settlement</span>

            <div className="grid grid-cols-2 gap-2">
              <button
                type="button"
                onClick={() => setSettlementMethod(1)}
                className={`py-1.5 px-3 text-xs font-semibold rounded-xl border text-center transition ${
                  settlementMethod === 1
                    ? 'bg-brand-600 border-brand-600 text-white'
                    : 'bg-white border-slate-200 text-slate-700'
                }`}
              >
                Corporate Wallet
              </button>
              <button
                type="button"
                onClick={() => setSettlementMethod(2)}
                className={`py-1.5 px-3 text-xs font-semibold rounded-xl border text-center transition ${
                  settlementMethod === 2
                    ? 'bg-brand-600 border-brand-600 text-white'
                    : 'bg-white border-slate-200 text-slate-700'
                }`}
              >
                Direct Bank Transfer
              </button>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">Amount to Pay (₦)</label>
              <input
                type="number"
                min="100"
                step="50"
                placeholder={balanceDue.toString()}
                value={paymentAmount || balanceDue}
                onChange={(e) => setPaymentAmount(e.target.value)}
                className="w-full bg-white border border-slate-200 rounded-xl px-3 py-2 text-xs font-mono font-bold text-slate-900 focus:ring-1 focus:ring-brand-500"
              />
            </div>

            {settlementMethod === 1 && (
              <PinInput
                label="Authorize with 4-Digit PIN"
                value={pin}
                onChange={(v) => {
                  setPin(v);
                  if (error) setError(null);
                }}
              />
            )}

            <div className="flex items-center gap-2 pt-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPaymentMode(false)}
                disabled={loading}
                className="flex-1"
              >
                Cancel
              </Button>
              <Button
                type="submit"
                variant="primary"
                size="sm"
                loading={loading}
                icon={CheckCircle2}
                className="flex-1"
              >
                Confirm Payment
              </Button>
            </div>
          </form>
        ) : (
          <div className="flex items-center gap-2 pt-2 border-t border-slate-100">
            {invoice.status === 'Draft' && (
              <Button
                variant="primary"
                size="md"
                icon={Send}
                loading={loading}
                onClick={handleIssue}
                className="flex-1"
              >
                Issue to Client
              </Button>
            )}

            {(invoice.status === 'Issued' || invoice.status === 'PartiallyPaid') && (
              <Button
                variant="primary"
                size="md"
                icon={ArrowDownLeft}
                onClick={() => setPaymentMode(true)}
                className="flex-1"
              >
                Record Payment
              </Button>
            )}

            {invoice.status !== 'Paid' && invoice.status !== 'Cancelled' && (
              <Button
                variant="outline"
                size="md"
                icon={XCircle}
                loading={loading}
                onClick={handleCancel}
                className="text-status-danger hover:bg-status-danger-bg"
              >
                Cancel
              </Button>
            )}
          </div>
        )}
      </div>
    </Drawer>
  );
}
