import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatDate } from '../../utils/formatters';
import { FileText, Plus, DollarSign, Tag } from 'lucide-react';

export default function OrgERPExpenses() {
  const [showModal, setShowModal] = useState(false);
  const { showSuccess } = useToast();

  const [category, setCategory] = useState('Office Utilities');
  const [amount, setAmount] = useState('350000');
  const [description, setDescription] = useState('');
  const [paymentMode, setPaymentMode] = useState('CORPORATE_WALLET');

  const [expenses, setExpenses] = useState([]);

  const handleCreate = (e) => {
    e.preventDefault();
    const newExp = {
      id: `exp-${Date.now()}`,
      category,
      description,
      amount: parseFloat(amount),
      currency: 'NGN',
      paymentMode,
      date: new Date().toISOString(),
      recordedBy: 'Finance Ops'
    };
    setExpenses((prev) => [newExp, ...prev]);
    showSuccess('Expense Recorded', `${formatCurrency(newExp.amount)} logged under ${category}.`);
    setShowModal(false);
    setDescription('');
  };

  const columns = [
    {
      header: 'Expense Category',
      accessor: 'category',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.category}</span>
          <span className="text-[11px] text-slate-400 font-mono">{row.id}</span>
        </div>
      )
    },
    {
      header: 'Description',
      accessor: 'description',
      render: (row) => <span className="text-slate-700 text-xs truncate max-w-sm block">{row.description}</span>
    },
    {
      header: 'Amount',
      accessor: 'amount',
      render: (row) => <span className="font-mono font-bold text-slate-900">{formatCurrency(row.amount)}</span>
    },
    {
      header: 'Payment Mode',
      accessor: 'paymentMode',
      render: (row) => <Badge status="ACTIVE" label={row.paymentMode.replace('_', ' ')} size="sm" />
    },
    {
      header: 'Date',
      accessor: 'date',
      render: (row) => formatDate(row.date, true)
    }
  ];

  return (
    <div>
      <PageHeader
        title="ERP: Operating Expenses (OPEX)"
        subtitle="Record corporate operating expenditures, categorization, and payment mode traceability."
        actions={
          <button
            onClick={() => setShowModal(true)}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
          >
            <Plus className="w-3.5 h-3.5" />
            Record Operating Expense
          </button>
        }
      />

      <DataTable
        columns={columns}
        data={expenses}
        searchPlaceholder="Search expenses..."
      />

      {/* Modal */}
      <Modal
        isOpen={showModal}
        onClose={() => setShowModal(false)}
        title="Record Operating Expense"
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
            <button onClick={handleCreate} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Save Expense</button>
          </div>
        }
      >
        <form onSubmit={handleCreate} className="space-y-4 text-xs text-left">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Expense Category</label>
              <select value={category} onChange={(e) => setCategory(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl">
                <option value="Office Utilities & Power">Office Utilities &amp; Power</option>
                <option value="Software Licenses">Software Licenses</option>
                <option value="Marketing & Acquisition">Marketing &amp; Acquisition</option>
                <option value="Legal & Regulatory">Legal &amp; Regulatory</option>
                <option value="Travel & Logistics">Travel &amp; Logistics</option>
              </select>
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Expense Amount (₦)</label>
              <input type="number" required value={amount} onChange={(e) => setAmount(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold" />
            </div>
          </div>
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Description &amp; Purpose</label>
            <textarea rows={3} required value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Provide invoice reference or itemized description..." className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl" />
          </div>
        </form>
      </Modal>
    </div>
  );
}
