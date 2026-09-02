import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Tabs from '../../components/common/Tabs';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatPercent, formatDate } from '../../utils/formatters';
import { Percent, ArrowRightLeft, Building2, Plus, Sliders, Shield } from 'lucide-react';

export default function AdminFeePolicies() {
  const [activeTab, setActiveTab] = useState('peer'); // 'peer' | 'bank' | 'platform'
  const [showCreateModal, setShowCreateModal] = useState(false);
  const { showSuccess } = useToast();

  const [mode, setMode] = useState('Percentage');
  const [percentageRate, setPercentageRate] = useState('0.015');
  const [minFee, setMinFee] = useState('50');
  const [maxFee, setMaxFee] = useState('1500');
  const [feeBearer, setFeeBearer] = useState('CUSTOMER_PAYS');

  // Peer transfer policies
  const [peerPolicies, setPeerPolicies] = useState([
    {
      id: 'pol-peer-01',
      version: 2,
      mode: 'Percentage',
      percentageRate: 0.005,
      minimumFee: 20.0,
      maximumFee: 500.0,
      isActive: true,
      createdAt: '2026-08-01T00:00:00Z',
      createdBy: 'Honour Ajani'
    },
    {
      id: 'pol-peer-00',
      version: 1,
      mode: 'Free',
      percentageRate: null,
      minimumFee: 0,
      maximumFee: 0,
      isActive: false,
      createdAt: '2026-07-01T00:00:00Z',
      createdBy: 'Honour Ajani'
    }
  ]);

  // Bank transfer policies
  const [bankPolicies, setBankPolicies] = useState([
    {
      id: 'pol-bank-01',
      version: 1,
      mode: 'Percentage',
      percentageRate: 0.012,
      minimumFee: 50.0,
      maximumFee: 2000.0,
      isActive: true,
      createdAt: '2026-08-01T00:00:00Z',
      createdBy: 'Honour Ajani'
    }
  ]);

  const handleCreatePolicy = (e) => {
    e.preventDefault();
    const newPol = {
      id: `pol-${Date.now()}`,
      version: (activeTab === 'peer' ? peerPolicies.length : bankPolicies.length) + 1,
      mode,
      percentageRate: mode === 'Percentage' ? parseFloat(percentageRate) : null,
      minimumFee: parseFloat(minFee) || 0,
      maximumFee: parseFloat(maxFee) || 0,
      isActive: true,
      createdAt: new Date().toISOString(),
      createdBy: 'Honour Ajani'
    };

    if (activeTab === 'peer') {
      setPeerPolicies((prev) => [newPol, ...prev.map((p) => ({ ...p, isActive: false }))]);
    } else {
      setBankPolicies((prev) => [newPol, ...prev.map((p) => ({ ...p, isActive: false }))]);
    }

    showSuccess('Fee Policy Activated', `New Version ${newPol.version} is now the single source of truth.`);
    setShowCreateModal(false);
  };

  const columns = [
    {
      header: 'Policy Version',
      accessor: 'version',
      render: (row) => (
        <div className="flex items-center gap-2">
          <span className="font-bold text-slate-900 font-mono">v{row.version}</span>
          {row.isActive && <Badge status="ACTIVE" size="sm" label="Live / Authoritative" />}
        </div>
      )
    },
    {
      header: 'Calculation Mode',
      accessor: 'mode',
      render: (row) => <span className="font-semibold text-slate-800">{row.mode}</span>
    },
    {
      header: 'Rate',
      accessor: 'percentageRate',
      render: (row) => (row.mode === 'Free' ? 'Free (0%)' : formatPercent(row.percentageRate))
    },
    {
      header: 'Floor (Min Fee)',
      accessor: 'minimumFee',
      render: (row) => formatCurrency(row.minimumFee)
    },
    {
      header: 'Ceiling (Max Fee)',
      accessor: 'maximumFee',
      render: (row) => formatCurrency(row.maximumFee)
    },
    {
      header: 'Effective Date',
      accessor: 'createdAt',
      render: (row) => formatDate(row.createdAt, true)
    },
    {
      header: 'Author',
      accessor: 'createdBy',
      render: (row) => <span className="text-slate-600">{row.createdBy}</span>
    }
  ];

  return (
    <div>
      <PageHeader
        title="Fee Economics &amp; Policy Engine"
        subtitle="Configure sovereign platform fee models and economic burden allocation (Customer Pays, Deduct from Funds, Platform Absorbs)."
        actions={
          <button
            onClick={() => setShowCreateModal(true)}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 transition-all shadow-xs"
          >
            <Plus className="w-3.5 h-3.5" />
            Create Policy Version
          </button>
        }
      />

      <Tabs
        tabs={[
          { id: 'peer', label: 'Peer Wallet Transfers', icon: ArrowRightLeft },
          { id: 'bank', label: 'Outbound Bank Payouts', icon: Building2 },
          { id: 'platform', label: 'Economic Bearer Models', icon: Sliders }
        ]}
        activeTab={activeTab}
        onChange={setActiveTab}
        className="mb-6"
      />

      {activeTab === 'peer' && (
        <DataTable
          columns={columns}
          data={peerPolicies}
          searchPlaceholder="Search peer transfer policies..."
        />
      )}

      {activeTab === 'bank' && (
        <DataTable
          columns={columns}
          data={bankPolicies}
          searchPlaceholder="Search bank transfer policies..."
        />
      )}

      {activeTab === 'platform' && (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div className="bg-white p-6 rounded-2xl border border-slate-200/80 shadow-xs">
            <h4 className="font-bold text-sm text-slate-900 mb-2">1. CUSTOMER_PAYS (Default)</h4>
            <p className="text-xs text-slate-500 leading-relaxed mb-4">
              The fee is added on top of requested amount. E.g., for a ₦100,000 transfer with ₦700 fee, the customer's wallet is debited ₦100,700, and ₦100,000 is dispatched.
            </p>
            <Badge status="ACTIVE" size="sm" label="Active Standard" />
          </div>

          <div className="bg-white p-6 rounded-2xl border border-slate-200/80 shadow-xs">
            <h4 className="font-bold text-sm text-slate-900 mb-2">2. DEDUCT_FROM_FUNDS</h4>
            <p className="text-xs text-slate-500 leading-relaxed mb-4">
              The fee is deducted from gross inbound funds. E.g., ₦100,000 incoming card deposit yields ₦99,300 credited to customer wallet.
            </p>
            <Badge status="ACTIVE" size="sm" label="Active Funding Model" />
          </div>

          <div className="bg-white p-6 rounded-2xl border border-slate-200/80 shadow-xs">
            <h4 className="font-bold text-sm text-slate-900 mb-2">3. PLATFORM_ABSORBS</h4>
            <p className="text-xs text-slate-500 leading-relaxed mb-4">
              Customer receives full gross sum (₦100,000 = ₦100,000 credited). CebizPay platform absorbs upstream gateway processing costs.
            </p>
            <Badge status="DRAFT" size="sm" label="Configurable Option" />
          </div>
        </div>
      )}

      {/* Create Policy Modal */}
      <Modal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        title={`Activate New ${activeTab === 'peer' ? 'Peer Transfer' : 'Bank Payout'} Fee Policy`}
        subtitle="Creating a new version atomically supersedes prior versions and writes an immutable audit record."
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button
              onClick={() => setShowCreateModal(false)}
              className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl"
            >
              Cancel
            </button>
            <button
              onClick={handleCreatePolicy}
              className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700"
            >
              Activate Policy
            </button>
          </div>
        }
      >
        <form onSubmit={handleCreatePolicy} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1.5">Calculation Mode</label>
            <select
              value={mode}
              onChange={(e) => setMode(e.target.value)}
              className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-bold text-slate-800"
            >
              <option value="Percentage">Percentage with Min/Max Caps</option>
              <option value="Free">Free (0% Platform Fee)</option>
            </select>
          </div>

          {mode === 'Percentage' && (
            <>
              <div>
                <label className="block font-semibold text-slate-700 mb-1.5">Percentage Rate (e.g. 0.015 = 1.5%)</label>
                <input
                  type="number"
                  step="0.001"
                  required
                  value={percentageRate}
                  onChange={(e) => setPercentageRate(e.target.value)}
                  className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-mono"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-semibold text-slate-700 mb-1.5">Minimum Fee Floor (₦)</label>
                  <input
                    type="number"
                    required
                    value={minFee}
                    onChange={(e) => setMinFee(e.target.value)}
                    className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-mono"
                  />
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1.5">Maximum Fee Ceiling (₦)</label>
                  <input
                    type="number"
                    required
                    value={maxFee}
                    onChange={(e) => setMaxFee(e.target.value)}
                    className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-mono"
                  />
                </div>
              </div>
            </>
          )}
        </form>
      </Modal>
    </div>
  );
}
