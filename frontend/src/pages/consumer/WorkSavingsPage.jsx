import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Tabs from '../../components/common/Tabs';
import Modal from '../../components/common/Modal';
import PinModal from '../../components/common/PinModal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatPercent, formatDate } from '../../utils/formatters';
import { PiggyBank, Plus, ArrowUpRight, ArrowDownLeft, AlertTriangle, ShieldCheck, Sparkles } from 'lucide-react';

export default function WorkSavingsPage() {
  const [activeTab, setActiveTab] = useState('my-savings'); // 'my-savings' | 'browse'
  const [showOpenModal, setShowOpenModal] = useState(false);
  const [showContributeModal, setShowContributeModal] = useState(false);
  const [showWithdrawModal, setShowWithdrawModal] = useState(false);
  const [showPinModal, setShowPinModal] = useState(false);
  const [selectedPlan, setSelectedPlan] = useState(null);

  const [contributeAmount, setContributeAmount] = useState('50000');
  const [goalTitle, setGoalTitle] = useState('');
  const [targetAmount, setTargetAmount] = useState('600000');
  const [lockTenureDays, setLockTenureDays] = useState(90);

  const { showSuccess, showError } = useToast();

  // Active User Savings Accounts
  const [myAccounts, setMyAccounts] = useState([
    {
      id: 'SAV-ACC-01',
      title: 'Tech Equipment & Upskilling Fund (Sponsored)',
      type: 'CORPORATE_SPONSORED',
      balance: 250000.0,
      targetAmount: 500000.0,
      interestRate: 0.115, // 11.5% p.a.
      accruedInterest: 7187.50,
      lockDays: 180,
      startDate: '2026-06-01T00:00:00Z',
      maturityDate: '2026-11-28T00:00:00Z',
      status: 'ACTIVE'
    },
    {
      id: 'SAV-ACC-02',
      title: 'High-Yield Fixed Lock Vault (365 Days)',
      type: 'INDIVIDUAL_FIXED',
      balance: 1000000.0,
      targetAmount: 1000000.0,
      interestRate: 0.145, // 14.5% p.a.
      accruedInterest: 36250.0,
      lockDays: 365,
      startDate: '2026-05-15T00:00:00Z',
      maturityDate: '2027-05-15T00:00:00Z',
      status: 'ACTIVE'
    }
  ]);

  // Catalog of Available Plans
  const availablePlans = [
    {
      id: 'plan-30d',
      name: 'Flexi 30-Day Vault',
      annualRate: 0.085, // 8.5%
      minAmount: 10000.0,
      tenure: '30 Days',
      description: 'Short term liquid savings with daily interest compounding.'
    },
    {
      id: 'plan-90d',
      name: 'Growth 90-Day Lock',
      annualRate: 0.115, // 11.5%
      minAmount: 25000.0,
      tenure: '90 Days',
      description: 'Quarterly fixed lock ideal for building emergency funds.'
    },
    {
      id: 'plan-365d',
      name: 'Annual Wealth Builder',
      annualRate: 0.145, // 14.5%
      minAmount: 50000.0,
      tenure: '365 Days',
      description: 'Maximum guaranteed annual yield across all platform vaults.'
    }
  ];

  const handleOpenAccount = (e) => {
    e.preventDefault();
    const rate = lockTenureDays === 365 ? 0.145 : lockTenureDays === 90 ? 0.115 : 0.085;
    const newAcc = {
      id: `SAV-ACC-${Date.now().toString().slice(-4)}`,
      title: goalTitle || 'My Fixed-Lock Savings',
      type: 'INDIVIDUAL_FIXED',
      balance: 0,
      targetAmount: parseFloat(targetAmount),
      interestRate: rate,
      accruedInterest: 0,
      lockDays: lockTenureDays,
      startDate: new Date().toISOString(),
      maturityDate: new Date(Date.now() + lockTenureDays * 24 * 3600 * 1000).toISOString(),
      status: 'ACTIVE'
    };
    setMyAccounts((prev) => [newAcc, ...prev]);
    showSuccess('Savings Vault Created', `${newAcc.title} opened with ${formatPercent(rate)} annual yield.`);
    setShowOpenModal(false);
    setGoalTitle('');
  };

  const handleContribute = (account) => {
    setSelectedPlan(account);
    setShowContributeModal(true);
  };

  const handleWithdrawEarly = (account) => {
    setSelectedPlan(account);
    setShowWithdrawModal(true);
  };

  const handleConfirmContribute = () => {
    setShowContributeModal(false);
    const added = parseFloat(contributeAmount);
    setMyAccounts((prev) =>
      prev.map((a) => (a.id === selectedPlan.id ? { ...a, balance: a.balance + added } : a))
    );
    showSuccess('Contribution Deposited', `Added ${formatCurrency(added)} to ${selectedPlan.title}.`);
  };

  const handleConfirmWithdraw = () => {
    setShowWithdrawModal(false);
    setShowPinModal(true);
  };

  const handlePinConfirm = (pin) => {
    setShowPinModal(false);
    const penaltyPrincipal = selectedPlan.balance * 0.025; // 2.5% principal penalty
    const netPayout = selectedPlan.balance - penaltyPrincipal; // 100% interest forfeited

    setMyAccounts((prev) => prev.filter((a) => a.id !== selectedPlan.id));
    showSuccess(
      'Early Liquidation Executed',
      `Credited ${formatCurrency(netPayout)} to wallet. (Forfeited accrued interest + 2.5% principal penalty).`
    );
  };

  const columns = [
    {
      header: 'Savings Vault',
      accessor: 'title',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.title}</span>
          <span className="text-[11px] text-slate-400 font-mono">{row.id} • {row.type.replace('_', ' ')}</span>
        </div>
      )
    },
    {
      header: 'Current Principal',
      accessor: 'balance',
      render: (row) => <span className="font-mono font-bold text-slate-900 text-sm">{formatCurrency(row.balance)}</span>
    },
    {
      header: 'Annual Yield',
      accessor: 'interestRate',
      render: (row) => (
        <span className="font-bold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded text-xs">
          {formatPercent(row.interestRate)} p.a.
        </span>
      )
    },
    {
      header: 'Accrued Interest',
      accessor: 'accruedInterest',
      render: (row) => <span className="font-mono font-bold text-blue-700">+{formatCurrency(row.accruedInterest)}</span>
    },
    {
      header: 'Maturity Date',
      accessor: 'maturityDate',
      render: (row) => formatDate(row.maturityDate)
    },
    {
      header: 'Status',
      accessor: 'status',
      render: (row) => <Badge status={row.status} size="sm" />
    },
    {
      header: 'Actions',
      align: 'right',
      render: (row) => (
        <div className="flex items-center justify-end gap-1.5">
          <button
            onClick={() => handleContribute(row)}
            className="px-2.5 py-1 bg-blue-50 text-blue-700 hover:bg-blue-100 rounded-lg text-xs font-bold transition-colors"
          >
            Deposit
          </button>
          <button
            onClick={() => handleWithdrawEarly(row)}
            className="px-2.5 py-1 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg text-xs font-bold transition-colors"
          >
            Liquidate
          </button>
        </div>
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title="High-Yield Savings &amp; Fixed Locks"
        subtitle="Automated high-yield interest accounts (8.5%–14.5% p.a.) with daily compound accrual and employer contribution matching."
        actions={
          <button
            onClick={() => setShowOpenModal(true)}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
          >
            <Plus className="w-3.5 h-3.5" />
            Open Fixed-Lock Vault
          </button>
        }
      />

      <Tabs
        tabs={[
          { id: 'my-savings', label: 'My Savings Accounts', count: myAccounts.length, icon: PiggyBank },
          { id: 'browse', label: 'Explore High-Yield Plans', icon: Sparkles }
        ]}
        activeTab={activeTab}
        onChange={setActiveTab}
        className="mb-6"
      />

      {activeTab === 'my-savings' && (
        <DataTable
          columns={columns}
          data={myAccounts}
          searchPlaceholder="Search my savings accounts..."
        />
      )}

      {activeTab === 'browse' && (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 text-xs text-left">
          {availablePlans.map((p) => (
            <div key={p.id} className="bg-white p-6 rounded-3xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
              <div>
                <div className="flex items-center justify-between mb-3">
                  <span className="font-bold text-slate-900 text-sm">{p.name}</span>
                  <span className="font-bold text-emerald-800 bg-emerald-50 px-2.5 py-1 rounded-xl border border-emerald-200 font-mono">
                    {formatPercent(p.annualRate)} p.a.
                  </span>
                </div>
                <p className="text-slate-500 leading-relaxed mb-4">{p.description}</p>
                <div className="p-3 bg-slate-50 rounded-xl border border-slate-100 mb-4 space-y-1 text-slate-600">
                  <div className="flex justify-between">
                    <span>Lock Tenure:</span>
                    <strong className="text-slate-800">{p.tenure}</strong>
                  </div>
                  <div className="flex justify-between">
                    <span>Minimum Deposit:</span>
                    <strong className="text-slate-800 font-mono">{formatCurrency(p.minAmount)}</strong>
                  </div>
                </div>
              </div>
              <button
                onClick={() => {
                  setLockTenureDays(p.id === 'plan-365d' ? 365 : p.id === 'plan-90d' ? 90 : 30);
                  setShowOpenModal(true);
                }}
                className="w-full py-2.5 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-xl shadow-xs transition-colors text-center"
              >
                Open This Vault
              </button>
            </div>
          ))}
        </div>
      )}

      {/* Open Account Modal */}
      <Modal
        isOpen={showOpenModal}
        onClose={() => setShowOpenModal(false)}
        title="Open Fixed-Lock Savings Vault"
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowOpenModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
            <button onClick={handleOpenAccount} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Create Vault</button>
          </div>
        }
      >
        <form onSubmit={handleOpenAccount} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Savings Goal Title</label>
            <input type="text" required value={goalTitle} onChange={(e) => setGoalTitle(e.target.value)} placeholder="e.g. Annual Vacation / Wedding Fund" className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-bold" />
          </div>
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Target Savings Goal (₦)</label>
            <input type="number" required value={targetAmount} onChange={(e) => setTargetAmount(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold" />
          </div>
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Lock Duration</label>
            <select value={lockTenureDays} onChange={(e) => setLockTenureDays(parseInt(e.target.value))} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-bold">
              <option value={30}>30 Days (8.5% Annual Interest)</option>
              <option value={90}>90 Days (11.5% Annual Interest)</option>
              <option value={365}>365 Days (14.5% Annual Interest)</option>
            </select>
          </div>
        </form>
      </Modal>

      {/* Contribute Modal */}
      {selectedPlan && (
        <Modal
          isOpen={showContributeModal}
          onClose={() => setShowContributeModal(false)}
          title={`Deposit Funds: ${selectedPlan.title}`}
          footer={
            <div className="flex items-center justify-end gap-3 w-full">
              <button onClick={() => setShowContributeModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
              <button onClick={handleConfirmContribute} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Confirm Deposit</button>
            </div>
          }
        >
          <div className="space-y-4 text-xs text-left">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Deposit Amount (₦)</label>
              <input type="number" required value={contributeAmount} onChange={(e) => setContributeAmount(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono text-base font-bold" />
            </div>
          </div>
        </Modal>
      )}

      {/* Early Liquidation Preview Modal */}
      {selectedPlan && (
        <Modal
          isOpen={showWithdrawModal}
          onClose={() => setShowWithdrawModal(false)}
          title={`Early Liquidation: ${selectedPlan.title}`}
          subtitle="Early withdrawal before maturity incurs statutory penalty per PRD guidelines."
          footer={
            <div className="flex items-center justify-end gap-3 w-full">
              <button onClick={() => setShowWithdrawModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
              <button onClick={handleConfirmWithdraw} className="px-5 py-2 text-xs font-bold text-white bg-rose-600 rounded-xl">Proceed with Penalty</button>
            </div>
          }
        >
          <div className="space-y-4 text-xs text-left">
            <div className="p-4 bg-rose-50 rounded-2xl border border-rose-200 text-rose-900 space-y-2 font-mono">
              <div className="flex justify-between">
                <span className="font-sans">Current Principal:</span>
                <span>{formatCurrency(selectedPlan.balance)}</span>
              </div>
              <div className="flex justify-between text-rose-700">
                <span className="font-sans">Forfeited Accrued Interest:</span>
                <span>-{formatCurrency(selectedPlan.accruedInterest)} (100% Forfeit)</span>
              </div>
              <div className="flex justify-between text-rose-700">
                <span className="font-sans">Principal Penalty (2.5%):</span>
                <span>-{formatCurrency(selectedPlan.balance * 0.025)}</span>
              </div>
              <div className="flex justify-between text-base font-bold text-slate-900 pt-2 border-t border-rose-200">
                <span className="font-sans">Net Liquidated to Wallet:</span>
                <span>{formatCurrency(selectedPlan.balance * 0.975)}</span>
              </div>
            </div>
          </div>
        </Modal>
      )}

      {/* PIN Modal */}
      <PinModal
        isOpen={showPinModal}
        onClose={() => setShowPinModal(false)}
        onConfirm={handlePinConfirm}
        title="Authorize Savings Liquidation"
        amount={selectedPlan ? formatCurrency(selectedPlan.balance * 0.975) : '0.00'}
        recipient="Personal Wallet"
      />
    </div>
  );
}
