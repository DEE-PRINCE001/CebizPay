import React, { useState } from 'react';
import Card from '../common/Card';
import Button from '../common/Button';
import { Wallet, Plus, ArrowUpRight, Copy, Check, Eye, EyeOff, Building } from 'lucide-react';
import { useToast } from '../../hooks/useToast';

/**
 * Wallet balance card with primary funding and transfer actions.
 */
export default function BalanceCard({
  balance = 0,
  currency = 'NGN',
  virtualAccount = null,
  loading = false,
  onFundWallet,
  onTransfer,
  className = ''
}) {
  const { showSuccess } = useToast();
  const [showBalance, setShowBalance] = useState(true);
  const [copied, setCopied] = useState(false);

  const formattedBalance = showBalance
    ? new Intl.NumberFormat('en-NG', {
        style: 'currency',
        currency: currency || 'NGN',
        minimumFractionDigits: 2
      }).format(balance || 0)
    : '••••••••••';

  const handleCopy = (text, label) => {
    if (!text) return;
    navigator.clipboard.writeText(text);
    setCopied(true);
    showSuccess(`${label} copied to clipboard.`);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <Card padding="p-6 sm:p-7" className={`bg-white relative overflow-hidden flex flex-col justify-between ${className}`}>
      <div className="absolute top-0 right-0 -mr-8 -mt-8 w-40 h-40 bg-brand-50 rounded-full blur-2xl pointer-events-none opacity-60" />

      <div>
        {/* Balance Label and Visibility Toggle */}
        <div className="flex items-center justify-between mb-2">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded-full bg-brand-50 text-brand-600 flex items-center justify-center">
              <Wallet size={16} />
            </div>
            <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider">
              Total Wallet Balance
            </span>
          </div>
          <button
            type="button"
            onClick={() => setShowBalance(!showBalance)}
            className="text-slate-400 hover:text-slate-600 p-1.5 rounded-lg hover:bg-slate-50 transition-colors"
            aria-label="Toggle balance visibility"
          >
            {showBalance ? <EyeOff size={16} /> : <Eye size={16} />}
          </button>
        </div>

        {/* Balance Display */}
        <div className="my-2">
          {loading ? (
            <div className="h-10 w-48 bg-slate-200 animate-pulse rounded-xl" />
          ) : (
            <h2 className="text-3xl sm:text-4xl font-extrabold text-slate-900 tracking-tight font-sans">
              {formattedBalance}
            </h2>
          )}
        </div>

        {/* Dedicated Virtual Account */}
        {virtualAccount && (
          <div className="mt-4 p-3 bg-slate-50 rounded-xl border border-slate-100 flex flex-col sm:flex-row sm:items-center justify-between gap-2 text-xs">
            <div className="flex items-center gap-2 text-slate-600">
              <Building size={14} className="text-slate-400 shrink-0" />
              <span className="font-medium text-slate-700">{virtualAccount.bankName || 'Wema Bank'}</span>
              <span className="text-slate-300">|</span>
              <span className="font-mono font-bold text-slate-900">{virtualAccount.accountNumber}</span>
              <span className="text-slate-400 text-[11px] truncate">({virtualAccount.accountName})</span>
            </div>
            <button
              type="button"
              onClick={() => handleCopy(virtualAccount.accountNumber, 'Account number')}
              className="inline-flex items-center gap-1 text-[11px] font-semibold text-brand-600 hover:underline shrink-0"
            >
              {copied ? <Check size={12} className="text-status-success" /> : <Copy size={12} />}
              <span>{copied ? 'Copied' : 'Copy'}</span>
            </button>
          </div>
        )}
      </div>

      {/* Action Buttons */}
      <div className="flex flex-wrap items-center gap-3 pt-6 mt-4 border-t border-slate-100">
        <Button
          variant="primary"
          size="md"
          icon={Plus}
          onClick={onFundWallet}
          className="flex-1 sm:flex-none"
        >
          Fund Wallet
        </Button>
        <Button
          variant="outline"
          size="md"
          icon={ArrowUpRight}
          onClick={onTransfer}
          className="flex-1 sm:flex-none"
        >
          Send / Transfer
        </Button>
      </div>
    </Card>
  );
}
