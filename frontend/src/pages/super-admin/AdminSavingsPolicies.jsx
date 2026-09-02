import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import { formatPercent, formatCurrency, formatDate } from '../../utils/formatters';
import { Sliders, Plus, PiggyBank, Shield } from 'lucide-react';

export default function AdminSavingsPolicies() {
  const [showModal, setShowModal] = useState(false);
  const { showSuccess } = useToast();

  const [policies, setPolicies] = useState([
    {
      id: 'sav-pol-02',
      version: 2,
      tier1Rate: 0.085, // 8.5%
      tier2Rate: 0.115, // 11.5%
      tier3Rate: 0.145, // 14.5%
      earlyWithdrawalPenaltyPrincipal: 0.025, // 2.5% principal forfeiture
      forfeitAccruedInterest: true,
      isActive: true,
      effectiveFromUtc: '2026-08-01T00:00:00Z',
      createdBy: 'Honour Ajani'
    },
    {
      id: 'sav-pol-01',
      version: 1,
      tier1Rate: 0.080,
      tier2Rate: 0.100,
      tier3Rate: 0.120,
      earlyWithdrawalPenaltyPrincipal: 0.025,
      forfeitAccruedInterest: true,
      isActive: false,
      effectiveFromUtc: '2026-06-01T00:00:00Z',
      createdBy: 'Honour Ajani'
    }
  ]);

  const [t1, setT1] = useState('0.09');
  const [t2, setT2] = useState('0.12');
  const [t3, setT3] = useState('0.15');

  const handleCreate = (e) => {
    e.preventDefault();
    const newP = {
      id: `sav-pol-${Date.now()}`,
      version: policies.length + 1,
      tier1Rate: parseFloat(t1),
      tier2Rate: parseFloat(t2),
      tier3Rate: parseFloat(t3),
      earlyWithdrawalPenaltyPrincipal: 0.025,
      forfeitAccruedInterest: true,
      isActive: true,
      effectiveFromUtc: new Date().toISOString(),
      createdBy: 'Honour Ajani'
    };
    setPolicies([newP, ...policies.map((p) => ({ ...p, isActive: false }))]);
    showSuccess('Savings Policy Updated', `Version ${newP.version} is now active.`);
    setShowModal(false);
  };

  const columns = [
    {
      header: 'Policy Version',
      accessor: 'version',
      render: (row) => (
        <div className="flex items-center gap-2">
          <span className="font-bold font-mono">v{row.version}</span>
          {row.isActive && <Badge status="ACTIVE" size="sm" label="Active" />}
        </div>
      )
    },
    {
      header: '30-Day Lock Rate',
      accessor: 'tier1Rate',
      render: (row) => <span className="font-bold text-slate-800">{formatPercent(row.tier1Rate)} p.a.</span>
    },
    {
      header: '90-Day Lock Rate',
      accessor: 'tier2Rate',
      render: (row) => <span className="font-bold text-slate-800">{formatPercent(row.tier2Rate)} p.a.</span>
    },
    {
      header: '365-Day Lock Rate',
      accessor: 'tier3Rate',
      render: (row) => <span className="font-bold text-emerald-700">{formatPercent(row.tier3Rate)} p.a.</span>
    },
    {
      header: 'Early Exit Penalty',
      accessor: 'earlyWithdrawalPenaltyPrincipal',
      render: (row) => (
        <span className="text-slate-600">
          100% Interest + {formatPercent(row.earlyWithdrawalPenaltyPrincipal)} Principal
        </span>
      )
    },
    {
      header: 'Effective Date',
      accessor: 'effectiveFromUtc',
      render: (row) => formatDate(row.effectiveFromUtc, true)
    }
  ];

  return (
    <div>
      <PageHeader
        title="Platform Savings Interest Policies"
        subtitle="Configure daily compound interest accrual schedules, tenure yields (8%–15%), and early liquidation penalty parameters."
        actions={
          <button
            onClick={() => setShowModal(true)}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
          >
            <Plus className="w-3.5 h-3.5" />
            Update Interest Policy
          </button>
        }
      />

      <DataTable
        columns={columns}
        data={policies}
        searchPlaceholder="Search interest policies..."
      />

      {/* Modal */}
      <Modal
        isOpen={showModal}
        onClose={() => setShowModal(false)}
        title="Activate New Savings Interest Policy"
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button
              onClick={() => setShowModal(false)}
              className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl"
            >
              Cancel
            </button>
            <button
              onClick={handleCreate}
              className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl"
            >
              Activate Policy
            </button>
          </div>
        }
      >
        <form onSubmit={handleCreate} className="space-y-4 text-xs text-left">
          <div className="grid grid-cols-3 gap-3">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">30-Day Rate (p.a.)</label>
              <input
                type="number"
                step="0.005"
                value={t1}
                onChange={(e) => setT1(e.target.value)}
                className="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold"
              />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">90-Day Rate (p.a.)</label>
              <input
                type="number"
                step="0.005"
                value={t2}
                onChange={(e) => setT2(e.target.value)}
                className="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold"
              />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">365-Day Rate (p.a.)</label>
              <input
                type="number"
                step="0.005"
                value={t3}
                onChange={(e) => setT3(e.target.value)}
                className="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold"
              />
            </div>
          </div>
        </form>
      </Modal>
    </div>
  );
}
