import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatPercent, formatDate } from '../../utils/formatters';
import { PiggyBank, Plus, Users, Calendar, Sparkles } from 'lucide-react';

export default function OrgSavings() {
  const [showCreateModal, setShowCreateModal] = useState(false);
  const { showSuccess } = useToast();

  const [title, setTitle] = useState('');
  const [targetAmount, setTargetAmount] = useState('500000');
  const [frequency, setFrequency] = useState('MONTHLY');
  const [corporateMatchPct, setCorporateMatchPct] = useState('0.05'); // 5% matching

  const [plans, setPlans] = useState([
    {
      id: 'sav-org-01',
      title: 'Tech Equipment & Upskilling Fund',
      targetAmount: 500000.0,
      frequency: 'MONTHLY',
      corporateMatchPct: 0.05,
      participantsCount: 14,
      totalSaved: 4200000.0,
      status: 'ACTIVE',
      createdAt: '2026-05-01T00:00:00Z'
    },
    {
      id: 'sav-org-02',
      title: 'Annual Corporate Holiday Savings (December)',
      targetAmount: 1200000.0,
      frequency: 'MONTHLY',
      corporateMatchPct: 0.10,
      participantsCount: 22,
      totalSaved: 18400000.0,
      status: 'ACTIVE',
      createdAt: '2026-01-01T00:00:00Z'
    }
  ]);

  const handleCreate = (e) => {
    e.preventDefault();
    const newP = {
      id: `sav-org-${Date.now()}`,
      title,
      targetAmount: parseFloat(targetAmount),
      frequency,
      corporateMatchPct: parseFloat(corporateMatchPct),
      participantsCount: 0,
      totalSaved: 0,
      status: 'ACTIVE',
      createdAt: new Date().toISOString()
    };
    setPlans((prev) => [newP, ...prev]);
    showSuccess('Sponsored Scheme Created', `${title} is now open for employee enrollment.`);
    setShowCreateModal(false);
    setTitle('');
  };

  const columns = [
    {
      header: 'Scheme Name',
      accessor: 'title',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.title}</span>
          <span className="text-[11px] text-slate-400 font-mono">{row.id}</span>
        </div>
      )
    },
    {
      header: 'Target Goal Amount',
      accessor: 'targetAmount',
      render: (row) => <span className="font-mono font-bold text-slate-900">{formatCurrency(row.targetAmount)}</span>
    },
    {
      header: 'Corporate Match',
      accessor: 'corporateMatchPct',
      render: (row) => (
        <span className="font-bold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded text-xs">
          +{formatPercent(row.corporateMatchPct)} Match
        </span>
      )
    },
    {
      header: 'Enrolled Employees',
      accessor: 'participantsCount',
      render: (row) => <span className="font-bold text-slate-800">{row.participantsCount} Staff</span>
    },
    {
      header: 'Total Pool Saved',
      accessor: 'totalSaved',
      render: (row) => <span className="font-mono font-bold text-blue-700">{formatCurrency(row.totalSaved)}</span>
    },
    {
      header: 'Status',
      accessor: 'status',
      render: (row) => <Badge status={row.status} size="sm" />
    }
  ];

  return (
    <div>
      <PageHeader
        title="Corporate-Sponsored Savings Schemes"
        subtitle="Incentivize employee financial wellness with employer matching contributions and automated payroll deductions."
        actions={
          <button
            onClick={() => setShowCreateModal(true)}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
          >
            <Plus className="w-3.5 h-3.5" />
            Create Sponsored Scheme
          </button>
        }
      />

      <DataTable
        columns={columns}
        data={plans}
        searchPlaceholder="Search savings schemes..."
      />

      {/* Modal */}
      <Modal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        title="Create Corporate-Sponsored Savings Scheme"
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowCreateModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
            <button onClick={handleCreate} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Create Scheme</button>
          </div>
        }
      >
        <form onSubmit={handleCreate} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Scheme Title</label>
            <input type="text" required value={title} onChange={(e) => setTitle(e.target.value)} placeholder="e.g. End of Year Bonus Match Pool" className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Individual Target Goal (₦)</label>
              <input type="number" required value={targetAmount} onChange={(e) => setTargetAmount(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold" />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Employer Contribution Match Rate</label>
              <input type="number" step="0.01" required value={corporateMatchPct} onChange={(e) => setCorporateMatchPct(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono" />
            </div>
          </div>
        </form>
      </Modal>
    </div>
  );
}
