import React, { useState, useEffect } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Tabs from '../../components/common/Tabs';
import Modal from '../../components/common/Modal';
import { useAuth } from '../../context/AuthContext';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatPercent, formatDate } from '../../utils/formatters';
import { loansApi } from '../../api/loansApi';
import { Banknote, Calculator, Plus, AlertCircle, CheckCircle2, FileText, ArrowRight, RefreshCw } from 'lucide-react';

export default function WorkLoansPage() {
  const [activeTab, setActiveTab] = useState('apply'); // 'apply' | 'contracts'
  const { user } = useAuth();
  const { showSuccess, showError } = useToast();

  const baseSalary = user?.baseSalary || 1250000.0;
  const maxDtiCap = baseSalary * 0.33;

  // Form state
  const [requestedAmount, setRequestedAmount] = useState('600000');
  const [tenureMonths, setTenureMonths] = useState(6);
  const [interestRateMonthly] = useState(0.035); // 3.5%
  const [purpose, setPurpose] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isLoadingContracts, setIsLoadingContracts] = useState(true);

  // Dynamic loan calculation
  const principal = parseFloat(requestedAmount || 0);
  const totalInterest = principal * interestRateMonthly * tenureMonths;
  const totalRepayable = principal + totalInterest;
  const monthlyInstallment = tenureMonths > 0 ? totalRepayable / tenureMonths : 0;
  const dtiRatio = baseSalary > 0 ? monthlyInstallment / baseSalary : 0;
  const isDtiCompliant = dtiRatio <= 0.33;

  // Live Contracts List
  const [contracts, setContracts] = useState([]);

  const fetchContracts = async () => {
    setIsLoadingContracts(true);
    try {
      const res = await loansApi.getMyStaffLoanContracts();
      if (Array.isArray(res)) {
        setContracts(res);
      } else {
        setContracts([]);
      }
    } catch (err) {
      setContracts([]);
      console.warn('Backend staff loan contracts fetch:', err);
    } finally {
      setIsLoadingContracts(false);
    }
  };

  useEffect(() => {
    fetchContracts();
  }, []);

  const handleApplyLoan = async (e) => {
    e.preventDefault();
    if (!isDtiCompliant) {
      showError('33% DTI Exceeded', 'Monthly loan installment cannot exceed 33% of your gross monthly base salary.');
      return;
    }

    setIsSubmitting(true);
    try {
      const payload = {
        amount: principal,
        tenureMonths,
        purpose,
      };
      await loansApi.submitStaffLoanApplication(payload);
      showSuccess(
        'Loan Application Submitted',
        `Your request for ${formatCurrency(principal)} has been submitted for HR approval.`
      );
      setPurpose('');
      setActiveTab('contracts');
      await fetchContracts();
    } catch (err) {
      const msg = err.message || 'Failed to submit loan application.';
      showError('Application Error', msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  const columns = [
    {
      header: 'Loan ID',
      accessor: 'id',
      render: (row) => (
        <div>
          <span className="font-mono font-bold text-slate-900 block">{row.id}</span>
          <span className="text-[11px] text-slate-400">{row.purpose || 'Salary Advance'}</span>
        </div>
      ),
    },
    {
      header: 'Principal Disbursed',
      accessor: 'principal',
      render: (row) => <span className="font-mono font-bold text-slate-900">{formatCurrency(row.principal || row.amount)}</span>,
    },
    {
      header: 'Monthly Deduction',
      accessor: 'monthlyInstallment',
      render: (row) => (
        <div>
          <span className="font-mono font-bold text-slate-800 text-xs block">{formatCurrency(row.monthlyInstallment)}/mo</span>
          <span className="text-[10px] text-emerald-600 font-bold">DTI: {formatPercent(row.dtiRatio || 0.1)} ✓</span>
        </div>
      ),
    },
    {
      header: 'Outstanding Balance',
      accessor: 'remainingBalance',
      render: (row) => <span className="font-mono font-bold text-rose-600">{formatCurrency(row.remainingBalance || row.totalRepayable || 0)}</span>,
    },
    {
      header: 'Remaining Tenure',
      accessor: 'remainingMonths',
      render: (row) => <span className="text-slate-700 text-xs">{row.remainingMonths || row.tenureMonths} of {row.tenureMonths} Months</span>,
    },
    {
      header: 'Status',
      accessor: 'status',
      render: (row) => <Badge status={row.status} size="sm" />,
    },
  ];

  return (
    <div>
      <PageHeader
        title="Salary Advance &amp; Employee Credit"
        subtitle="Low-interest corporate loans with automatic payroll deduction and statutory 33% Debt-to-Income (DTI) compliance."
      />

      <Tabs
        tabs={[
          { id: 'apply', label: 'Loan Calculator & Apply', icon: Calculator },
          { id: 'contracts', label: 'My Loan Contracts', count: contracts.length, icon: FileText },
        ]}
        activeTab={activeTab}
        onChange={setActiveTab}
        className="mb-6"
      />

      {activeTab === 'apply' && (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 text-xs text-left">
          {/* Loan Application Form */}
          <div className="lg:col-span-2 bg-white p-6 sm:p-8 rounded-3xl border border-slate-200/80 shadow-xs">
            <h3 className="text-sm font-bold text-slate-900 mb-6 flex items-center gap-2">
              <Banknote className="w-4 h-4 text-blue-600" />
              Configure Loan Request
            </h3>

            <form onSubmit={handleApplyLoan} className="space-y-6">
              <div>
                <label className="block font-semibold text-slate-700 mb-1.5">Requested Principal Amount (₦)</label>
                <input
                  type="number"
                  required
                  min={50000}
                  max={2500000}
                  value={requestedAmount}
                  onChange={(e) => setRequestedAmount(e.target.value)}
                  className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-mono text-xl font-bold focus:border-blue-600 focus:ring-2 focus:ring-blue-500/20 outline-hidden"
                />
                <span className="text-[11px] text-slate-400 mt-1 block">Maximum Corporate Limit: ₦2,500,000.00</span>
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-2">Repayment Tenure (Months)</label>
                <div className="grid grid-cols-4 gap-2.5">
                  {[3, 6, 9, 12].map((m) => (
                    <button
                      key={m}
                      type="button"
                      onClick={() => setTenureMonths(m)}
                      className={`py-3 rounded-2xl border font-bold transition-all text-center cursor-pointer ${
                        tenureMonths === m
                          ? 'bg-blue-600 text-white border-blue-600 shadow-xs'
                          : 'bg-slate-50 text-slate-700 border-slate-200 hover:bg-slate-100'
                      }`}
                    >
                      {m} Months
                    </button>
                  ))}
                </div>
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1.5">Purpose of Loan</label>
                <input
                  type="text"
                  required
                  value={purpose}
                  onChange={(e) => setPurpose(e.target.value)}
                  placeholder="e.g. Tuition fee, Medical, Home upgrade..."
                  className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl outline-hidden focus:border-blue-600 focus:ring-2 focus:ring-blue-500/20"
                />
              </div>

              <button
                type="submit"
                disabled={isSubmitting || !isDtiCompliant || !purpose}
                className="w-full py-3.5 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-xl shadow-xs flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed transition-all cursor-pointer"
              >
                {isSubmitting ? (
                  <span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                ) : (
                  <>
                    <span>Submit Application for HR Approval</span>
                    <ArrowRight className="w-4 h-4" />
                  </>
                )}
              </button>
            </form>
          </div>

          {/* Live Loan Calculation & 33% DTI Indicator */}
          <div className="bg-white p-6 rounded-3xl border border-slate-200/80 shadow-xs space-y-4">
            <h4 className="font-bold text-sm text-slate-900">Live Credit Assessment</h4>

            <div className="space-y-3 p-4 bg-slate-50 rounded-2xl border border-slate-200 font-mono">
              <div className="flex justify-between">
                <span className="text-slate-500 font-sans">Gross Base Salary:</span>
                <span className="font-bold text-slate-900">{formatCurrency(baseSalary)}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500 font-sans">Monthly Interest (3.5%):</span>
                <span className="text-slate-700">+{formatCurrency(totalInterest / tenureMonths)}/mo</span>
              </div>
              <div className="flex justify-between text-base font-bold text-blue-700 pt-2 border-t border-slate-200">
                <span className="font-sans">Monthly Installment:</span>
                <span>{formatCurrency(monthlyInstallment)}</span>
              </div>
            </div>

            {/* DTI Compliance Gauge */}
            <div className={`p-4 rounded-2xl border ${isDtiCompliant ? 'bg-emerald-50/50 border-emerald-200' : 'bg-rose-50/50 border-rose-200'}`}>
              <div className="flex items-center justify-between mb-1">
                <span className="font-bold text-xs text-slate-900">Debt-to-Income (DTI) Ratio</span>
                <span className={`font-mono font-bold text-xs ${isDtiCompliant ? 'text-emerald-700' : 'text-rose-700'}`}>
                  {formatPercent(dtiRatio)} (Max: 33%)
                </span>
              </div>
              <div className="w-full bg-slate-200 rounded-full h-2 overflow-hidden my-2">
                <div
                  style={{ width: `${Math.min(100, (dtiRatio / 0.33) * 100)}%` }}
                  className={`h-full rounded-full transition-all ${isDtiCompliant ? 'bg-emerald-500' : 'bg-rose-500'}`}
                />
              </div>
              <p className={`text-[11px] ${isDtiCompliant ? 'text-emerald-800' : 'text-rose-800'}`}>
                {isDtiCompliant
                  ? '✓ Eligible: Monthly deduction is within the statutory 33% maximum ceiling.'
                  : '⚠ Ineligible: Monthly installment exceeds statutory 33% of base salary.'}
              </p>
            </div>
          </div>
        </div>
      )}

      {activeTab === 'contracts' && (
        isLoadingContracts ? (
          <div className="p-12 text-center text-xs text-slate-400 bg-white rounded-3xl border border-slate-200">
            <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-blue-600" />
            Loading loan contracts from ledger...
          </div>
        ) : contracts.length === 0 ? (
          <div className="p-12 text-center text-xs text-slate-500 bg-white rounded-3xl border border-dashed border-slate-200">
            <Banknote className="w-10 h-10 mx-auto mb-3 text-slate-300" />
            <h4 className="font-bold text-slate-900 text-sm">No Active Loan Contracts</h4>
            <p className="mt-1 text-slate-400 max-w-sm mx-auto">
              You do not have any active or pending salary advance loans. Configure terms on the calculator tab to apply.
            </p>
          </div>
        ) : (
          <DataTable
            columns={columns}
            data={contracts}
            searchPlaceholder="Search active loan contracts..."
          />
        )
      )}
    </div>
  );
}
