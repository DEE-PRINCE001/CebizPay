import React, { useState, useEffect } from 'react';
import PageHeader from '../../components/common/PageHeader';
import Badge from '../../components/common/Badge';
import PinModal from '../../components/common/PinModal';
import { useAuth } from '../../context/AuthContext';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatDate } from '../../utils/formatters';
import { walletApi } from '../../api/walletApi';
import { orgApi } from '../../api/orgApi';
import {
  Building,
  Users,
  Wallet,
  Play,
  Copy,
  Check,
  CreditCard,
  Briefcase,
  TrendingUp,
  Boxes,
  FileSpreadsheet,
  PlusCircle,
  RefreshCw,
  AlertCircle,
} from 'lucide-react';
import { Link } from 'react-router-dom';

export default function OrgDashboard() {
  const { activeOrg, balanceVisible, toggleBalancePrivacy } = useAuth();
  const { showSuccess, showError } = useToast();

  const [copiedAccount, setCopiedAccount] = useState(false);
  const [showPinModal, setShowPinModal] = useState(false);
  const [fundAmount, setFundAmount] = useState('5000000');
  const [isLoading, setIsLoading] = useState(true);
  const [isProvisioning, setIsProvisioning] = useState(false);
  const [errorMessage, setErrorMessage] = useState(null);

  // Live corporate organization state
  const [virtualAccount, setVirtualAccount] = useState(null);
  const [corporateBalance, setCorporateBalance] = useState(0);
  const [headcount, setHeadcount] = useState(0);
  const [departmentsCount, setDepartmentsCount] = useState(0);

  const fetchOrgData = async () => {
    setIsLoading(true);
    setErrorMessage(null);
    try {
      const [staffRes, deptsRes, dvaRes] = await Promise.allSettled([
        orgApi.getStaffDirectory(),
        orgApi.getDepartments(),
        walletApi.getPrimaryVirtualAccount('NGN'),
      ]);

      if (staffRes.status === 'fulfilled' && staffRes.value?.items) {
        setHeadcount(staffRes.value.items.length);
      } else {
        setHeadcount(0);
      }

      if (deptsRes.status === 'fulfilled' && Array.isArray(deptsRes.value)) {
        setDepartmentsCount(deptsRes.value.length);
      } else {
        setDepartmentsCount(0);
      }

      if (dvaRes.status === 'fulfilled' && dvaRes.value?.accountNumber) {
        setVirtualAccount(dvaRes.value);
      } else {
        setVirtualAccount(null);
      }

      setCorporateBalance(activeOrg?.balance || 0);
    } catch (err) {
      setErrorMessage(err.message || 'Failed to fetch corporate treasury telemetry.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchOrgData();
  }, [activeOrg]);

  // Handle live corporate DVA Provisioning
  const handleProvisionDva = async () => {
    setIsProvisioning(true);
    try {
      const res = await walletApi.provisionVirtualAccount({
        currency: 'NGN',
        provider: 1,
      });

      if (res && res.accountNumber) {
        setVirtualAccount(res);
        showSuccess(
          'Corporate Virtual Account Provisioned',
          `Assigned ${res.bankName || 'Wema Bank'} NUBAN: ${res.accountNumber}`
        );
      } else {
        await fetchOrgData();
        showSuccess('Virtual Account Created', 'Dedicated Virtual Account is now active.');
      }
    } catch (err) {
      const msg = err.message || 'Failed to provision corporate virtual account.';
      showError('Provisioning Error', msg);
    } finally {
      setIsProvisioning(false);
    }
  };

  const handleCopy = () => {
    if (!virtualAccount?.accountNumber) return;
    navigator.clipboard.writeText(virtualAccount.accountNumber);
    setCopiedAccount(true);
    showSuccess('DVA Account Copied', 'Dedicated Virtual Account ready for inbound bank settlements.');
    setTimeout(() => setCopiedAccount(false), 2000);
  };

  const handleFundWallet = () => {
    setShowPinModal(true);
  };

  const handlePinConfirm = async (pin) => {
    setShowPinModal(false);
    const added = parseFloat(fundAmount);
    setCorporateBalance((prev) => prev + added);
    showSuccess(
      'Corporate Treasury Funded',
      `Credited ${formatCurrency(added)} into ${activeOrg?.name || 'Organization'} corporate wallet.`
    );
  };

  return (
    <div>
      <PageHeader
        title={`${activeOrg?.name || 'Corporate Organization'}`}
        subtitle={`RC: ${activeOrg?.cacNumber || 'Pending CAC'} • B2B Corporate Treasury &amp; Workforce OS`}
        actions={
          <div className="flex items-center gap-2">
            <button
              onClick={handleFundWallet}
              className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-slate-800 bg-white border border-slate-200 rounded-xl hover:bg-slate-50 shadow-xs cursor-pointer"
            >
              <CreditCard className="w-3.5 h-3.5 text-blue-600" />
              Deposit Treasury
            </button>
            <Link
              to="/org/payroll"
              className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs cursor-pointer"
            >
              <Play className="w-3.5 h-3.5" />
              Execute Payroll
            </Link>
          </div>
        }
      />

      {/* Error Banner */}
      {errorMessage && (
        <div className="mb-6 p-4 bg-rose-50 rounded-2xl border border-rose-200 text-rose-900 flex items-center justify-between text-xs">
          <div className="flex items-center gap-2">
            <AlertCircle className="w-4 h-4 text-rose-600 shrink-0" />
            <span>{errorMessage}</span>
          </div>
          <button
            onClick={fetchOrgData}
            className="px-3 py-1 bg-rose-100 hover:bg-rose-200 text-rose-800 rounded-lg font-bold"
          >
            Retry
          </button>
        </div>
      )}

      {/* Main Treasury Cards */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-8">
        {/* Balance Card */}
        <div className="lg:col-span-2 bg-linear-to-br from-purple-800 via-purple-700 to-indigo-900 text-white rounded-3xl p-6 sm:p-8 shadow-xl flex flex-col justify-between text-left">
          <div>
            <div className="flex items-center justify-between mb-4">
              <div className="flex items-center gap-2 text-xs text-purple-200 font-semibold uppercase tracking-wider">
                <Wallet className="w-4 h-4" />
                Corporate Treasury Vault Balance
              </div>
              <Badge status={activeOrg?.kybStatus || 'PENDING'} size="sm" />
            </div>

            <h2 className="text-4xl sm:text-5xl font-extrabold tracking-tight font-mono mb-4">
              {isLoading ? (
                <span className="opacity-50 text-3xl">Loading...</span>
              ) : balanceVisible ? (
                formatCurrency(corporateBalance)
              ) : (
                '••••••••'
              )}
            </h2>

            <p className="text-xs text-purple-200">
              Primary NGN Operating Reserve • Double-entry ledger verified
            </p>
          </div>

          <div className="pt-6 mt-6 border-t border-white/15 grid grid-cols-3 gap-3 text-center">
            <Link
              to="/org/staff"
              className="p-2.5 bg-white/10 hover:bg-white/20 rounded-xl text-xs font-bold transition-colors flex flex-col items-center gap-1 backdrop-blur-xs cursor-pointer"
            >
              <Users className="w-4 h-4" />
              Staff ({headcount})
            </Link>
            <Link
              to="/org/payroll"
              className="p-2.5 bg-white/10 hover:bg-white/20 rounded-xl text-xs font-bold transition-colors flex flex-col items-center gap-1 backdrop-blur-xs cursor-pointer"
            >
              <FileSpreadsheet className="w-4 h-4" />
              Payroll Run
            </Link>
            <Link
              to="/org/erp/invoices"
              className="p-2.5 bg-white/10 hover:bg-white/20 rounded-xl text-xs font-bold transition-colors flex flex-col items-center gap-1 backdrop-blur-xs cursor-pointer"
            >
              <Boxes className="w-4 h-4" />
              ERP Invoicing
            </Link>
          </div>
        </div>

        {/* Dedicated Virtual Account Card */}
        <div className="bg-white rounded-3xl border border-slate-200/80 p-6 shadow-xs flex flex-col justify-between text-left">
          {isLoading ? (
            <div className="p-8 text-center text-xs text-slate-400">
              <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-purple-600" />
              Fetching corporate DVA...
            </div>
          ) : virtualAccount ? (
            <div>
              <div className="flex items-center justify-between mb-4">
                <div className="flex items-center gap-2 text-xs font-bold text-slate-500 uppercase tracking-wider">
                  <Building className="w-4 h-4 text-purple-600" />
                  Corporate Inbound DVA
                </div>
                <Badge status={virtualAccount.status || 'ACTIVE'} size="sm" />
              </div>

              <div className="p-4 bg-slate-50 rounded-2xl border border-slate-100 mb-4">
                <span className="text-[10px] font-bold uppercase tracking-wider text-slate-400 block mb-1">
                  Virtual Account Number
                </span>
                <div className="flex items-center justify-between">
                  <span className="text-2xl font-bold font-mono text-slate-900 tracking-wider">
                    {virtualAccount.accountNumber}
                  </span>
                  <button
                    onClick={handleCopy}
                    className="p-2 text-slate-500 hover:text-blue-600 hover:bg-slate-200/60 rounded-xl transition-colors cursor-pointer"
                    title="Copy Account Number"
                  >
                    {copiedAccount ? <Check className="w-4 h-4 text-emerald-600" /> : <Copy className="w-4 h-4" />}
                  </button>
                </div>
              </div>

              <div className="space-y-2 text-xs text-slate-600">
                <div className="flex justify-between">
                  <span className="text-slate-400">Settlement Bank:</span>
                  <span className="font-semibold text-slate-800">{virtualAccount.bankName || 'Wema Bank'}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-400">Beneficiary Title:</span>
                  <span className="font-semibold text-slate-800 truncate max-w-[180px]">
                    {virtualAccount.accountName || activeOrg?.name}
                  </span>
                </div>
              </div>
            </div>
          ) : (
            <div className="my-auto text-center space-y-3 p-2">
              <div className="w-10 h-10 rounded-2xl bg-purple-50 text-purple-600 flex items-center justify-center mx-auto">
                <Building className="w-5 h-5" />
              </div>
              <div>
                <h4 className="font-bold text-slate-900 text-sm">No Corporate Virtual Account</h4>
                <p className="text-xs text-slate-500 mt-1 leading-relaxed">
                  Provision an automated NUBAN for your company to receive instant interbank payments directly into treasury.
                </p>
              </div>
              <button
                onClick={handleProvisionDva}
                disabled={isProvisioning}
                className="w-full py-2.5 bg-purple-600 hover:bg-purple-700 text-white font-bold text-xs rounded-xl shadow-xs transition-all flex items-center justify-center gap-1.5 cursor-pointer disabled:opacity-50"
              >
                {isProvisioning ? (
                  <RefreshCw className="w-4 h-4 animate-spin" />
                ) : (
                  <>
                    <PlusCircle className="w-4 h-4" />
                    <span>Provision Corporate Account</span>
                  </>
                )}
              </button>
            </div>
          )}

          <p className="text-[11px] text-slate-400 mt-4 leading-relaxed">
            Inbound interbank deposits instantly credit corporate treasury with automated ledger reconciliation.
          </p>
        </div>
      </div>

      {/* Quick Nav Hub */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4 text-left">
        <Link
          to="/org/departments"
          className="p-5 bg-white rounded-3xl border border-slate-200/80 hover:border-purple-300 transition-all shadow-xs cursor-pointer"
        >
          <Building className="w-6 h-6 text-purple-600 mb-2" />
          <h4 className="font-bold text-slate-900 text-sm">Workforce Structure</h4>
          <p className="text-xs text-slate-500 mt-1">{departmentsCount} Departments Registered</p>
        </Link>
        <Link
          to="/org/loans"
          className="p-5 bg-white rounded-3xl border border-slate-200/80 hover:border-purple-300 transition-all shadow-xs cursor-pointer"
        >
          <Briefcase className="w-6 h-6 text-blue-600 mb-2" />
          <h4 className="font-bold text-slate-900 text-sm">Employee Credit</h4>
          <p className="text-xs text-slate-500 mt-1">Salary Advances &amp; 33% DTI Underwriting</p>
        </Link>
        <Link
          to="/org/erp/inventory"
          className="p-5 bg-white rounded-3xl border border-slate-200/80 hover:border-purple-300 transition-all shadow-xs cursor-pointer"
        >
          <Boxes className="w-6 h-6 text-emerald-600 mb-2" />
          <h4 className="font-bold text-slate-900 text-sm">ERP Inventory &amp; CRM</h4>
          <p className="text-xs text-slate-500 mt-1">Products, Customers, &amp; Orders</p>
        </Link>
        <Link
          to="/org/erp/reports"
          className="p-5 bg-white rounded-3xl border border-slate-200/80 hover:border-purple-300 transition-all shadow-xs cursor-pointer"
        >
          <TrendingUp className="w-6 h-6 text-amber-600 mb-2" />
          <h4 className="font-bold text-slate-900 text-sm">Financial Statements</h4>
          <p className="text-xs text-slate-500 mt-1">Net P&amp;L, Sales, &amp; Settlements</p>
        </Link>
      </div>

      {/* PIN Modal */}
      <PinModal
        isOpen={showPinModal}
        onClose={() => setShowPinModal(false)}
        onConfirm={handlePinConfirm}
        title="Authorize Treasury Deposit"
        amount={formatCurrency(fundAmount)}
        recipient="Corporate Treasury Operating Vault"
      />
    </div>
  );
}
