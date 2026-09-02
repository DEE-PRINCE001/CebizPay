import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Tabs from '../../components/common/Tabs';
import Modal from '../../components/common/Modal';
import PinModal from '../../components/common/PinModal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatPercent, formatDate } from '../../utils/formatters';
import { BadgePercent, Plus, CheckCircle, XCircle, Sliders, AlertCircle, FileText } from 'lucide-react';

export default function OrgLoans() {
  const [activeTab, setActiveTab] = useState('applications'); // 'applications' | 'plans' | 'contracts'
  const [showCreatePlanModal, setShowCreatePlanModal] = useState(false);
  const [showPinModal, setShowPinModal] = useState(false);
  const [selectedApplication, setSelectedApplication] = useState(null);
  const [declineReason, setDeclineReason] = useState('');
  const [showDeclineModal, setShowDeclineModal] = useState(false);
  const { showSuccess, showError } = useToast();

  // New Plan form state
  const [planName, setPlanName] = useState('');
  const [interestRate, setInterestRate] = useState('0.035'); // 3.5%
  const [maxTenureMonths, setMaxTenureMonths] = useState('6');
  const [maxAmount, setMaxAmount] = useState('1500000');

  // Corporate Loan Plans
  const [plans, setPlans] = useState([
    {
      id: 'plan-01',
      name: 'Emergency Salary Advance (6-Month)',
      interestRate: 0.035, // 3.5% monthly
      maxTenureMonths: 6,
      maxAmount: 1500000.0,
      currency: 'NGN',
      isActive: true,
      createdAt: '2026-06-01T00:00:00Z'
    },
    {
      id: 'plan-02',
      name: 'Annual Equipment & Asset Advance',
      interestRate: 0.025,
      maxTenureMonths: 12,
      maxAmount: 3000000.0,
      currency: 'NGN',
      isActive: true,
      createdAt: '2026-07-01T00:00:00Z'
    }
  ]);

  // Loan Applications Queue
  const [applications, setApplications] = useState([
    {
      id: 'app-01',
      staffName: 'Amina Adeleke',
      email: 'amina.adeleke@example.com',
      monthlySalary: 1250000.0,
      requestedAmount: 600000.0,
      tenureMonths: 6,
      monthlyDeduction: 121000.0,
      dtiRatio: 0.0968, // 9.68% (under 33% statutory cap)
      reason: 'Home Appliance & Relocation Expenses',
      status: 'PENDING',
      appliedAt: '2026-09-01T14:30:00Z'
    },
    {
      id: 'app-02',
      staffName: 'Emeka Nwosu',
      email: 'emeka.n@apextech.com',
      monthlySalary: 850000.0,
      requestedAmount: 800000.0,
      tenureMonths: 4,
      monthlyDeduction: 228000.0,
      dtiRatio: 0.268, // 26.8% (under 33% cap)
      reason: 'Professional Cloud Certification',
      status: 'PENDING',
      appliedAt: '2026-09-01T15:00:00Z'
    }
  ]);

  // Active Contracts
  const [contracts, setContracts] = useState([
    {
      id: 'contract-9921',
      staffName: 'Amina Adeleke',
      principal: 600000.0,
      totalRepayable: 726000.0,
      remainingBalance: 484000.0,
      monthlyInstallment: 121000.0,
      remainingMonths: 4,
      status: 'ACTIVE',
      disbursedAt: '2026-07-01T00:00:00Z'
    }
  ]);

  const handleCreatePlan = (e) => {
    e.preventDefault();
    const newPlan = {
      id: `plan-${Date.now()}`,
      name: planName,
      interestRate: parseFloat(interestRate),
      maxTenureMonths: parseInt(maxTenureMonths),
      maxAmount: parseFloat(maxAmount),
      currency: 'NGN',
      isActive: true,
      createdAt: new Date().toISOString()
    };
    setPlans((prev) => [newPlan, ...prev]);
    showSuccess('Loan Plan Created', `${planName} is now available for staff loan requests.`);
    setShowCreatePlanModal(false);
    setPlanName('');
  };

  const handleApproveApplication = (app) => {
    setSelectedApplication(app);
    setShowPinModal(true);
  };

  const handlePinConfirm = (pin) => {
    setShowPinModal(false);
    setApplications((prev) =>
      prev.map((a) => (a.id === selectedApplication.id ? { ...a, status: 'APPROVED' } : a))
    );
    showSuccess(
      'Loan Approved & Disbursed',
      `₦${selectedApplication.requestedAmount.toLocaleString()} disbursed to ${selectedApplication.staffName}'s personal wallet.`
    );
  };

  const handleDecline = () => {
    if (!declineReason) {
      showError('Reason Required', 'Please provide reason for declining loan.');
      return;
    }
    setApplications((prev) =>
      prev.map((a) => (a.id === selectedApplication.id ? { ...a, status: 'REJECTED' } : a))
    );
    showSuccess('Loan Declined', 'Application rejected.');
    setShowDeclineModal(false);
    setDeclineReason('');
  };

  const appColumns = [
    {
      header: 'Staff Member',
      accessor: 'staffName',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.staffName}</span>
          <span className="text-[11px] text-slate-400">{row.email}</span>
        </div>
      )
    },
    {
      header: 'Principal Requested',
      accessor: 'requestedAmount',
      render: (row) => <span className="font-mono font-bold text-slate-900">{formatCurrency(row.requestedAmount)}</span>
    },
    {
      header: 'Tenure',
      accessor: 'tenureMonths',
      render: (row) => <span className="text-slate-700 font-medium">{row.tenureMonths} Months</span>
    },
    {
      header: 'Monthly Deduction (DTI)',
      accessor: 'monthlyDeduction',
      render: (row) => (
        <div>
          <span className="font-mono font-bold text-slate-800 block">{formatCurrency(row.monthlyDeduction)}/mo</span>
          <span className={`text-[10px] font-bold ${row.dtiRatio > 0.33 ? 'text-rose-600' : 'text-emerald-600'}`}>
            DTI: {formatPercent(row.dtiRatio)} {row.dtiRatio <= 0.33 ? '✓ (Cap: 33%)' : '⚠ Exceeds Cap'}
          </span>
        </div>
      )
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
          {row.status === 'PENDING' && (
            <>
              <button
                onClick={() => handleApproveApplication(row)}
                className="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-bold transition-colors shadow-xs"
              >
                Approve &amp; Disburse
              </button>
              <button
                onClick={() => {
                  setSelectedApplication(row);
                  setShowDeclineModal(true);
                }}
                className="px-3 py-1.5 bg-rose-50 hover:bg-rose-100 text-rose-700 border border-rose-200 rounded-lg text-xs font-bold transition-colors"
              >
                Decline
              </button>
            </>
          )}
        </div>
      )
    }
  ];

  const planColumns = [
    {
      header: 'Loan Plan Name',
      accessor: 'name',
      render: (row) => <span className="font-bold text-slate-900">{row.name}</span>
    },
    {
      header: 'Interest Rate',
      accessor: 'interestRate',
      render: (row) => <span className="font-bold text-slate-800">{formatPercent(row.interestRate)} / month</span>
    },
    {
      header: 'Max Tenure',
      accessor: 'maxTenureMonths',
      render: (row) => <span className="text-slate-700">{row.maxTenureMonths} Months</span>
    },
    {
      header: 'Maximum Cap',
      accessor: 'maxAmount',
      render: (row) => <span className="font-mono font-bold text-slate-900">{formatCurrency(row.maxAmount)}</span>
    },
    {
      header: 'Status',
      accessor: 'isActive',
      render: (row) => <Badge status={row.isActive ? 'ACTIVE' : 'DRAFT'} size="sm" />
    }
  ];

  const contractColumns = [
    {
      header: 'Contract ID',
      accessor: 'id',
      render: (row) => (
        <div>
          <span className="font-mono font-bold text-slate-900 block">{row.id}</span>
          <span className="text-[11px] text-slate-400">{row.staffName}</span>
        </div>
      )
    },
    {
      header: 'Principal Disbursed',
      accessor: 'principal',
      render: (row) => <span className="font-mono">{formatCurrency(row.principal)}</span>
    },
    {
      header: 'Remaining Balance',
      accessor: 'remainingBalance',
      render: (row) => <span className="font-mono font-bold text-rose-600">{formatCurrency(row.remainingBalance)}</span>
    },
    {
      header: 'Monthly Deduction',
      accessor: 'monthlyInstallment',
      render: (row) => <span className="font-mono text-slate-800">{formatCurrency(row.monthlyInstallment)}/mo</span>
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
        title="Corporate Loan Plans &amp; Credit Oversight"
        subtitle="Corporate-backed employee credit with automated payroll deduction and mandatory 33% Debt-to-Income (DTI) compliance."
        actions={
          <button
            onClick={() => setShowCreatePlanModal(true)}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
          >
            <Plus className="w-3.5 h-3.5" />
            Create Loan Plan
          </button>
        }
      />

      <Tabs
        tabs={[
          { id: 'applications', label: 'Applications Queue', count: applications.length, icon: BadgePercent },
          { id: 'plans', label: 'Loan Plans Configuration', count: plans.length, icon: Sliders },
          { id: 'contracts', label: 'Active Contracts & Repayments', count: contracts.length, icon: FileText }
        ]}
        activeTab={activeTab}
        onChange={setActiveTab}
        className="mb-6"
      />

      {activeTab === 'applications' && <DataTable columns={appColumns} data={applications} />}
      {activeTab === 'plans' && <DataTable columns={planColumns} data={plans} />}
      {activeTab === 'contracts' && <DataTable columns={contractColumns} data={contracts} />}

      {/* Create Plan Modal */}
      <Modal
        isOpen={showCreatePlanModal}
        onClose={() => setShowCreatePlanModal(false)}
        title="Create Corporate Loan Plan"
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowCreatePlanModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
            <button onClick={handleCreatePlan} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Create Plan</button>
          </div>
        }
      >
        <form onSubmit={handleCreatePlan} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Plan Name</label>
            <input type="text" required value={planName} onChange={(e) => setPlanName(e.target.value)} placeholder="e.g. Housing & Relocation Loan" className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Monthly Interest Rate (e.g. 0.035 = 3.5%)</label>
              <input type="number" step="0.001" required value={interestRate} onChange={(e) => setInterestRate(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono" />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Max Tenure (Months)</label>
              <input type="number" required value={maxTenureMonths} onChange={(e) => setMaxTenureMonths(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono" />
            </div>
          </div>
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Maximum Borrowing Cap (₦)</label>
            <input type="number" required value={maxAmount} onChange={(e) => setMaxAmount(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold" />
          </div>
        </form>
      </Modal>

      {/* Decline Modal */}
      {selectedApplication && (
        <Modal
          isOpen={showDeclineModal}
          onClose={() => setShowDeclineModal(false)}
          title={`Decline Loan Application: ${selectedApplication.staffName}`}
          footer={
            <div className="flex items-center justify-end gap-3 w-full">
              <button onClick={() => setShowDeclineModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
              <button onClick={handleDecline} className="px-5 py-2 text-xs font-bold text-white bg-rose-600 rounded-xl">Decline Application</button>
            </div>
          }
        >
          <div className="space-y-4 text-xs text-left">
            <div>
              <label className="block font-semibold text-slate-700 mb-1.5">Reason for Declining</label>
              <textarea rows={3} required value={declineReason} onChange={(e) => setDeclineReason(e.target.value)} placeholder="Provide explanation for rejection..." className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl" />
            </div>
          </div>
        </Modal>
      )}

      {/* PIN Modal for Loan Disbursement */}
      <PinModal
        isOpen={showPinModal}
        onClose={() => setShowPinModal(false)}
        onConfirm={handlePinConfirm}
        title="Authorize Loan Disbursement"
        description="Enter your 4-digit transaction PIN to debit corporate loan reserves and credit the employee's personal wallet."
        amount={selectedApplication ? formatCurrency(selectedApplication.requestedAmount) : '0.00'}
        recipient={selectedApplication ? `${selectedApplication.staffName} (${selectedApplication.email})` : ''}
      />
    </div>
  );
}
