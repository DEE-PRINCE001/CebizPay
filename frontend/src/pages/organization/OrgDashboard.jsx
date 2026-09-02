import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import StatCard from '../../components/common/StatCard';
import Badge from '../../components/common/Badge';
import { formatCurrency, formatDate } from '../../utils/formatters';
import { useAuth } from '../../context/AuthContext';
import { useToast } from '../../context/ToastContext';
import {
  Wallet,
  Building,
  Users,
  Banknote,
  ArrowUpRight,
  ArrowDownLeft,
  Briefcase,
  Layers,
  Plus,
  ArrowRight,
  TrendingUp,
  Receipt,
  FileCheck,
  CheckCircle2
} from 'lucide-react';
import { Link } from 'react-router-dom';
import PinModal from '../../components/common/PinModal';
import Modal from '../../components/common/Modal';

export default function OrgDashboard() {
  const { activeOrg, balanceVisible } = useAuth();
  const { showSuccess } = useToast();
  const [showFundModal, setShowFundModal] = useState(false);
  const [showPinModal, setShowPinModal] = useState(false);
  const [fundAmount, setFundAmount] = useState('1000000');

  // Stats
  const stats = {
    walletBalance: activeOrg?.balance || 14250000.0,
    totalStaff: 28,
    departmentsCount: 5,
    lastPayrollAmount: 11840000.0,
    lastPayrollDate: '2026-08-28T10:00:00Z',
    activeLoansCount: 6,
    activeLoansTotal: 4800000.0,
    pendingInvoicesTotal: 2450000.0
  };

  // Recent Payroll Runs
  const recentPayrolls = [
    {
      id: 'batch-aug-2026',
      period: 'August 2026 Monthly Payroll',
      mode: 'Pay All (28 Staff)',
      gross: 12450000.0,
      net: 11840000.0,
      status: 'COMPLETED',
      disbursedAt: '2026-08-28T10:15:00Z'
    },
    {
      id: 'batch-jul-2026',
      period: 'July 2026 Monthly Payroll',
      mode: 'Pay All (27 Staff)',
      gross: 11980000.0,
      net: 11420000.0,
      status: 'COMPLETED',
      disbursedAt: '2026-07-28T11:00:00Z'
    }
  ];

  // 12-Month Payroll Spend Breakdown
  const monthlyPayrollTrends = [
    { month: 'Jan', amount: 8.5 },
    { month: 'Feb', amount: 9.0 },
    { month: 'Mar', amount: 9.2 },
    { month: 'Apr', amount: 9.8 },
    { month: 'May', amount: 10.4 },
    { month: 'Jun', amount: 10.9 },
    { month: 'Jul', amount: 11.4 },
    { month: 'Aug', amount: 11.8 },
    { month: 'Sep', amount: 12.1 },
    { month: 'Oct', amount: 12.5 },
    { month: 'Nov', amount: 13.0 },
    { month: 'Dec', amount: 14.2 }
  ];

  const handleFundWallet = () => {
    setShowFundModal(false);
    setShowPinModal(true);
  };

  const handlePinConfirm = (pin) => {
    showSuccess(
      'Corporate Wallet Funded',
      `Successfully deposited ${formatCurrency(fundAmount)} via Monnify Corporate Reserved Account.`
    );
    setShowPinModal(false);
  };

  return (
    <div>
      <PageHeader
        title={`Corporate Treasury & Dashboard`}
        subtitle={`${activeOrg?.name || 'Apex Global Technologies Ltd'} • RC: ${activeOrg?.cacNumber || 'RC-1849204'} • Multi-Currency Ledger`}
        actions={
          <div className="flex items-center gap-2.5">
            <button
              onClick={() => setShowFundModal(true)}
              className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
            >
              <ArrowDownLeft className="w-3.5 h-3.5" />
              Fund Corporate Wallet
            </button>
            <Link
              to="/org/payroll"
              className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-slate-800 bg-white border border-slate-200 rounded-xl hover:bg-slate-50 shadow-xs"
            >
              <Banknote className="w-3.5 h-3.5 text-blue-600" />
              Run Payroll
            </Link>
          </div>
        }
      />

      {/* Top Stat Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <StatCard
          title="Corporate Wallet Balance"
          value={balanceVisible ? formatCurrency(stats.walletBalance) : '••••••••'}
          icon={Wallet}
          iconBg="bg-blue-50 text-blue-600"
          subtitle="Monnify / Dedicated VA"
        />
        <StatCard
          title="Active Workforce (Staff)"
          value={stats.totalStaff}
          icon={Users}
          iconBg="bg-purple-50 text-purple-600"
          subtitle={`${stats.departmentsCount} Functional Departments`}
        />
        <StatCard
          title="Last Payroll Disbursed"
          value={formatCurrency(stats.lastPayrollAmount)}
          icon={Banknote}
          iconBg="bg-emerald-50 text-emerald-600"
          subtitle="August 2026 • Settled"
        />
        <StatCard
          title="Active Corporate Loans"
          value={formatCurrency(stats.activeLoansTotal)}
          icon={TrendingUp}
          iconBg="bg-amber-50 text-amber-600"
          subtitle={`${stats.activeLoansCount} Staff Repaying (33% DTI)`}
        />
      </div>

      {/* Dedicated Virtual Account Card */}
      <div className="bg-linear-to-r from-slate-900 to-slate-800 text-white rounded-3xl p-6 mb-8 shadow-xl relative overflow-hidden">
        <div className="absolute right-0 top-0 bottom-0 w-1/3 bg-blue-600/10 pointer-events-none rounded-r-3xl" />
        <div className="relative z-10 flex flex-col md:flex-row md:items-center justify-between gap-6">
          <div>
            <div className="flex items-center gap-2 text-xs text-blue-400 font-bold uppercase tracking-wider mb-1">
              <Building className="w-4 h-4" />
              Corporate Dedicated Virtual Account (DVA)
            </div>
            <h3 className="text-2xl font-bold tracking-tight font-mono">1029482019</h3>
            <p className="text-xs text-slate-300 mt-1">
              Bank: <strong>Wema Bank / Monnify Settlement Rail</strong> • Account Name: <strong>Apex Global Technologies Ltd</strong>
            </p>
          </div>
          <div className="flex items-center gap-3">
            <button
              onClick={() => {
                navigator.clipboard.writeText('1029482019');
                showSuccess('Copied to Clipboard', 'Account number copied.');
              }}
              className="px-4 py-2.5 bg-white/10 hover:bg-white/20 text-white rounded-xl text-xs font-bold transition-colors border border-white/20 backdrop-blur-xs"
            >
              Copy Account Details
            </button>
            <Link
              to="/org/kyb"
              className="px-4 py-2.5 bg-blue-600 hover:bg-blue-700 text-white rounded-xl text-xs font-bold transition-colors shadow-xs"
            >
              View KYB Status
            </Link>
          </div>
        </div>
      </div>

      {/* 12-Month Payroll Trend & Recent Runs Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* 12-Month Trend */}
        <div className="lg:col-span-2 bg-white p-6 rounded-2xl border border-slate-200/80 shadow-xs">
          <div className="flex items-center justify-between mb-6">
            <div>
              <h3 className="text-sm font-bold text-slate-900">12-Month Corporate Payroll Spend</h3>
              <p className="text-xs text-slate-500 mt-0.5">Historical salary &amp; compensation disbursements</p>
            </div>
            <span className="text-xs font-bold text-slate-700 bg-slate-100 px-2.5 py-1 rounded-lg">
              2026 Fiscal Year
            </span>
          </div>

          <div className="h-44 flex items-end justify-between gap-2 pt-4 pb-2 border-b border-slate-100">
            {monthlyPayrollTrends.map((item, idx) => {
              const heightPct = Math.round((item.amount / 16) * 100);
              return (
                <div key={idx} className="flex-1 flex flex-col items-center gap-1.5 group">
                  <div className="w-full bg-slate-100 rounded-t-md h-32 flex items-end p-0.5 relative">
                    <div
                      style={{ height: `${heightPct}%` }}
                      className="w-full bg-blue-600 rounded-t-sm group-hover:bg-blue-700 transition-all"
                    />
                    <div className="opacity-0 group-hover:opacity-100 absolute -top-8 left-1/2 -translate-x-1/2 bg-slate-900 text-white text-[10px] font-mono py-1 px-1.5 rounded shadow-md pointer-events-none transition-opacity whitespace-nowrap z-10">
                      ₦{item.amount}M
                    </div>
                  </div>
                  <span className="text-[10px] font-semibold text-slate-400 group-hover:text-slate-800 transition-colors">
                    {item.month}
                  </span>
                </div>
              );
            })}
          </div>
          <div className="mt-4 flex items-center justify-between text-xs text-slate-500">
            <span>Aggregated Gross Net Disbursed: ₦134.8 Million</span>
            <Link to="/org/payroll" className="font-bold text-blue-600 hover:underline">
              Payroll Engine →
            </Link>
          </div>
        </div>

        {/* Quick Links / ERP Hub */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
          <div>
            <h3 className="text-sm font-bold text-slate-900 mb-4">Corporate ERP &amp; Operations</h3>
            <div className="space-y-2.5">
              <Link
                to="/org/erp/invoices"
                className="flex items-center justify-between p-3 rounded-xl bg-slate-50 border border-slate-100 hover:border-slate-200 transition-colors group"
              >
                <div className="flex items-center gap-3">
                  <Receipt className="w-4 h-4 text-blue-600" />
                  <div>
                    <span className="text-xs font-bold text-slate-900 block">Invoices &amp; Receipts</span>
                    <span className="text-[11px] text-slate-400">7.5% Statutory VAT billing</span>
                  </div>
                </div>
                <ArrowRight className="w-4 h-4 text-slate-400 group-hover:text-slate-700 transition-colors" />
              </Link>

              <Link
                to="/org/erp/inventory"
                className="flex items-center justify-between p-3 rounded-xl bg-slate-50 border border-slate-100 hover:border-slate-200 transition-colors group"
              >
                <div className="flex items-center gap-3">
                  <Layers className="w-4 h-4 text-purple-600" />
                  <div>
                    <span className="text-xs font-bold text-slate-900 block">Inventory &amp; Stock</span>
                    <span className="text-[11px] text-slate-400">SKU tracking &amp; valuation</span>
                  </div>
                </div>
                <ArrowRight className="w-4 h-4 text-slate-400 group-hover:text-slate-700 transition-colors" />
              </Link>

              <Link
                to="/org/erp/vouchers"
                className="flex items-center justify-between p-3 rounded-xl bg-slate-50 border border-slate-100 hover:border-slate-200 transition-colors group"
              >
                <div className="flex items-center gap-3">
                  <FileCheck className="w-4 h-4 text-emerald-600" />
                  <div>
                    <span className="text-xs font-bold text-slate-900 block">Payment Vouchers</span>
                    <span className="text-[11px] text-slate-400">Company disbursement sign-off</span>
                  </div>
                </div>
                <ArrowRight className="w-4 h-4 text-slate-400 group-hover:text-slate-700 transition-colors" />
              </Link>

              <Link
                to="/org/recruitment"
                className="flex items-center justify-between p-3 rounded-xl bg-slate-50 border border-slate-100 hover:border-slate-200 transition-colors group"
              >
                <div className="flex items-center gap-3">
                  <Briefcase className="w-4 h-4 text-amber-600" />
                  <div>
                    <span className="text-xs font-bold text-slate-900 block">Recruitment &amp; Jobs</span>
                    <span className="text-[11px] text-slate-400">Job postings &amp; candidate review</span>
                  </div>
                </div>
                <ArrowRight className="w-4 h-4 text-slate-400 group-hover:text-slate-700 transition-colors" />
              </Link>
            </div>
          </div>
        </div>
      </div>

      {/* Fund Modal */}
      <Modal
        isOpen={showFundModal}
        onClose={() => setShowFundModal(false)}
        title="Fund Corporate Wallet"
        subtitle="Simulate inbound transfer via Dedicated Virtual Account or Monnify direct settlement."
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button
              onClick={() => setShowFundModal(false)}
              className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl"
            >
              Cancel
            </button>
            <button
              onClick={handleFundWallet}
              className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700"
            >
              Proceed to PIN Verification
            </button>
          </div>
        }
      >
        <div className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1.5">Funding Amount (₦)</label>
            <input
              type="number"
              value={fundAmount}
              onChange={(e) => setFundAmount(e.target.value)}
              className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-mono text-base font-bold"
            />
          </div>
          <div className="p-3 bg-slate-50 rounded-xl border border-slate-200 text-slate-600">
            Deposits are immediately posted to the organization's Double-Entry Ledger asset account and ready for payroll disbursement.
          </div>
        </div>
      </Modal>

      {/* PIN Modal */}
      <PinModal
        isOpen={showPinModal}
        onClose={() => setShowPinModal(false)}
        onConfirm={handlePinConfirm}
        title="Authorize Corporate Funding"
        amount={formatCurrency(fundAmount)}
        recipient="Apex Global Technologies Corporate Wallet"
      />
    </div>
  );
}
