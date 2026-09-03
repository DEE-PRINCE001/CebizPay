import React, { useState } from 'react';
import Modal from '../common/Modal';
import Input from '../forms/Input';
import PinInput from '../forms/PinInput';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import SuccessModal from '../feedback/SuccessModal';
import Card from '../common/Card';
import { Receipt, Calendar, Users, ArrowRight, ArrowLeft, CheckCircle2, ShieldCheck, Zap } from 'lucide-react';
import apiClient from '../../services/api/client';
import { parseProblemDetails } from '../../utils/problemDetails';
import { useToast } from '../../hooks/useToast';

/**
 * Multi-step payroll execution and dry-run calculation wizard.
 */
export default function RunPayrollWizardModal({
  isOpen,
  onClose,
  onSuccess
}) {
  const { showSuccess } = useToast();

  const [step, setStep] = useState(1); // 1: Period selection, 2: Preview calculation, 3: PIN authorization
  const [periodStart, setPeriodStart] = useState(() => {
    const d = new Date();
    return new Date(d.getFullYear(), d.getMonth(), 1).toISOString().slice(0, 10);
  });
  const [periodEnd, setPeriodEnd] = useState(() => {
    const d = new Date();
    return new Date(d.getFullYear(), d.getMonth() + 1, 0).toISOString().slice(0, 10);
  });
  const [currency, setCurrency] = useState('NGN');
  const [pin, setPin] = useState('');

  const [calculating, setCalculating] = useState(false);
  const [executing, setExecuting] = useState(false);
  const [calculationResult, setCalculationResult] = useState(null);
  const [error, setError] = useState(null);
  const [successData, setSuccessData] = useState(null);

  const resetWizard = () => {
    setStep(1);
    setPin('');
    setCalculationResult(null);
    setError(null);
  };

  const handleClose = () => {
    resetWizard();
    onClose();
  };

  // Step 1 -> Step 2: Calculate Payroll Dry Run
  const handleCalculate = async (e) => {
    if (e) e.preventDefault();
    setCalculating(true);
    setError(null);

    try {
      const response = await apiClient.post('/org/payroll/calculate', {
        currency,
        criteria: {}
      });

      setCalculationResult(response);
      setStep(2);
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Unable to calculate payroll dry-run.');
    } finally {
      setCalculating(false);
    }
  };

  // Step 3: Execute Payroll Batch
  const handleExecute = async (e) => {
    if (e) e.preventDefault();

    if (pin.length < 4) {
      setError('Please enter your 4-digit transaction PIN.');
      return;
    }

    setExecuting(true);
    setError(null);

    try {
      const response = await apiClient.postFinancial('/org/payroll/execute', {
        currency,
        periodStart: new Date(periodStart).toISOString(),
        periodEnd: new Date(periodEnd).toISOString(),
        criteria: {}
      });

      const batchId = response?.batchId || response?.id || 'Processing';
      setSuccessData({
        batchId,
        totalStaff: calculationResult?.staffCount || calculationResult?.totalEmployees || 0,
        netPayout: calculationResult?.totalNetPay || calculationResult?.totalAmount || 0
      });

      showSuccess('Corporate payroll run enqueued for disbursement.');
      resetWizard();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Payroll execution failed. Please verify wallet balance and authorization PIN.');
    } finally {
      setExecuting(false);
    }
  };

  const formatCurrency = (amt) => {
    return new Intl.NumberFormat('en-NG', {
      style: 'currency',
      currency: 'NGN',
      minimumFractionDigits: 2
    }).format(amt || 0);
  };

  return (
    <>
      <Modal
        isOpen={isOpen && !successData}
        onClose={handleClose}
        title="Run Corporate Payroll"
        subtitle={`Step ${step} of 3 — ${
          step === 1
            ? 'Select Pay Period'
            : step === 2
            ? 'Review Calculation & Roster'
            : 'Authorize Disbursal'
        }`}
        maxWidth="max-w-lg"
      >
        <div className="space-y-4 pt-1">
          {error && (
            <Alert variant="danger" onClose={() => setError(null)}>
              {error}
            </Alert>
          )}

          {/* STEP 1: Select Pay Period */}
          {step === 1 && (
            <form onSubmit={handleCalculate} className="space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <Input
                  label="Period Start Date"
                  type="date"
                  value={periodStart}
                  onChange={(e) => setPeriodStart(e.target.value)}
                  icon={Calendar}
                  required
                />
                <Input
                  label="Period End Date"
                  type="date"
                  value={periodEnd}
                  onChange={(e) => setPeriodEnd(e.target.value)}
                  icon={Calendar}
                  required
                />
              </div>

              <div className="p-4 bg-slate-50 border border-slate-200/80 rounded-2xl text-xs text-slate-600 space-y-2">
                <div className="font-semibold text-slate-900 flex items-center gap-1.5">
                  <Receipt size={15} className="text-brand-600" />
                  <span>Automated Payroll Execution</span>
                </div>
                <p className="text-[11px] text-slate-500 leading-relaxed">
                  The system will compute net salary, deductions, and workforce salary levels for all active staff members in the selected cycle.
                </p>
              </div>

              <div className="flex items-center gap-3 pt-2">
                <Button
                  variant="outline"
                  size="md"
                  onClick={handleClose}
                  className="flex-1"
                >
                  Cancel
                </Button>
                <Button
                  type="submit"
                  variant="primary"
                  size="md"
                  loading={calculating}
                  icon={ArrowRight}
                  iconPosition="right"
                  className="flex-1"
                >
                  Preview Calculation
                </Button>
              </div>
            </form>
          )}

          {/* STEP 2: Review Calculation */}
          {step === 2 && (
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <div className="p-4 bg-slate-50 border border-slate-100 rounded-2xl">
                  <span className="text-slate-500 text-[11px] block mb-1">Eligible Employees</span>
                  <span className="text-2xl font-extrabold text-slate-900 font-sans">
                    {calculationResult?.staffCount || calculationResult?.totalEmployees || 0}
                  </span>
                </div>
                <div className="p-4 bg-brand-50 border border-brand-100 rounded-2xl">
                  <span className="text-brand-600 text-[11px] block mb-1 font-semibold">Total Net Payout</span>
                  <span className="text-xl font-extrabold text-brand-700 font-sans">
                    {formatCurrency(calculationResult?.totalNetPay || calculationResult?.totalAmount || 0)}
                  </span>
                </div>
              </div>

              <div className="p-3 bg-white border border-slate-200/80 rounded-2xl space-y-2 text-xs">
                <div className="flex justify-between py-1 border-b border-slate-100">
                  <span className="text-slate-500">Gross Salary Total</span>
                  <span className="font-bold text-slate-900">
                    {formatCurrency(calculationResult?.totalGrossPay || calculationResult?.grossTotal || 0)}
                  </span>
                </div>
                <div className="flex justify-between py-1 border-b border-slate-100">
                  <span className="text-slate-500">Loan & Tax Deductions</span>
                  <span className="font-medium text-slate-700">
                    -{formatCurrency(calculationResult?.totalDeductions || 0)}
                  </span>
                </div>
                <div className="flex justify-between py-1 font-bold text-slate-900">
                  <span>Net Ledger Debit</span>
                  <span className="font-mono text-brand-600">
                    {formatCurrency(calculationResult?.totalNetPay || calculationResult?.totalAmount || 0)}
                  </span>
                </div>
              </div>

              <div className="flex items-center gap-3 pt-2">
                <Button
                  variant="outline"
                  size="md"
                  icon={ArrowLeft}
                  onClick={() => setStep(1)}
                  className="flex-1"
                >
                  Back
                </Button>
                <Button
                  variant="primary"
                  size="md"
                  onClick={() => setStep(3)}
                  icon={ArrowRight}
                  iconPosition="right"
                  className="flex-1"
                >
                  Proceed to PIN
                </Button>
              </div>
            </div>
          )}

          {/* STEP 3: PIN Authorization */}
          {step === 3 && (
            <form onSubmit={handleExecute} className="space-y-4">
              <div className="p-4 bg-brand-50 border border-brand-100 rounded-2xl text-center space-y-1">
                <span className="text-xs text-brand-600 font-semibold uppercase">Total Batch Disbursal</span>
                <div className="text-2xl font-extrabold text-brand-700 font-sans">
                  {formatCurrency(calculationResult?.totalNetPay || calculationResult?.totalAmount || 0)}
                </div>
                <p className="text-[11px] text-slate-500">
                  For {calculationResult?.staffCount || 0} employees ({periodStart} to {periodEnd})
                </p>
              </div>

              <div className="pt-2">
                <PinInput
                  label="Authorize Batch Disbursal with 4-Digit PIN"
                  value={pin}
                  onChange={(val) => {
                    setPin(val);
                    if (error) setError(null);
                  }}
                />
              </div>

              <div className="flex items-center gap-3 pt-3 border-t border-slate-100">
                <Button
                  variant="outline"
                  size="md"
                  icon={ArrowLeft}
                  onClick={() => setStep(2)}
                  disabled={executing}
                  className="flex-1"
                >
                  Back
                </Button>
                <Button
                  type="submit"
                  variant="primary"
                  size="md"
                  loading={executing}
                  icon={Zap}
                  className="flex-1"
                >
                  Disburse Payroll
                </Button>
              </div>
            </form>
          )}
        </div>
      </Modal>

      {/* Success Modal */}
      {successData && (
        <SuccessModal
          isOpen={true}
          onClose={() => {
            setSuccessData(null);
            handleClose();
          }}
          title="Payroll Batch Disbursed"
          message={`Successfully enqueued batch #${successData.batchId} for ${successData.totalStaff} staff members totaling ${formatCurrency(successData.netPayout)}.`}
          buttonText="Done"
        />
      )}
    </>
  );
}
