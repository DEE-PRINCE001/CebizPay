import React from 'react';
import Drawer from '../common/Drawer';
import Badge from '../common/Badge';
import Button from '../common/Button';
import { CheckCircle2, Copy, Printer, ArrowDownLeft, ArrowUpRight, Share2 } from 'lucide-react';
import { useToast } from '../../hooks/useToast';

/**
 * Transaction receipt slide-over drawer with itemized breakdown.
 */
export default function TransactionReceiptDrawer({
  isOpen,
  onClose,
  transaction = null
}) {
  const { showSuccess } = useToast();

  if (!transaction) return null;

  const isCredit = transaction.type === 'Credit' || transaction.direction === 'Inflow' || (transaction.amount > 0 && !transaction.isDebit);

  const formattedAmount = new Intl.NumberFormat('en-NG', {
    style: 'currency',
    currency: transaction.currency || 'NGN',
    minimumFractionDigits: 2
  }).format(Math.abs(transaction.amount || 0));

  const handleCopy = (text, label) => {
    if (!text) return;
    navigator.clipboard.writeText(text);
    showSuccess(`${label} copied to clipboard.`);
  };

  const handlePrint = () => {
    window.print();
  };

  const getStatusBadge = (status) => {
    const s = (status || '').toLowerCase();
    if (s === 'completed' || s === 'success' || s === 'settled') {
      return <Badge variant="success" dot={true}>Completed</Badge>;
    }
    if (s === 'pending' || s === 'processing') {
      return <Badge variant="warning" dot={true}>Pending</Badge>;
    }
    if (s === 'failed' || s === 'rejected') {
      return <Badge variant="danger" dot={true}>Failed</Badge>;
    }
    return <Badge variant="neutral">{status || 'Settled'}</Badge>;
  };

  const formatDate = (dateString) => {
    if (!dateString) return '—';
    try {
      const date = new Date(dateString);
      return new Intl.DateTimeFormat('en-GB', {
        day: 'numeric',
        month: 'long',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
      }).format(date);
    } catch {
      return dateString;
    }
  };

  return (
    <Drawer
      isOpen={isOpen}
      onClose={onClose}
      title="Transaction Receipt"
      subtitle="Detailed ledger entry & settlement breakdown"
      maxWidth="max-w-md"
    >
      <div className="space-y-6 pt-2">
        {/* Top Status Card */}
        <div className="text-center p-6 bg-slate-50 rounded-2xl border border-slate-100 space-y-3">
          <div className={`w-14 h-14 rounded-full mx-auto flex items-center justify-center ${
            isCredit ? 'bg-status-success-bg text-status-success' : 'bg-slate-100 text-slate-800'
          }`}>
            {isCredit ? <ArrowDownLeft size={28} /> : <ArrowUpRight size={28} />}
          </div>

          <div>
            <div className="text-2xl font-extrabold text-slate-900 tracking-tight font-sans">
              {isCredit ? `+${formattedAmount}` : `-${formattedAmount}`}
            </div>
            <div className="mt-1 flex items-center justify-center">
              {getStatusBadge(transaction.status)}
            </div>
          </div>
        </div>

        {/* Itemized Receipt Details */}
        <div className="space-y-3 bg-white rounded-2xl border border-slate-100 p-4 text-xs">
          <div className="flex items-center justify-between py-1.5 border-b border-slate-100">
            <span className="text-slate-500">Transaction Reference</span>
            <div className="flex items-center gap-1 font-mono font-bold text-slate-900">
              <span className="max-w-[150px] truncate">{transaction.reference || transaction.id || '—'}</span>
              {(transaction.reference || transaction.id) && (
                <button
                  type="button"
                  onClick={() => handleCopy(transaction.reference || transaction.id, 'Reference')}
                  className="text-brand-600 hover:text-brand-700 p-0.5"
                >
                  <Copy size={12} />
                </button>
              )}
            </div>
          </div>

          <div className="flex items-center justify-between py-1.5 border-b border-slate-100">
            <span className="text-slate-500">Description / Narration</span>
            <span className="font-semibold text-slate-900 max-w-[180px] text-right truncate">
              {transaction.description || transaction.narration || 'Wallet Transfer'}
            </span>
          </div>

          <div className="flex items-center justify-between py-1.5 border-b border-slate-100">
            <span className="text-slate-500">Category / Type</span>
            <span className="font-medium text-slate-800">{transaction.type || (isCredit ? 'Inflow' : 'Payout')}</span>
          </div>

          {transaction.beneficiary && (
            <div className="flex items-center justify-between py-1.5 border-b border-slate-100">
              <span className="text-slate-500">Beneficiary / Destination</span>
              <span className="font-semibold text-slate-900 text-right">{transaction.beneficiary}</span>
            </div>
          )}

          {transaction.bankName && (
            <div className="flex items-center justify-between py-1.5 border-b border-slate-100">
              <span className="text-slate-500">Destination Bank</span>
              <span className="font-medium text-slate-800">{transaction.bankName}</span>
            </div>
          )}

          <div className="flex items-center justify-between py-1.5 border-b border-slate-100">
            <span className="text-slate-500">Timestamp</span>
            <span className="font-medium text-slate-700">{formatDate(transaction.createdAt || transaction.timestamp || transaction.date)}</span>
          </div>

          <div className="flex items-center justify-between py-1.5 border-b border-slate-100">
            <span className="text-slate-500">Transaction Fee</span>
            <span className="font-medium text-slate-800">
              {transaction.fee ? `₦${transaction.fee.toLocaleString()}` : '₦0.00'}
            </span>
          </div>

          <div className="flex items-center justify-between pt-1 font-bold text-slate-900">
            <span>Total Debited / Credited</span>
            <span className="font-mono text-sm">{formattedAmount}</span>
          </div>
        </div>

        {/* Action Buttons */}
        <div className="flex items-center gap-3 pt-2">
          <Button
            variant="outline"
            size="md"
            icon={Printer}
            onClick={handlePrint}
            className="flex-1"
          >
            Print Receipt
          </Button>
          <Button
            variant="primary"
            size="md"
            onClick={onClose}
            className="flex-1"
          >
            Done
          </Button>
        </div>
      </div>
    </Drawer>
  );
}
