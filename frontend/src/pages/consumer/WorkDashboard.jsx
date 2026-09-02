import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatDate } from '../../utils/formatters';
import {
  Briefcase,
  Building,
  KeyRound,
  FileText,
  Printer,
  CheckCircle2,
  Calendar,
  Layers
} from 'lucide-react';

export default function WorkDashboard() {
  const [showJoinModal, setShowJoinModal] = useState(false);
  const [inviteCode, setInviteCode] = useState('');
  const [selectedPayslip, setSelectedPayslip] = useState(null);
  const [showPayslipModal, setShowPayslipModal] = useState(false);
  const { showSuccess, showError } = useToast();

  // Affiliation details
  const [affiliation, setAffiliation] = useState({
    isAffiliated: true,
    companyName: 'Apex Global Technologies Ltd',
    cacNumber: 'RC-1849204',
    roleTitle: 'Senior Software Engineer',
    department: 'Engineering',
    salaryLevel: 'L4 - Senior Lead',
    baseSalary: 1250000.0,
    joinedAt: '2026-03-15T09:00:00Z'
  });

  // Payslips
  const [payslips] = useState([
    {
      id: 'PS-2026-08',
      period: 'August 2026',
      grossSalary: 1250000.0,
      loanDeduction: 120000.0,
      taxWithheld: 62500.0,
      netPay: 1067500.0,
      disbursedAt: '2026-08-28T10:15:00Z',
      status: 'SETTLED'
    },
    {
      id: 'PS-2026-07',
      period: 'July 2026',
      grossSalary: 1250000.0,
      loanDeduction: 120000.0,
      taxWithheld: 62500.0,
      netPay: 1067500.0,
      disbursedAt: '2026-07-28T11:00:00Z',
      status: 'SETTLED'
    },
    {
      id: 'PS-2026-06',
      period: 'June 2026',
      grossSalary: 1250000.0,
      loanDeduction: 0,
      taxWithheld: 62500.0,
      netPay: 1187500.0,
      disbursedAt: '2026-06-28T09:30:00Z',
      status: 'SETTLED'
    }
  ]);

  const handleJoin = (e) => {
    e.preventDefault();
    if (!inviteCode) return;
    setAffiliation({
      isAffiliated: true,
      companyName: 'Apex Global Technologies Ltd',
      cacNumber: 'RC-1849204',
      roleTitle: 'Senior Software Engineer',
      department: 'Engineering',
      salaryLevel: 'L4 - Senior Lead',
      baseSalary: 1250000.0,
      joinedAt: new Date().toISOString()
    });
    showSuccess('Organization Joined', 'Your profile is now bound to Apex Global Technologies.');
    setShowJoinModal(false);
    setInviteCode('');
  };

  const columns = [
    {
      header: 'Pay Period',
      accessor: 'period',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.period}</span>
          <span className="text-[11px] text-slate-400 font-mono">{row.id}</span>
        </div>
      )
    },
    {
      header: 'Gross Salary',
      accessor: 'grossSalary',
      render: (row) => <span className="font-mono text-slate-700">{formatCurrency(row.grossSalary)}</span>
    },
    {
      header: 'Loan Deductions',
      accessor: 'loanDeduction',
      render: (row) => (
        <span className="font-mono text-rose-600">
          {row.loanDeduction > 0 ? `-${formatCurrency(row.loanDeduction)}` : '—'}
        </span>
      )
    },
    {
      header: 'PAYE Tax',
      accessor: 'taxWithheld',
      render: (row) => <span className="font-mono text-slate-500">-{formatCurrency(row.taxWithheld)}</span>
    },
    {
      header: 'Net Take-Home Pay',
      accessor: 'netPay',
      render: (row) => <span className="font-mono font-bold text-emerald-700">{formatCurrency(row.netPay)}</span>
    },
    {
      header: 'Disbursement Date',
      accessor: 'disbursedAt',
      render: (row) => formatDate(row.disbursedAt, true)
    },
    {
      header: 'Payslip Document',
      align: 'right',
      render: (row) => (
        <button
          onClick={() => {
            setSelectedPayslip(row);
            setShowPayslipModal(true);
          }}
          className="px-3 py-1.5 bg-blue-50 text-blue-700 hover:bg-blue-100 rounded-lg text-xs font-bold transition-colors flex items-center gap-1.5"
        >
          <FileText className="w-3.5 h-3.5" />
          View Payslip
        </button>
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title="Work Domain &amp; Employee Benefits"
        subtitle="Manage your workplace affiliation, salary compensation history, and monthly employee payslips."
        actions={
          <button
            onClick={() => setShowJoinModal(true)}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-slate-800 bg-white border border-slate-200 rounded-xl hover:bg-slate-50 shadow-xs"
          >
            <KeyRound className="w-3.5 h-3.5 text-blue-600" />
            Join Org with Code
          </button>
        }
      />

      {/* Workplace Profile Card */}
      <div className="bg-white rounded-3xl border border-slate-200/80 p-6 sm:p-8 mb-8 shadow-xs">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-6 pb-6 border-b border-slate-100">
          <div className="flex items-center gap-4">
            <div className="w-14 h-14 rounded-2xl bg-purple-50 text-purple-700 flex items-center justify-center border border-purple-100 font-bold text-lg">
              <Building className="w-7 h-7" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h3 className="text-xl font-bold text-slate-900">{affiliation.companyName}</h3>
                <Badge status="ACTIVE" label="Active Staff" size="sm" />
              </div>
              <p className="text-xs text-slate-500 mt-1">
                Role: <strong className="text-slate-800">{affiliation.roleTitle}</strong> • Department: <strong className="text-slate-800">{affiliation.department}</strong>
              </p>
            </div>
          </div>

          <div className="flex items-center gap-4 text-xs font-mono">
            <div className="p-3 bg-slate-50 rounded-xl border border-slate-100 text-center">
              <span className="text-slate-400 block text-[10px] font-sans">Compensation Band</span>
              <span className="font-bold text-slate-900">{affiliation.salaryLevel}</span>
            </div>
            <div className="p-3 bg-slate-50 rounded-xl border border-slate-100 text-center">
              <span className="text-slate-400 block text-[10px] font-sans">Monthly Base Gross</span>
              <span className="font-bold text-slate-900 font-mono">{formatCurrency(affiliation.baseSalary)}</span>
            </div>
          </div>
        </div>

        <div className="pt-4 flex items-center justify-between text-xs text-slate-500">
          <span>Enrolled in corporate payroll &amp; automated salary advance scheme (33% DTI).</span>
          <span className="font-semibold text-slate-700">Joined: {formatDate(affiliation.joinedAt)}</span>
        </div>
      </div>

      {/* Monthly Payslips Table */}
      <div className="space-y-4">
        <h3 className="text-sm font-bold text-slate-900">Monthly Compensation &amp; Payslips</h3>
        <DataTable
          columns={columns}
          data={payslips}
          searchPlaceholder="Search payslips by month..."
        />
      </div>

      {/* Join Org Modal */}
      <Modal
        isOpen={showJoinModal}
        onClose={() => setShowJoinModal(false)}
        title="Join Organization"
        subtitle="Enter the unique workplace invitation code dispatched by your corporate HR administrator."
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowJoinModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
            <button onClick={handleJoin} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Verify &amp; Join</button>
          </div>
        }
      >
        <form onSubmit={handleJoin} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Workplace Invitation Code</label>
            <input
              type="text"
              required
              value={inviteCode}
              onChange={(e) => setInviteCode(e.target.value)}
              placeholder="e.g. INV-APEX-884920"
              className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-mono text-base font-bold tracking-wider uppercase focus:border-blue-600 focus:ring-2 focus:ring-blue-500/20 outline-hidden"
            />
          </div>
        </form>
      </Modal>

      {/* Printable Payslip Modal */}
      {selectedPayslip && (
        <Modal
          isOpen={showPayslipModal}
          onClose={() => setShowPayslipModal(false)}
          title={`Employee Payslip — ${selectedPayslip.period}`}
          subtitle={`${affiliation.companyName} • RC: ${affiliation.cacNumber}`}
          footer={
            <div className="flex items-center justify-between w-full">
              <button
                onClick={() => window.print()}
                className="px-4 py-2 text-xs font-bold text-slate-800 bg-slate-100 hover:bg-slate-200 rounded-xl flex items-center gap-1.5"
              >
                <Printer className="w-3.5 h-3.5" />
                Print Payslip
              </button>
              <button
                onClick={() => setShowPayslipModal(false)}
                className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl"
              >
                Close
              </button>
            </div>
          }
        >
          <div className="p-6 bg-slate-50 rounded-2xl border border-slate-200 space-y-4 text-xs text-left">
            <div className="flex justify-between items-start pb-3 border-b border-slate-200">
              <div>
                <h4 className="font-bold text-slate-900 text-sm">Amina Adeleke</h4>
                <p className="text-slate-500 text-[11px]">{affiliation.roleTitle} • {affiliation.department}</p>
              </div>
              <Badge status={selectedPayslip.status} />
            </div>

            <div className="space-y-2 py-2 border-b border-slate-200 font-mono">
              <div className="flex justify-between">
                <span className="text-slate-600 font-sans">Gross Base Salary:</span>
                <span className="font-bold text-slate-900">{formatCurrency(selectedPayslip.grossSalary)}</span>
              </div>
              <div className="flex justify-between text-rose-600">
                <span className="font-sans">Corporate Salary Advance Loan Principal Deduction:</span>
                <span>-{formatCurrency(selectedPayslip.loanDeduction)}</span>
              </div>
              <div className="flex justify-between text-rose-600">
                <span className="font-sans">State PAYE Income Tax Withholding:</span>
                <span>-{formatCurrency(selectedPayslip.taxWithheld)}</span>
              </div>
              <div className="flex justify-between text-base font-bold text-emerald-800 pt-2 border-t border-slate-200">
                <span className="font-sans">Net Payout Credited to CebizPay Wallet:</span>
                <span>{formatCurrency(selectedPayslip.netPay)}</span>
              </div>
            </div>

            <p className="text-[11px] text-slate-400">
              Settled via automated payroll direct deposit • Verified by CebizPay double-entry central ledger.
            </p>
          </div>
        </Modal>
      )}
    </div>
  );
}
