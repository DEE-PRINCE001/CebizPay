import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Modal from '../../components/common/Modal';
import PinModal from '../../components/common/PinModal';
import { useAuth } from '../../context/AuthContext';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatDate } from '../../utils/formatters';
import { FileCheck, Plus, CheckCircle, Eye, Printer, ShieldCheck } from 'lucide-react';

export default function OrgERPVouchers() {
  const { activeOrg } = useAuth();
  const { showSuccess } = useToast();

  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showPinModal, setShowPinModal] = useState(false);
  const [selectedVoucher, setSelectedVoucher] = useState(null);

  // Form state
  const [payeeName, setPayeeName] = useState('');
  const [amount, setAmount] = useState('1850000');
  const [currency, setCurrency] = useState('NGN');
  const [purpose, setPurpose] = useState('');
  const [paymentMethod, setPaymentMethod] = useState('CORPORATE_BANK_TRANSFER');
  const [vouchers, setVouchers] = useState([]);

  const handleCreate = (e) => {
    e.preventDefault();
    const newV = {
      id: `CPV-2026-${Date.now().toString().slice(-3)}`,
      payeeName,
      purpose,
      amount: parseFloat(amount),
      currency,
      paymentMethod,
      status: 'DRAFT',
      preparedBy: 'Finance Ops',
      approvedBy: 'Pending',
      createdAt: new Date().toISOString()
    };
    setVouchers((prev) => [newV, ...prev]);
    showSuccess('Company Voucher Drafted', `${newV.id} prepared for approval.`);
    setShowCreateModal(false);
    setPayeeName('');
    setPurpose('');
  };

  const handleApprove = (v) => {
    setVouchers((prev) =>
      prev.map((item) =>
        item.id === v.id ? { ...item, status: 'APPROVED', approvedBy: 'CEO (Tunde Adeleke)' } : item
      )
    );
    showSuccess('Voucher Approved', `${v.id} approved for disbursement.`);
  };

  const handlePay = (v) => {
    setSelectedVoucher(v);
    setShowPinModal(true);
  };

  const handlePinConfirm = (pin) => {
    setShowPinModal(false);
    setVouchers((prev) =>
      prev.map((item) => (item.id === selectedVoucher.id ? { ...item, status: 'PAID' } : item))
    );
    showSuccess(
      'Disbursement Executed',
      `${formatCurrency(selectedVoucher.amount)} paid to ${selectedVoucher.payeeName} via ${selectedVoucher.paymentMethod}.`
    );
  };

  const columns = [
    {
      header: 'Voucher Number',
      accessor: 'id',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 font-mono block">{row.id}</span>
          <span className="text-[11px] text-slate-400">{row.payeeName}</span>
        </div>
      )
    },
    {
      header: 'Purpose',
      accessor: 'purpose',
      render: (row) => <span className="text-slate-700 text-xs truncate max-w-xs block">{row.purpose}</span>
    },
    {
      header: 'Gross Amount',
      accessor: 'amount',
      render: (row) => <span className="font-mono font-bold text-slate-900">{formatCurrency(row.amount)}</span>
    },
    {
      header: 'Disbursement Method',
      accessor: 'paymentMethod',
      render: (row) => <span className="font-semibold text-slate-700 text-xs">{row.paymentMethod.replace(/_/g, ' ')}</span>
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
          {row.status === 'DRAFT' && (
            <button
              onClick={() => handleApprove(row)}
              className="px-3 py-1 bg-blue-50 text-blue-700 hover:bg-blue-100 rounded-lg text-xs font-bold transition-colors"
            >
              Approve
            </button>
          )}
          {row.status === 'APPROVED' && (
            <button
              onClick={() => handlePay(row)}
              className="px-3 py-1 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-bold transition-colors shadow-xs"
            >
              Disburse (PIN)
            </button>
          )}
        </div>
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title="ERP: Company Disbursement Vouchers"
        subtitle="Authorize non-payroll corporate disbursements, multi-currency vendor payments, cheques, and corporate expenses."
        actions={
          <button
            onClick={() => setShowCreateModal(true)}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
          >
            <Plus className="w-3.5 h-3.5" />
            Prepare Company Voucher
          </button>
        }
      />

      <DataTable
        columns={columns}
        data={vouchers}
        searchPlaceholder="Search company disbursement vouchers..."
      />

      {/* Create Modal */}
      <Modal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        title="Prepare Company Payment Voucher"
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowCreateModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
            <button onClick={handleCreate} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Draft Voucher</button>
          </div>
        }
      >
        <form onSubmit={handleCreate} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Payee Name (Vendor / Beneficiary)</label>
            <input type="text" required value={payeeName} onChange={(e) => setPayeeName(e.target.value)} placeholder="e.g. Oracle Nigeria Ltd" className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-bold" />
          </div>
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Purpose of Disbursement</label>
            <textarea rows={2} required value={purpose} onChange={(e) => setPurpose(e.target.value)} placeholder="Explain nature of expenditure..." className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Amount (₦)</label>
              <input type="number" required value={amount} onChange={(e) => setAmount(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold" />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Disbursement Mode</label>
              <select value={paymentMethod} onChange={(e) => setPaymentMethod(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-bold">
                <option value="CORPORATE_BANK_TRANSFER">Direct Corporate NUBAN Payout</option>
                <option value="CHEQUE_PAYMENT">Bank Draft / Cheque</option>
                <option value="CORPORATE_CARD">Corporate Debit Card</option>
              </select>
            </div>
          </div>
        </form>
      </Modal>

      {/* PIN Modal */}
      <PinModal
        isOpen={showPinModal}
        onClose={() => setShowPinModal(false)}
        onConfirm={handlePinConfirm}
        title="Authorize Company Disbursement"
        amount={selectedVoucher ? formatCurrency(selectedVoucher.amount) : '0.00'}
        recipient={selectedVoucher ? selectedVoucher.payeeName : ''}
      />
    </div>
  );
}
