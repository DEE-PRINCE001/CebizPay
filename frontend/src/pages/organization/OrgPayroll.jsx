import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Tabs from '../../components/common/Tabs';
import Modal from '../../components/common/Modal';
import PinModal from '../../components/common/PinModal';
import { useAuth } from '../../context/AuthContext';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatDate } from '../../utils/formatters';
import {
  Banknote,
  Play,
  Calculator,
  RefreshCw,
  FileCheck,
  CheckCircle2,
  AlertCircle,
  Eye,
  Sliders,
  Printer,
  Edit,
  Building,
  Users,
  Layers,
  ArrowRight
} from 'lucide-react';

export default function OrgPayroll() {
  const { activeOrg, balanceVisible } = useAuth();
  const { showSuccess, showError } = useToast();

  const [activeTab, setActiveTab] = useState('engine'); // 'engine' | 'batches' | 'vouchers'
  
  // Payroll Execution Form state
  const [payrollMode, setPayrollMode] = useState('ALL'); // 'ALL' | 'DEPARTMENT' | 'ROLE' | 'LEVEL' | 'INDIVIDUAL'
  const [selectedDept, setSelectedDept] = useState('Engineering');
  const [periodStart, setPeriodStart] = useState('2026-09-01');
  const [periodEnd, setPeriodEnd] = useState('2026-09-30');
  const [currency, setCurrency] = useState('NGN');

  // Preview state
  const [previewResult, setPreviewResult] = useState(null);
  const [isCalculating, setIsCalculating] = useState(false);

  // Execution modal & PIN
  const [showPinModal, setShowPinModal] = useState(false);
  const [isExecuting, setIsExecuting] = useState(false);
  const [activeBatch, setActiveBatch] = useState(null);

  // Voucher Inspector state
  const [selectedVoucher, setSelectedVoucher] = useState(null);
  const [showVoucherModal, setShowVoucherModal] = useState(false);
  const [showEditVoucherModal, setShowEditVoucherModal] = useState(false);
  const [voucherRemarks, setVoucherRemarks] = useState('');
  const [voucherBank, setVoucherBank] = useState('');

  // Sample historical batches
  const [batches, setBatches] = useState([
    {
      id: 'batch-aug-2026',
      period: 'Aug 01, 2026 – Aug 31, 2026',
      mode: 'Pay All (28 Employees)',
      currency: 'NGN',
      totalEmployees: 28,
      totalGross: 12450000.0,
      totalDeductions: 610000.0,
      totalNet: 11840000.0,
      status: 'COMPLETED',
      completedCount: 28,
      failedCount: 0,
      executedAt: '2026-08-28T10:15:00Z'
    },
    {
      id: 'batch-jul-2026',
      period: 'Jul 01, 2026 – Jul 31, 2026',
      mode: 'Pay All (27 Employees)',
      currency: 'NGN',
      totalEmployees: 27,
      totalGross: 11980000.0,
      totalDeductions: 560000.0,
      totalNet: 11420000.0,
      status: 'COMPLETED',
      completedCount: 27,
      failedCount: 0,
      executedAt: '2026-07-28T11:00:00Z'
    }
  ]);

  // Sample Vouchers
  const [vouchers, setVouchers] = useState([
    {
      id: 'vouch-9921',
      voucherNumber: 'PV-2026-08-001',
      staffName: 'Amina Adeleke',
      department: 'Engineering',
      grossSalary: 1250000.0,
      loanDeduction: 120000.0,
      taxDeduction: 62500.0,
      netSalary: 1067500.0,
      currency: 'NGN',
      payingBank: 'Standard Chartered Corporate Direct Payout',
      remarks: 'August 2026 Base Salary + On-Call Allowance',
      status: 'SETTLED',
      paidAt: '2026-08-28T10:15:00Z'
    },
    {
      id: 'vouch-9922',
      voucherNumber: 'PV-2026-08-002',
      staffName: 'Babatunde Fashola',
      department: 'Product & Design',
      grossSalary: 1150000.0,
      loanDeduction: 0,
      taxDeduction: 57500.0,
      netSalary: 1092500.0,
      currency: 'NGN',
      payingBank: 'Standard Chartered Corporate Direct Payout',
      remarks: 'August 2026 Base Salary',
      status: 'SETTLED',
      paidAt: '2026-08-28T10:15:00Z'
    }
  ]);

  // Run dry-run calculation preview
  const handleCalculatePreview = (e) => {
    e.preventDefault();
    setIsCalculating(true);

    setTimeout(() => {
      setIsCalculating(false);
      const headcount = payrollMode === 'DEPARTMENT' ? 12 : 28;
      const gross = headcount === 12 ? 5800000.0 : 12850000.0;
      const loanDed = headcount === 12 ? 240000.0 : 640000.0;
      const taxDed = headcount === 12 ? 290000.0 : 642500.0;
      const net = gross - loanDed - taxDed;

      setPreviewResult({
        headcount,
        mode: payrollMode,
        target: payrollMode === 'DEPARTMENT' ? selectedDept : 'All Workforce',
        gross,
        loanDeductions: loanDed,
        taxDeductions: taxDed,
        netPayout: net,
        platformFees: headcount * 50.0,
        totalOrgCost: net + (headcount * 50.0),
        currency: 'NGN',
        lineItems: [
          { name: 'Amina Adeleke', dept: 'Engineering', gross: 1250000, loan: 120000, tax: 62500, net: 1067500 },
          { name: 'Babatunde Fashola', dept: 'Product', gross: 1150000, loan: 0, tax: 57500, net: 1092500 },
          { name: 'Chidinma Eze', dept: 'HR', gross: 550000, loan: 0, tax: 27500, net: 522500 },
          { name: 'Emeka Nwosu', dept: 'Engineering', gross: 850000, loan: 80000, tax: 42500, net: 727500 }
        ]
      });

      showSuccess('Preview Calculation Generated', 'Deterministic dry-run preview ready for audit and authorization.');
    }, 600);
  };

  const handleStartExecute = () => {
    if (!previewResult) {
      showError('Preview Required', 'Please compute dry-run calculation preview before execution.');
      return;
    }
    setShowPinModal(true);
  };

  const handleConfirmPin = (pin) => {
    setShowPinModal(false);
    setIsExecuting(true);

    const newBatch = {
      id: `batch-sep-2026-${Date.now()}`,
      period: `${formatDate(periodStart)} – ${formatDate(periodEnd)}`,
      mode: `Mode: ${previewResult.mode} (${previewResult.headcount} Staff)`,
      currency: 'NGN',
      totalEmployees: previewResult.headcount,
      totalGross: previewResult.gross,
      totalDeductions: previewResult.loanDeductions + previewResult.taxDeductions,
      totalNet: previewResult.netPayout,
      status: 'PROCESSING',
      completedCount: 0,
      failedCount: 0,
      executedAt: new Date().toISOString()
    };

    setActiveBatch(newBatch);

    // Simulate batch execution progression
    setTimeout(() => {
      newBatch.completedCount = previewResult.headcount;
      newBatch.status = 'COMPLETED';
      setActiveBatch({ ...newBatch });
      setBatches((prev) => [newBatch, ...prev]);
      setIsExecuting(false);
      showSuccess('Payroll Batch Completed', `Disbursed ${formatCurrency(newBatch.totalNet)} to ${newBatch.totalEmployees} employee wallets.`);
    }, 2200);
  };

  const handleUpdateVoucher = () => {
    if (!selectedVoucher) return;
    setVouchers((prev) =>
      prev.map((v) =>
        v.id === selectedVoucher.id
          ? { ...v, remarks: voucherRemarks, payingBank: voucherBank }
          : v
      )
    );
    showSuccess('Voucher Updated', `Safe metadata for ${selectedVoucher.voucherNumber} saved with audit log.`);
    setShowEditVoucherModal(false);
  };

  const batchColumns = [
    {
      header: 'Payroll Batch',
      accessor: 'period',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.period}</span>
          <span className="text-[11px] text-slate-400 font-mono">{row.id}</span>
        </div>
      )
    },
    {
      header: 'Scope',
      accessor: 'mode',
      render: (row) => <span className="font-semibold text-slate-700">{row.mode}</span>
    },
    {
      header: 'Gross Amount',
      accessor: 'totalGross',
      render: (row) => <span className="font-mono text-slate-600">{formatCurrency(row.totalGross)}</span>
    },
    {
      header: 'Deductions (Loans & Tax)',
      accessor: 'totalDeductions',
      render: (row) => <span className="font-mono text-rose-600">-{formatCurrency(row.totalDeductions)}</span>
    },
    {
      header: 'Net Disbursed',
      accessor: 'totalNet',
      render: (row) => <span className="font-mono font-bold text-emerald-700">{formatCurrency(row.totalNet)}</span>
    },
    {
      header: 'Execution Status',
      accessor: 'status',
      render: (row) => <Badge status={row.status} size="sm" />
    },
    {
      header: 'Timestamp',
      accessor: 'executedAt',
      render: (row) => formatDate(row.executedAt, true)
    }
  ];

  const voucherColumns = [
    {
      header: 'Voucher Number',
      accessor: 'voucherNumber',
      render: (row) => (
        <div>
          <span className="font-bold font-mono text-slate-900 block">{row.voucherNumber}</span>
          <span className="text-[11px] text-slate-400">{row.staffName}</span>
        </div>
      )
    },
    {
      header: 'Gross Salary',
      accessor: 'grossSalary',
      render: (row) => <span className="font-mono">{formatCurrency(row.grossSalary)}</span>
    },
    {
      header: 'Loan Deductions',
      accessor: 'loanDeduction',
      render: (row) => <span className="font-mono text-rose-600">-{formatCurrency(row.loanDeduction)}</span>
    },
    {
      header: 'Net Pay',
      accessor: 'netSalary',
      render: (row) => <span className="font-mono font-bold text-emerald-700">{formatCurrency(row.netSalary)}</span>
    },
    {
      header: 'Remarks',
      accessor: 'remarks',
      render: (row) => <span className="text-slate-500 text-xs truncate max-w-xs block">{row.remarks}</span>
    },
    {
      header: 'Actions',
      align: 'right',
      render: (row) => (
        <div className="flex items-center justify-end gap-1.5">
          <button
            onClick={() => {
              setSelectedVoucher(row);
              setShowVoucherModal(true);
            }}
            className="p-1.5 text-slate-500 hover:text-blue-600 hover:bg-slate-100 rounded-lg"
            title="View Voucher"
          >
            <Eye className="w-4 h-4" />
          </button>
          <button
            onClick={() => {
              setSelectedVoucher(row);
              setVoucherRemarks(row.remarks);
              setVoucherBank(row.payingBank);
              setShowEditVoucherModal(true);
            }}
            className="p-1.5 text-slate-500 hover:text-slate-800 hover:bg-slate-100 rounded-lg"
            title="Edit Remarks"
          >
            <Edit className="w-4 h-4" />
          </button>
        </div>
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title="Corporate Payroll Engine &amp; Vouchers"
        subtitle="Multi-mode deterministic payroll computation, loan deduction enforcement (33% DTI), and double-entry salary settlement."
      />

      <Tabs
        tabs={[
          { id: 'engine', label: 'Payroll Engine & Execute', icon: Banknote },
          { id: 'batches', label: 'Batch History & Progress', count: batches.length, icon: FileCheck },
          { id: 'vouchers', label: 'Payment Vouchers (Payslips)', count: vouchers.length, icon: Printer }
        ]}
        activeTab={activeTab}
        onChange={setActiveTab}
        className="mb-6"
      />

      {activeTab === 'engine' && (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Configuration Form */}
          <div className="bg-white p-6 rounded-3xl border border-slate-200/80 shadow-xs text-xs text-left">
            <h3 className="text-sm font-bold text-slate-900 mb-4 flex items-center gap-2">
              <Sliders className="w-4 h-4 text-blue-600" />
              Payroll Configuration
            </h3>

            <form onSubmit={handleCalculatePreview} className="space-y-4">
              <div>
                <label className="block font-semibold text-slate-700 mb-1.5">Execution Mode</label>
                <select
                  value={payrollMode}
                  onChange={(e) => setPayrollMode(e.target.value)}
                  className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-bold text-slate-800"
                >
                  <option value="ALL">Pay All (Entire Active Workforce)</option>
                  <option value="DEPARTMENT">By Department</option>
                  <option value="ROLE">By Workforce Role</option>
                  <option value="LEVEL">By Salary Compensation Level</option>
                  <option value="INDIVIDUAL">By Individual Staff Member</option>
                </select>
              </div>

              {payrollMode === 'DEPARTMENT' && (
                <div>
                  <label className="block font-semibold text-slate-700 mb-1.5">Target Department</label>
                  <select
                    value={selectedDept}
                    onChange={(e) => setSelectedDept(e.target.value)}
                    className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-bold"
                  >
                    <option value="Engineering">Engineering (12 Staff)</option>
                    <option value="Product & Design">Product &amp; Design (6 Staff)</option>
                    <option value="Finance & Accounting">Finance &amp; Accounting (4 Staff)</option>
                    <option value="Human Resources">Human Resources (3 Staff)</option>
                    <option value="Sales & Growth">Sales &amp; Growth (3 Staff)</option>
                  </select>
                </div>
              )}

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-semibold text-slate-700 mb-1.5">Period Start</label>
                  <input
                    type="date"
                    required
                    value={periodStart}
                    onChange={(e) => setPeriodStart(e.target.value)}
                    className="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl"
                  />
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1.5">Period End</label>
                  <input
                    type="date"
                    required
                    value={periodEnd}
                    onChange={(e) => setPeriodEnd(e.target.value)}
                    className="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl"
                  />
                </div>
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1.5">Disbursement Currency</label>
                <select
                  value={currency}
                  onChange={(e) => setCurrency(e.target.value)}
                  className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-bold"
                >
                  <option value="NGN">NGN — Nigerian Naira (Domestic Standard)</option>
                  <option value="INTERNATIONAL_NGN">INTERNATIONAL_NGN — Offshore Contractors</option>
                  <option value="USDT">USDT — Stablecoin Treasury Payout</option>
                </select>
              </div>

              <button
                type="submit"
                disabled={isCalculating}
                className="w-full py-3 bg-slate-900 hover:bg-black text-white font-bold rounded-xl shadow-xs flex items-center justify-center gap-2 transition-all"
              >
                {isCalculating ? (
                  <span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                ) : (
                  <>
                    <Calculator className="w-4 h-4 text-blue-400" />
                    <span>Generate Dry-Run Preview</span>
                  </>
                )}
              </button>
            </form>
          </div>

          {/* Dry-Run Calculation Preview Panel */}
          <div className="lg:col-span-2 bg-white p-6 rounded-3xl border border-slate-200/80 shadow-xs flex flex-col justify-between text-left">
            <div>
              <div className="flex items-center justify-between pb-4 border-b border-slate-100 mb-4">
                <div>
                  <h3 className="text-sm font-bold text-slate-900">Deterministic Calculation Preview</h3>
                  <p className="text-xs text-slate-500 mt-0.5">Audited breakdown of gross, statutory deductions, and net payouts</p>
                </div>
                {previewResult && <Badge status="VERIFIED" label="Preview Ready" size="sm" />}
              </div>

              {previewResult ? (
                <div className="space-y-4">
                  {/* Summary Metric Strip */}
                  <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                    <div className="p-3 bg-slate-50 rounded-xl border border-slate-100">
                      <span className="text-[10px] uppercase font-bold text-slate-400 block">Total Staff</span>
                      <span className="text-base font-bold text-slate-900">{previewResult.headcount} Staff</span>
                    </div>
                    <div className="p-3 bg-slate-50 rounded-xl border border-slate-100">
                      <span className="text-[10px] uppercase font-bold text-slate-400 block">Gross Payroll</span>
                      <span className="text-base font-bold text-slate-900 font-mono">{formatCurrency(previewResult.gross)}</span>
                    </div>
                    <div className="p-3 bg-rose-50/50 rounded-xl border border-rose-100">
                      <span className="text-[10px] uppercase font-bold text-rose-500 block">Loan &amp; Tax Deductions</span>
                      <span className="text-base font-bold text-rose-700 font-mono">
                        -{formatCurrency(previewResult.loanDeductions + previewResult.taxDeductions)}
                      </span>
                    </div>
                    <div className="p-3 bg-emerald-50 rounded-xl border border-emerald-100">
                      <span className="text-[10px] uppercase font-bold text-emerald-600 block">Net Payout</span>
                      <span className="text-base font-bold text-emerald-800 font-mono">{formatCurrency(previewResult.netPayout)}</span>
                    </div>
                  </div>

                  {/* Sample Employee Line Items Table */}
                  <div className="border border-slate-200 rounded-xl overflow-hidden text-xs">
                    <table className="w-full text-left">
                      <thead className="bg-slate-50 text-slate-500 font-semibold border-b border-slate-200">
                        <tr>
                          <th className="p-2.5">Employee</th>
                          <th className="p-2.5">Department</th>
                          <th className="p-2.5">Gross</th>
                          <th className="p-2.5">Loan Ded.</th>
                          <th className="p-2.5">Net Payout</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-100">
                        {previewResult.lineItems.map((item, idx) => (
                          <tr key={idx}>
                            <td className="p-2.5 font-bold text-slate-900">{item.name}</td>
                            <td className="p-2.5 text-slate-600">{item.dept}</td>
                            <td className="p-2.5 font-mono">{formatCurrency(item.gross)}</td>
                            <td className="p-2.5 font-mono text-rose-600">
                              {item.loan > 0 ? `-${formatCurrency(item.loan)}` : '—'}
                            </td>
                            <td className="p-2.5 font-mono font-bold text-emerald-700">{formatCurrency(item.net)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              ) : (
                <div className="py-16 text-center text-slate-400">
                  <Calculator className="w-8 h-8 mx-auto mb-2 opacity-50" />
                  <p className="text-xs">Configure payroll parameters on the left and generate a preview.</p>
                </div>
              )}
            </div>

            {/* Execute Button */}
            {previewResult && (
              <div className="pt-6 border-t border-slate-100 flex items-center justify-between mt-6">
                <div>
                  <span className="text-xs text-slate-500 block">Wallet Pre-Validation:</span>
                  <span className="text-xs font-bold text-emerald-700 flex items-center gap-1">
                    <CheckCircle2 className="w-3.5 h-3.5" /> Corporate balance sufficient ({formatCurrency(activeOrg?.balance || 14250000)})
                  </span>
                </div>

                <button
                  onClick={handleStartExecute}
                  disabled={isExecuting}
                  className="px-6 py-3 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-xl shadow-md flex items-center gap-2 transition-all disabled:opacity-50"
                >
                  <Play className="w-4 h-4 fill-current" />
                  <span>Authorize &amp; Execute Payroll</span>
                </button>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Batch Monitor Tab */}
      {activeTab === 'batches' && (
        <div className="space-y-6">
          {activeBatch && (
            <div className="p-5 bg-white rounded-2xl border-2 border-blue-500 shadow-md animate-in fade-in">
              <div className="flex items-center justify-between mb-3">
                <div className="flex items-center gap-2">
                  <span className="w-3 h-3 rounded-full bg-blue-600 animate-ping" />
                  <h4 className="font-bold text-slate-900 text-sm">Active Batch In Progress: {activeBatch.id}</h4>
                </div>
                <Badge status={activeBatch.status} />
              </div>
              <p className="text-xs text-slate-600 mb-3">{activeBatch.mode} • Net: {formatCurrency(activeBatch.totalNet)}</p>
              <div className="w-full bg-slate-100 rounded-full h-3 overflow-hidden">
                <div
                  style={{ width: `${activeBatch.status === 'COMPLETED' ? 100 : 65}%` }}
                  className="bg-blue-600 h-full rounded-full transition-all duration-500"
                />
              </div>
            </div>
          )}

          <DataTable
            columns={batchColumns}
            data={batches}
            searchPlaceholder="Search past payroll runs..."
          />
        </div>
      )}

      {/* Vouchers Tab */}
      {activeTab === 'vouchers' && (
        <DataTable
          columns={voucherColumns}
          data={vouchers}
          searchPlaceholder="Search salary vouchers by staff name or voucher number..."
        />
      )}

      {/* PIN Authorization Modal */}
      <PinModal
        isOpen={showPinModal}
        onClose={() => setShowPinModal(false)}
        onConfirm={handleConfirmPin}
        title="Authorize Live Payroll Execution"
        description="Enter your 4-digit transaction PIN to debit corporate funds and disburse salary to employee wallets."
        amount={previewResult ? formatCurrency(previewResult.netPayout) : '0.00'}
        recipient={`${previewResult?.headcount || 0} Staff Employees (${activeOrg?.name})`}
        isLoading={isExecuting}
      />

      {/* View Voucher Modal (Printable) */}
      {selectedVoucher && (
        <Modal
          isOpen={showVoucherModal}
          onClose={() => setShowVoucherModal(false)}
          title={`Payment Voucher: ${selectedVoucher.voucherNumber}`}
          subtitle="Official Corporate Salary Disbursement Voucher"
          footer={
            <div className="flex items-center justify-between w-full">
              <button
                onClick={() => window.print()}
                className="px-4 py-2 text-xs font-bold text-slate-800 bg-slate-100 hover:bg-slate-200 rounded-xl flex items-center gap-1.5"
              >
                <Printer className="w-3.5 h-3.5" />
                Print Voucher
              </button>
              <button
                onClick={() => setShowVoucherModal(false)}
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
                <h4 className="font-bold text-slate-900 text-sm">{activeOrg?.name}</h4>
                <p className="text-slate-500 text-[11px]">RC: {activeOrg?.cacNumber} • Lagos, Nigeria</p>
              </div>
              <Badge status={selectedVoucher.status} />
            </div>

            <div className="grid grid-cols-2 gap-3 py-2 border-b border-slate-200">
              <div>
                <span className="text-slate-400 block text-[10px] uppercase font-bold">Beneficiary Employee</span>
                <span className="font-bold text-slate-900 text-sm">{selectedVoucher.staffName}</span>
                <span className="text-slate-500 block text-[11px]">{selectedVoucher.department}</span>
              </div>
              <div className="text-right">
                <span className="text-slate-400 block text-[10px] uppercase font-bold">Disbursement Date</span>
                <span className="font-mono text-slate-700">{formatDate(selectedVoucher.paidAt, true)}</span>
              </div>
            </div>

            <div className="space-y-2 py-2 border-b border-slate-200 font-mono">
              <div className="flex justify-between">
                <span className="text-slate-600 font-sans">Gross Base Compensation:</span>
                <span className="font-bold text-slate-900">{formatCurrency(selectedVoucher.grossSalary)}</span>
              </div>
              <div className="flex justify-between text-rose-600">
                <span className="font-sans">Loan Principal Deduction:</span>
                <span>-{formatCurrency(selectedVoucher.loanDeduction)}</span>
              </div>
              <div className="flex justify-between text-rose-600">
                <span className="font-sans">Statutory Tax Withholding:</span>
                <span>-{formatCurrency(selectedVoucher.taxDeduction)}</span>
              </div>
              <div className="flex justify-between text-base font-bold text-emerald-800 pt-2 border-t border-slate-200">
                <span className="font-sans">Net Payout Received:</span>
                <span>{formatCurrency(selectedVoucher.netSalary)}</span>
              </div>
            </div>

            <div className="text-[11px] text-slate-500">
              <span className="font-semibold text-slate-700 block">Paying Rail / Bank:</span>
              <p>{selectedVoucher.payingBank}</p>
              <span className="font-semibold text-slate-700 block mt-2">Audit Remarks:</span>
              <p>{selectedVoucher.remarks}</p>
            </div>
          </div>
        </Modal>
      )}

      {/* Edit Voucher Metadata Modal */}
      {selectedVoucher && (
        <Modal
          isOpen={showEditVoucherModal}
          onClose={() => setShowEditVoucherModal(false)}
          title={`Edit Safe Metadata: ${selectedVoucher.voucherNumber}`}
          subtitle="Updates payment remarks and bank references without altering immutable monetary amounts."
          footer={
            <div className="flex items-center justify-end gap-3 w-full">
              <button
                onClick={() => setShowEditVoucherModal(false)}
                className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl"
              >
                Cancel
              </button>
              <button
                onClick={handleUpdateVoucher}
                className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl"
              >
                Save Metadata
              </button>
            </div>
          }
        >
          <div className="space-y-4 text-xs text-left">
            <div>
              <label className="block font-semibold text-slate-700 mb-1.5">Paying Bank Description</label>
              <input
                type="text"
                value={voucherBank}
                onChange={(e) => setVoucherBank(e.target.value)}
                className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl"
              />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1.5">Audit Remarks &amp; Notes</label>
              <textarea
                rows={3}
                value={voucherRemarks}
                onChange={(e) => setVoucherRemarks(e.target.value)}
                className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl"
              />
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
}
