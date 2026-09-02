import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import StatCard from '../../components/common/StatCard';
import Badge from '../../components/common/Badge';
import { useAuth } from '../../context/AuthContext';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatDate } from '../../utils/formatters';
import {
  Wallet,
  ArrowUpRight,
  ArrowDownLeft,
  Smartphone,
  Copy,
  Check,
  Eye,
  EyeOff,
  Building,
  CreditCard,
  Send,
  PiggyBank,
  Banknote,
  ShieldCheck
} from 'lucide-react';
import { Link } from 'react-router-dom';

export default function WalletDashboard() {
  const { user, balanceVisible, toggleBalancePrivacy } = useAuth();
  const { showSuccess } = useToast();
  const [copiedAccount, setCopiedAccount] = useState(false);

  // Consumer balances
  const wallet = {
    balance: 485500.0,
    currency: 'NGN',
    dvaAccountNumber: '9018492018',
    dvaBankName: 'Wema Bank (Monnify Rail)',
    dvaAccountName: 'Amina Adeleke / CebizPay',
    kycTier: 'TIER_3',
    activeLoans: 484000.0,
    activeSavings: 250000.0
  };

  // Recent transactions
  const transactions = [
    {
      id: 'TXN-8849201',
      type: 'INBOUND_SALARY',
      description: 'August 2026 Salary — Apex Global Technologies',
      amount: 1067500.0,
      currency: 'NGN',
      isCredit: true,
      status: 'COMPLETED',
      date: '2026-08-28T10:15:00Z'
    },
    {
      id: 'TXN-8849199',
      type: 'OUTBOUND_TRANSFER',
      description: 'Transfer to Babatunde Adeleke (GTBank)',
      amount: 45000.0,
      currency: 'NGN',
      isCredit: false,
      status: 'COMPLETED',
      date: '2026-08-29T14:30:00Z'
    },
    {
      id: 'TXN-8849195',
      type: 'VAS_PURCHASE',
      description: 'MTN Nigeria 10GB Data Top-Up',
      amount: 3500.0,
      currency: 'NGN',
      isCredit: false,
      status: 'COMPLETED',
      date: '2026-08-30T09:12:00Z'
    },
    {
      id: 'TXN-8849190',
      type: 'SAVINGS_CONTRIBUTION',
      description: 'Tech Equipment & Upskilling Savings Deposit',
      amount: 50000.0,
      currency: 'NGN',
      isCredit: false,
      status: 'COMPLETED',
      date: '2026-09-01T08:00:00Z'
    }
  ];

  const handleCopy = () => {
    navigator.clipboard.writeText(wallet.dvaAccountNumber);
    setCopiedAccount(true);
    showSuccess('Account Number Copied', 'Your Dedicated Virtual Account is ready for direct bank deposits.');
    setTimeout(() => setCopiedAccount(false), 2000);
  };

  return (
    <div>
      <PageHeader
        title="Personal Wallet &amp; Ledger"
        subtitle={`Welcome back, ${user?.name || 'Amina Adeleke'} • Verified Consumer Identity • Tier 3 Limits`}
        actions={
          <div className="flex items-center gap-2">
            <Link
              to="/consumer/transfers"
              className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
            >
              <Send className="w-3.5 h-3.5" />
              Send Money
            </Link>
            <Link
              to="/consumer/vas"
              className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-slate-800 bg-white border border-slate-200 rounded-xl hover:bg-slate-50 shadow-xs"
            >
              <Smartphone className="w-3.5 h-3.5 text-blue-600" />
              Buy Airtime / Data
            </Link>
          </div>
        }
      />

      {/* Main Wallet Card */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-8">
        {/* Balance Card */}
        <div className="lg:col-span-2 bg-linear-to-br from-blue-700 via-blue-600 to-indigo-800 text-white rounded-3xl p-6 sm:p-8 shadow-xl relative overflow-hidden flex flex-col justify-between">
          <div className="relative z-10">
            <div className="flex items-center justify-between mb-4">
              <div className="flex items-center gap-2 text-xs text-blue-100 font-semibold uppercase tracking-wider">
                <Wallet className="w-4 h-4" />
                Available Wallet Balance
              </div>
              <button
                onClick={toggleBalancePrivacy}
                className="text-blue-100 hover:text-white p-1 rounded-lg transition-colors"
              >
                {balanceVisible ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>

            <h2 className="text-4xl sm:text-5xl font-extrabold tracking-tight font-mono mb-4">
              {balanceVisible ? formatCurrency(wallet.balance) : '••••••••'}
            </h2>

            <div className="flex items-center gap-2 text-xs text-blue-100">
              <ShieldCheck className="w-4 h-4 text-emerald-300" />
              <span>Double-entry verified ledger • NGN Primary Vault</span>
            </div>
          </div>

          <div className="pt-6 mt-6 border-t border-white/15 grid grid-cols-3 gap-3 text-center">
            <Link
              to="/consumer/transfers"
              className="p-2.5 bg-white/10 hover:bg-white/20 rounded-xl text-xs font-bold transition-colors flex flex-col items-center gap-1 backdrop-blur-xs"
            >
              <ArrowUpRight className="w-4 h-4" />
              Transfer Out
            </Link>
            <Link
              to="/consumer/cards"
              className="p-2.5 bg-white/10 hover:bg-white/20 rounded-xl text-xs font-bold transition-colors flex flex-col items-center gap-1 backdrop-blur-xs"
            >
              <CreditCard className="w-4 h-4" />
              Card Funding
            </Link>
            <Link
              to="/consumer/savings"
              className="p-2.5 bg-white/10 hover:bg-white/20 rounded-xl text-xs font-bold transition-colors flex flex-col items-center gap-1 backdrop-blur-xs"
            >
              <PiggyBank className="w-4 h-4" />
              Save Money
            </Link>
          </div>
        </div>

        {/* Dedicated Virtual Account (DVA) Card */}
        <div className="bg-white rounded-3xl border border-slate-200/80 p-6 shadow-xs flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between mb-4">
              <div className="flex items-center gap-2 text-xs font-bold text-slate-500 uppercase tracking-wider">
                <Building className="w-4 h-4 text-blue-600" />
                Inbound DVA Details
              </div>
              <Badge status={wallet.kycTier} size="sm" />
            </div>

            <div className="p-4 bg-slate-50 rounded-2xl border border-slate-100 mb-4">
              <span className="text-[10px] font-bold uppercase tracking-wider text-slate-400 block mb-1">
                Virtual Account Number
              </span>
              <div className="flex items-center justify-between">
                <span className="text-2xl font-bold font-mono text-slate-900 tracking-wider">
                  {wallet.dvaAccountNumber}
                </span>
                <button
                  onClick={handleCopy}
                  className="p-2 text-slate-500 hover:text-blue-600 hover:bg-slate-200/60 rounded-xl transition-colors"
                  title="Copy Account Number"
                >
                  {copiedAccount ? <Check className="w-4 h-4 text-emerald-600" /> : <Copy className="w-4 h-4" />}
                </button>
              </div>
            </div>

            <div className="space-y-2 text-xs text-slate-600">
              <div className="flex justify-between">
                <span className="text-slate-400">Bank Rail:</span>
                <span className="font-semibold text-slate-800">{wallet.dvaBankName}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">Account Name:</span>
                <span className="font-semibold text-slate-800">{wallet.dvaAccountName}</span>
              </div>
            </div>
          </div>

          <p className="text-[11px] text-slate-400 mt-4 leading-relaxed">
            Direct bank deposits via NIP / NIBSS credit this wallet immediately with automated reconciliation.
          </p>
        </div>
      </div>

      {/* Quick Beneficiary Shortcuts & Recent Ledger Feed */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Transactions Feed */}
        <div className="lg:col-span-2 bg-white p-6 rounded-3xl border border-slate-200/80 shadow-xs">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-sm font-bold text-slate-900">Recent Wallet Transactions</h3>
            <Link to="/consumer/transfers" className="text-xs font-bold text-blue-600 hover:underline">
              View All →
            </Link>
          </div>

          <div className="space-y-3">
            {transactions.map((tx) => (
              <div
                key={tx.id}
                className="flex items-center justify-between p-3.5 rounded-2xl bg-slate-50/70 border border-slate-100 hover:border-slate-200 transition-all text-xs"
              >
                <div className="flex items-center gap-3">
                  <div
                    className={`w-9 h-9 rounded-xl flex items-center justify-center font-bold shrink-0 ${
                      tx.isCredit ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-200/70 text-slate-700'
                    }`}
                  >
                    {tx.isCredit ? <ArrowDownLeft className="w-4 h-4" /> : <ArrowUpRight className="w-4 h-4" />}
                  </div>
                  <div>
                    <span className="font-bold text-slate-900 block truncate max-w-[240px] sm:max-w-md">
                      {tx.description}
                    </span>
                    <span className="text-[11px] text-slate-400 font-mono">
                      {formatDate(tx.date, true)} • {tx.id}
                    </span>
                  </div>
                </div>

                <div className="text-right shrink-0">
                  <span
                    className={`font-mono font-bold text-sm block ${
                      tx.isCredit ? 'text-emerald-700' : 'text-slate-900'
                    }`}
                  >
                    {tx.isCredit ? '+' : '-'}{formatCurrency(tx.amount)}
                  </span>
                  <Badge status={tx.status} size="sm" />
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Benefits & Work Overview */}
        <div className="bg-white p-6 rounded-3xl border border-slate-200/80 shadow-xs space-y-4">
          <h3 className="text-sm font-bold text-slate-900">Workplace &amp; Credit Snapshot</h3>

          <div className="p-4 bg-purple-50/50 rounded-2xl border border-purple-100">
            <span className="text-[10px] font-bold uppercase tracking-wider text-purple-700 block mb-1">
              Affiliated Employer
            </span>
            <h4 className="font-bold text-slate-900 text-sm">Apex Global Technologies</h4>
            <p className="text-xs text-slate-600 mt-1">Senior Software Engineer • L4 Senior Lead</p>
            <Link
              to="/consumer/work"
              className="mt-3 inline-flex items-center text-xs font-bold text-purple-700 hover:underline"
            >
              View Workplace Payslips →
            </Link>
          </div>

          <div className="p-4 bg-amber-50/50 rounded-2xl border border-amber-100">
            <span className="text-[10px] font-bold uppercase tracking-wider text-amber-700 block mb-1">
              Active Salary Advance Loan
            </span>
            <div className="flex justify-between items-center">
              <span className="font-mono font-bold text-slate-900">{formatCurrency(wallet.activeLoans)}</span>
              <span className="text-[10px] font-bold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded">DTI: 9.68% ✓</span>
            </div>
            <Link
              to="/consumer/loans"
              className="mt-2 inline-flex items-center text-xs font-bold text-amber-700 hover:underline"
            >
              Loan Schedule &amp; Calculator →
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
