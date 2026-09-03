import React, { useState } from 'react';
import Card from '../common/Card';
import Button from '../common/Button';
import Badge from '../common/Badge';
import { Wallet, Plus, ArrowUpRight, Copy, Check, Eye, EyeOff, Building, CreditCard } from 'lucide-react';
import { useToast } from '../../hooks/useToast';

/**
 * Corporate and individual wallet hero header card.
 */
export default function WalletHeader({
  balance = 0,
  currency = 'NGN',
  virtualAccount = null,
  loading = false,
  onFundWallet,
  onTransfer,
  onViewCards,
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
      <div className="absolute top-0 right-0 -mr-8 -mt-8 w-48 h-48 bg-brand-50 rounded-full blur-3xl pointer-events-none opacity-70" />

      <div>
        {/* Top Header */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 mb-3">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center">
              <Wallet size={18} />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider">
                  Operating Wallet Balance
                </span>
                <Badge variant="success" size="sm" dot={true}>
                  Active Ledger
                </Badge>
              </div>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setShowBalance(!showBalance)}
              className="text-slate-400 hover:text-slate-700 p-1.5 rounded-lg hover:bg-slate-50 transition-colors flex items-center gap-1.5 text-xs font-medium"
            >
              {showBalance ? <EyeOff size={15} /> : <Eye size={15} />}
              <span>{showBalance ? 'Hide' : 'Show'}</span>
            </button>
          </div>
        </div>

        {/* Big Balance Number */}
        <div className="my-2">
          {loading ? (
            <div className="h-10 w-56 bg-slate-200 animate-pulse rounded-xl" />
          ) : (
            <h2 className="text-3xl sm:text-4xl lg:text-5xl font-extrabold text-slate-900 tracking-tight font-sans">
              {formattedBalance}
            </h2>
          )}
        </div>

        {/* Dedicated Virtual Account */}
        {virtualAccount && (
          <div className="mt-4 p-3.5 bg-slate-50 rounded-2xl border border-slate-100 flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs">
            <div className="flex items-center gap-2.5 text-slate-600">
              <div className="w-7 h-7 rounded-lg bg-white border border-slate-200 flex items-center justify-center text-slate-500 shrink-0">
                <Building size={14} />
              </div>
              <div className="min-w-0">
                <div className="flex items-center gap-1.5 flex-wrap">
                  <span className="font-semibold text-slate-900">{virtualAccount.bankName || 'Wema Bank'}</span>
                  <span className="text-slate-300">|</span>
                  <span className="font-mono font-bold text-slate-900">{virtualAccount.accountNumber}</span>
                </div>
                <div className="text-[11px] text-slate-400 truncate">{virtualAccount.accountName || 'CebizPay Settlement'}</div>
              </div>
            </div>

            <button
              type="button"
              onClick={() => handleCopy(virtualAccount.accountNumber, 'Account number')}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl border border-slate-200 bg-white hover:bg-slate-50 text-[11px] font-semibold text-slate-700 transition shadow-2xs shrink-0 self-start sm:self-auto"
            >
              {copied ? <Check size={12} className="text-status-success" /> : <Copy size={12} />}
              <span>{copied ? 'Copied' : 'Copy Account'}</span>
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
          Transfer Funds
        </Button>
        {onViewCards && (
          <Button
            variant="ghost"
            size="md"
            icon={CreditCard}
            onClick={onViewCards}
            className="flex-1 sm:flex-none text-slate-600"
          >
            Saved Cards
          </Button>
        )}
      </div>
    </Card>
  );
}
