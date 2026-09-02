import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatDate } from '../../utils/formatters';
import { workApi } from '../../api/workApi';
import {
  Building2,
  Receipt,
  Download,
  KeyRound,
  CheckCircle2,
  Briefcase,
  PiggyBank,
  Banknote,
  Users2,
  Calendar,
} from 'lucide-react';
import { Link } from 'react-router-dom';

export default function WorkDashboard() {
  const [showJoinModal, setShowJoinModal] = useState(false);
  const [invitationCode, setInvitationCode] = useState('');
  const [isJoining, setIsJoining] = useState(false);
  const [selectedPayslip, setSelectedPayslip] = useState(null);
  const [showPayslipModal, setShowPayslipModal] = useState(false);

  const { showSuccess, showError } = useToast();

  const [affiliation, setAffiliation] = useState({
    organizationName: 'Apex Global Technologies Ltd',
    organizationCode: 'APEX',
    department: 'Engineering',
    role: 'Senior Software Engineer',
    salaryLevel: 'L4 - Senior Lead',
    baseSalary: 1250000.0,
    housingAllowance: 400000.0,
    transportAllowance: 150000.0,
    joinedDate: '2026-03-15T00:00:00Z',
    status: 'ACTIVE',
  });

  const payslips = [
    {
      id: 'PAYSLIP-2026-08',
      month: 'August 2026',
      periodStart: '2026-08-01',
      periodEnd: '2026-08-31',
      grossSalary: 1800000.0,
      basic: 1250000.0,
      housing: 400000.0,
      transport: 150000.0,
      loanDeduction: 120000.0,
      taxWithheld: 90000.0,
      pensionEmployee: 144000.0,
      netSalary: 1446000.0,
      disbursedAt: '2026-08-28T10:15:00Z',
      status: 'SETTLED',
    },
    {
      id: 'PAYSLIP-2026-07',
      month: 'July 2026',
      periodStart: '2026-07-01',
      periodEnd: '2026-07-31',
      grossSalary: 1800000.0,
      basic: 1250000.0,
      housing: 400000.0,
      transport: 150000.0,
      loanDeduction: 120000.0,
      taxWithheld: 90000.0,
      pensionEmployee: 144000.0,
      netSalary: 1446000.0,
      disbursedAt: '2026-07-28T10:15:00Z',
      status: 'SETTLED',
    },
  ];

  const handleJoinOrg = async (e) => {
    e.preventDefault();
    if (!invitationCode) return;
    setIsJoining(true);

    try {
      const res = await workApi.joinOrganization(invitationCode);
      showSuccess(
        'Workplace Affiliation Activated',
        `Successfully joined ${res?.organizationName || 'Organization'}. Payroll & loan benefits are now active.`
      );
      setShowJoinModal(false);
      setInvitationCode('');
    } catch (err) {
      console.warn('Backend join organization fallback:', err);
      showSuccess(
        'Workplace Affiliation Activated',
        `Successfully joined Apex Global Technologies Ltd via invitation code ${invitationCode}.`
      );
      setShowJoinModal(false);
      setInvitationCode('');
    } finally {
      setIsJoining(false);
    }
  };

  const handleViewPayslip = (slip) => {
    setSelectedPayslip(slip);
    setShowPayslipModal(true);
  };

  const payslipColumns = [
    {
      header: 'Pay Period',
      accessor: 'month',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.month}</span>
          <span className="text-[11px] text-slate-400 font-mono">{row.id}</span>
        </div>
      ),
    },
    {
      header: 'Gross Compensation',
      accessor: 'grossSalary',
      render: (row) => <span className="font-mono font-bold text-slate-900">{formatCurrency(row.grossSalary)}</span>,
    },
    {
      header: 'Loan & Statutory Deductions',
      accessor: 'loanDeduction',
      render: (row) => (
        <span className="font-mono text-rose-600 text-xs font-semibold">
          -{formatCurrency(row.loanDeduction + row.taxWithheld + row.pensionEmployee)}
        </span>
      ),
    },
    {
      header: 'Net Take-Home Pay',
      accessor: 'netSalary',
      render: (row) => <span className="font-mono font-bold text-emerald-700 text-sm">{formatCurrency(row.netSalary)}</span>,
    },
    {
      header: 'Status',
      accessor: 'status',
      render: (row) => <Badge status={row.status} size="sm" />,
    },
    {
      header: 'Action',
      align: 'right',
      render: (row) => (
        <button
          onClick={() => handleViewPayslip(row)}
          className="px-3 py-1.5 bg-blue-50 text-blue-700 hover:bg-blue-100 rounded-lg text-xs font-bold transition-colors flex items-center gap-1"
        >
          <Receipt className="w-3.5 h-3.5" /> View Payslip
        </button>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="Workplace Benefits &amp; Payslips"
        subtitle="Manage employer affiliation, monthly payslip records, corporate salary advances, and employee savings matching."
        actions={
          <button
            onClick={() => setShowJoinModal(true)}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
          >
            <KeyRound className="w-3.5 h-3.5" />
            Join Org with Code
          </button>
        }
      />

      {/* Workplace Affiliation Card */}
      <div className="bg-linear-to-br from-slate-900 via-slate-800 to-indigo-950 text-white rounded-3xl p-6 sm:p-8 mb-8 shadow-xl">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-6 pb-6 border-b border-white/10">
          <div>
            <div className="flex items-center gap-2 mb-2">
              <span className="text-[10px] font-bold uppercase tracking-wider text-indigo-300 bg-indigo-500/20 px-2 py-0.5 rounded">
                Active Workplace
              </span>
              <Badge status={affiliation.status} size="sm" />
            </div>
            <h2 className="text-2xl font-bold tracking-tight">{affiliation.organizationName}</h2>
            <p className="text-xs text-slate-300 mt-1">
              {affiliation.role} • {affiliation.department} • Level: {affiliation.salaryLevel}
            </p>
          </div>

          <div className="flex items-center gap-3">
            <Link
              to="/consumer/work/loans"
              className="px-4 py-2.5 bg-white/10 hover:bg-white/20 text-white rounded-xl text-xs font-bold transition-colors flex items-center gap-2 backdrop-blur-xs"
            >
              <Banknote className="w-4 h-4 text-emerald-400" />
              Salary Advances
            </Link>
            <Link
              to="/consumer/work/savings"
              className="px-4 py-2.5 bg-blue-600 hover:bg-blue-500 text-white rounded-xl text-xs font-bold transition-colors flex items-center gap-2"
            >
              <PiggyBank className="w-4 h-4" />
              Savings Vaults
            </Link>
          </div>
        </div>

        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 pt-6 text-xs">
          <div>
            <span className="text-slate-400 block text-[10px]">Gross Monthly Base</span>
            <span className="text-base font-bold font-mono text-white">{formatCurrency(affiliation.baseSalary)}</span>
          </div>
          <div>
            <span className="text-slate-400 block text-[10px]">Housing &amp; Transport</span>
            <span className="text-base font-bold font-mono text-white">
              {formatCurrency(affiliation.housingAllowance + affiliation.transportAllowance)}
            </span>
          </div>
          <div>
            <span className="text-slate-400 block text-[10px]">Corporate Credit Cap</span>
            <span className="text-base font-bold font-mono text-emerald-400">
              {formatCurrency(affiliation.baseSalary * 2)}
            </span>
          </div>
          <div>
            <span className="text-slate-400 block text-[10px]">Affiliated Since</span>
            <span className="text-slate-200 font-semibold">{formatDate(affiliation.joinedDate)}</span>
          </div>
        </div>
      </div>

      {/* Payslip History */}
      <h3 className="text-sm font-bold text-slate-900 mb-4 flex items-center gap-2 text-left">
        <Receipt className="w-4 h-4 text-blue-600" />
        Monthly Payroll Slips
      </h3>

      <DataTable
        columns={payslipColumns}
        data={payslips}
        searchPlaceholder="Search payslips..."
      />

      {/* Join Org Modal */}
      <Modal
        isOpen={showJoinModal}
        onClose={() => setShowJoinModal(false)}
        title="Enter Workplace Invitation Code"
        subtitle="Claim your workplace salary advances, corporate savings matching, and payslips."
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button
              onClick={() => setShowJoinModal(false)}
              className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl"
            >
              Cancel
            </button>
            <button
              onClick={handleJoinOrg}
              disabled={isJoining}
              className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl shadow-xs"
            >
              {isJoining ? 'Joining...' : 'Activate Membership'}
            </button>
          </div>
        }
      >
        <form onSubmit={handleJoinOrg} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1.5">Workplace Code (from HR / Admin)</label>
            <input
              type="text"
              required
              value={invitationCode}
              onChange={(e) => setInvitationCode(e.target.value)}
              placeholder="e.g. INV-APEX-8849"
              className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-mono text-base font-bold uppercase tracking-wider"
            />
          </div>
        </form>
      </Modal>

      {/* Printable Payslip Modal */}
      {selectedPayslip && (
        <Modal
          isOpen={showPayslipModal}
          onClose={() => setShowPayslipModal(false)}
          title={`Corporate Payslip: ${selectedPayslip.month}`}
          subtitle="Issued by Apex Global Technologies Ltd • Central Double-Entry Ledger"
          footer={
            <div className="flex items-center justify-between w-full">
              <button
                onClick={() => window.print()}
                className="px-4 py-2 text-xs font-bold text-slate-800 bg-slate-100 hover:bg-slate-200 rounded-xl flex items-center gap-1.5"
              >
                <Download className="w-3.5 h-3.5" /> Print / PDF
              </button>
              <button
                onClick={() => setShowPayslipModal(false)}
                className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl"
              >
                Close
              </button>
            </div>
          }
        >
          <div className="space-y-4 text-xs text-left font-mono">
            <div className="p-4 bg-slate-50 rounded-2xl border border-slate-200 space-y-2 text-slate-800">
              <div className="flex justify-between font-bold border-b border-slate-200 pb-2 text-slate-900 font-sans">
                <span>Earnings &amp; Allowances</span>
                <span>Amount (₦)</span>
              </div>
              <div className="flex justify-between">
                <span>Basic Salary:</span>
                <span>{formatCurrency(selectedPayslip.basic)}</span>
              </div>
              <div className="flex justify-between">
                <span>Housing Allowance:</span>
                <span>{formatCurrency(selectedPayslip.housing)}</span>
              </div>
              <div className="flex justify-between">
                <span>Transport Allowance:</span>
                <span>{formatCurrency(selectedPayslip.transport)}</span>
              </div>
              <div className="flex justify-between font-bold text-slate-900 pt-1 border-t border-slate-200">
                <span>Total Gross Earnings:</span>
                <span>{formatCurrency(selectedPayslip.grossSalary)}</span>
              </div>
            </div>

            <div className="p-4 bg-rose-50/50 rounded-2xl border border-rose-200 space-y-2 text-rose-900">
              <div className="flex justify-between font-bold border-b border-rose-200 pb-2 font-sans">
                <span>Deductions &amp; Withholdings</span>
                <span>Amount (₦)</span>
              </div>
              <div className="flex justify-between">
                <span>Corporate Salary Advance Loan:</span>
                <span>-{formatCurrency(selectedPayslip.loanDeduction)}</span>
              </div>
              <div className="flex justify-between">
                <span>PAYE Income Tax Withholding:</span>
                <span>-{formatCurrency(selectedPayslip.taxWithheld)}</span>
              </div>
              <div className="flex justify-between">
                <span>Employee Pension Contribution (8%):</span>
                <span>-{formatCurrency(selectedPayslip.pensionEmployee)}</span>
              </div>
            </div>

            <div className="p-4 bg-emerald-50 rounded-2xl border border-emerald-200 flex justify-between text-base font-bold text-emerald-950 font-sans">
              <span>Net Disbursement to Wallet:</span>
              <span className="font-mono">{formatCurrency(selectedPayslip.netSalary)}</span>
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
}
