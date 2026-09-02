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
import { Receipt, Plus, Eye, CheckCircle2, Printer, ArrowRight, ShieldCheck } from 'lucide-react';

export default function OrgERPInvoices() {
  const { activeOrg } = useAuth();
  const { showSuccess } = useToast();

  const [activeTab, setActiveTab] = useState('invoices'); // 'invoices' | 'receipts'
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showViewModal, setShowViewModal] = useState(false);
  const [showPinModal, setShowPinModal] = useState(false);
  const [selectedInvoice, setSelectedInvoice] = useState(null);

  // Form state
  const [customerName, setCustomerName] = useState('FirstBank Digital Innovations Unit');
  const [itemDescription, setItemDescription] = useState('Core Banking Microservices Integration');
  const [itemAmount, setItemAmount] = useState('5000000');
  const [includeVat, setIncludeVat] = useState(true);

  // Invoices list
  const [invoices, setInvoices] = useState([
    {
      id: 'INV-2026-001',
      customerName: 'FirstBank Digital Innovations Unit',
      description: 'Core Banking API Integration (Milestone 1)',
      subtotal: 5000000.0,
      vatRate: 0.075, // 7.5% statutory VAT
      vatAmount: 375000.0,
      totalAmount: 5375000.0,
      currency: 'NGN',
      status: 'ISSUED',
      createdAt: '2026-08-20T10:00:00Z',
      dueDate: '2026-09-20T23:59:59Z'
    },
    {
      id: 'INV-2026-002',
      customerName: 'Moniepoint MFB Corporate Accounts',
      description: 'Dedicated Settlement Rail Implementation',
      subtotal: 3500000.0,
      vatRate: 0.075,
      vatAmount: 262500.0,
      totalAmount: 3762500.0,
      currency: 'NGN',
      status: 'PAID',
      createdAt: '2026-08-15T14:30:00Z',
      dueDate: '2026-09-15T23:59:59Z',
      settledAt: '2026-08-25T11:00:00Z'
    }
  ]);

  // Receipts list
  const [receipts, setReceipts] = useState([
    {
      id: 'REC-2026-001',
      receiptNumber: 'RCPT-MNPT-88492',
      invoiceId: 'INV-2026-002',
      customerName: 'Moniepoint MFB Corporate Accounts',
      amountPaid: 3762500.0,
      paymentMethod: 'DEDICATED_VIRTUAL_ACCOUNT',
      currency: 'NGN',
      status: 'VERIFIED',
      issuedAt: '2026-08-25T11:00:00Z'
    }
  ]);

  const handleCreateInvoice = (e) => {
    e.preventDefault();
    const sub = parseFloat(itemAmount);
    const vat = includeVat ? sub * 0.075 : 0;
    const total = sub + vat;

    const newInv = {
      id: `INV-2026-${Date.now().toString().slice(-3)}`,
      customerName,
      description: itemDescription,
      subtotal: sub,
      vatRate: includeVat ? 0.075 : 0,
      vatAmount: vat,
      totalAmount: total,
      currency: 'NGN',
      status: 'ISSUED',
      createdAt: new Date().toISOString(),
      dueDate: new Date(Date.now() + 30 * 24 * 3600 * 1000).toISOString()
    };

    setInvoices((prev) => [newInv, ...prev]);
    showSuccess('Invoice Issued', `${newInv.id} issued for ${formatCurrency(newInv.totalAmount)} (incl. 7.5% VAT).`);
    setShowCreateModal(false);
  };

  const handleRecordPayment = (invoice) => {
    setSelectedInvoice(invoice);
    setShowPinModal(true);
  };

  const handlePinConfirm = (pin) => {
    setShowPinModal(false);
    const settledTime = new Date().toISOString();

    setInvoices((prev) =>
      prev.map((inv) =>
        inv.id === selectedInvoice.id
          ? { ...inv, status: 'PAID', settledAt: settledTime }
          : inv
      )
    );

    const newReceipt = {
      id: `REC-${Date.now()}`,
      receiptNumber: `RCPT-SETTLE-${Date.now().toString().slice(-5)}`,
      invoiceId: selectedInvoice.id,
      customerName: selectedInvoice.customerName,
      amountPaid: selectedInvoice.totalAmount,
      paymentMethod: 'CORPORATE_WALLET_SETTLEMENT',
      currency: 'NGN',
      status: 'VERIFIED',
      issuedAt: settledTime
    };

    setReceipts((prev) => [newReceipt, ...prev]);
    showSuccess(
      'Payment Settled & Receipt Generated',
      `Invoice ${selectedInvoice.id} settled. Immutable Receipt ${newReceipt.receiptNumber} generated.`
    );
  };

  const invoiceColumns = [
    {
      header: 'Invoice Number',
      accessor: 'id',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 font-mono block">{row.id}</span>
          <span className="text-[11px] text-slate-400">{row.customerName}</span>
        </div>
      )
    },
    {
      header: 'Description',
      accessor: 'description',
      render: (row) => <span className="text-slate-700 text-xs truncate max-w-xs block">{row.description}</span>
    },
    {
      header: 'Subtotal',
      accessor: 'subtotal',
      render: (row) => <span className="font-mono text-slate-600">{formatCurrency(row.subtotal)}</span>
    },
    {
      header: 'VAT (7.5%)',
      accessor: 'vatAmount',
      render: (row) => <span className="font-mono text-slate-600 font-medium">+{formatCurrency(row.vatAmount)}</span>
    },
    {
      header: 'Gross Total',
      accessor: 'totalAmount',
      render: (row) => <span className="font-mono font-bold text-slate-900">{formatCurrency(row.totalAmount)}</span>
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
          <button
            onClick={() => {
              setSelectedInvoice(row);
              setShowViewModal(true);
            }}
            className="p-1.5 text-slate-500 hover:text-blue-600 hover:bg-slate-100 rounded-lg"
            title="View Invoice"
          >
            <Eye className="w-4 h-4" />
          </button>
          {row.status === 'ISSUED' && (
            <button
              onClick={() => handleRecordPayment(row)}
              className="px-3 py-1 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-bold transition-colors shadow-xs"
            >
              Record Payment
            </button>
          )}
        </div>
      )
    }
  ];

  const receiptColumns = [
    {
      header: 'Receipt Number',
      accessor: 'receiptNumber',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 font-mono block">{row.receiptNumber}</span>
          <span className="text-[11px] text-slate-400 font-mono">For Invoice: {row.invoiceId}</span>
        </div>
      )
    },
    {
      header: 'Customer',
      accessor: 'customerName',
      render: (row) => <span className="font-bold text-slate-800 text-xs">{row.customerName}</span>
    },
    {
      header: 'Amount Paid',
      accessor: 'amountPaid',
      render: (row) => <span className="font-mono font-bold text-emerald-700">{formatCurrency(row.amountPaid)}</span>
    },
    {
      header: 'Payment Rail',
      accessor: 'paymentMethod',
      render: (row) => <Badge status="ACTIVE" label={row.paymentMethod.replace(/_/g, ' ')} size="sm" />
    },
    {
      header: 'Settlement Date',
      accessor: 'issuedAt',
      render: (row) => formatDate(row.issuedAt, true)
    }
  ];

  return (
    <div>
      <PageHeader
        title="ERP: Invoicing &amp; Immutable Receipts"
        subtitle="Multi-line item billing with statutory 7.5% VAT calculation, client settlement recording, and audit-proof receipts."
        actions={
          <button
            onClick={() => setShowCreateModal(true)}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
          >
            <Plus className="w-3.5 h-3.5" />
            Issue New Invoice
          </button>
        }
      />

      <Tabs
        tabs={[
          { id: 'invoices', label: 'Invoices & Billing', count: invoices.length, icon: Receipt },
          { id: 'receipts', label: 'Payment Receipts', count: receipts.length, icon: CheckCircle2 }
        ]}
        activeTab={activeTab}
        onChange={setActiveTab}
        className="mb-6"
      />

      {activeTab === 'invoices' && <DataTable columns={invoiceColumns} data={invoices} searchPlaceholder="Search invoices..." />}
      {activeTab === 'receipts' && <DataTable columns={receiptColumns} data={receipts} searchPlaceholder="Search receipts..." />}

      {/* Create Modal */}
      <Modal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        title="Issue Commercial Invoice"
        subtitle="Generates an invoice with automatic 7.5% Nigerian statutory VAT computation."
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowCreateModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
            <button onClick={handleCreateInvoice} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Issue Commercial Invoice</button>
          </div>
        }
      >
        <form onSubmit={handleCreateInvoice} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Customer / Client</label>
            <input type="text" required value={customerName} onChange={(e) => setCustomerName(e.target.value)} placeholder="Client Name..." className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-bold" />
          </div>
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Service / Goods Description</label>
            <input type="text" required value={itemDescription} onChange={(e) => setItemDescription(e.target.value)} placeholder="Description of services delivered..." className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Subtotal Amount (₦)</label>
              <input type="number" required value={itemAmount} onChange={(e) => setItemAmount(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold" />
            </div>
            <div className="flex items-center pt-6">
              <label className="flex items-center gap-2 cursor-pointer font-bold text-slate-800">
                <input type="checkbox" checked={includeVat} onChange={(e) => setIncludeVat(e.target.checked)} className="w-4 h-4 rounded text-blue-600" />
                Apply 7.5% Statutory VAT (+₦{(parseFloat(itemAmount || 0) * 0.075).toLocaleString()})
              </label>
            </div>
          </div>
        </form>
      </Modal>

      {/* View Invoice Modal */}
      {selectedInvoice && (
        <Modal
          isOpen={showViewModal}
          onClose={() => setShowViewModal(false)}
          title={`Commercial Invoice: ${selectedInvoice.id}`}
          footer={
            <div className="flex items-center justify-between w-full">
              <button onClick={() => window.print()} className="px-4 py-2 text-xs font-bold text-slate-800 bg-slate-100 hover:bg-slate-200 rounded-xl flex items-center gap-1.5">
                <Printer className="w-3.5 h-3.5" /> Print Invoice
              </button>
              <button onClick={() => setShowViewModal(false)} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Close</button>
            </div>
          }
        >
          <div className="p-6 bg-slate-50 rounded-2xl border border-slate-200 space-y-4 text-xs text-left">
            <div className="flex justify-between items-start pb-3 border-b border-slate-200">
              <div>
                <h4 className="font-bold text-slate-900 text-sm">{activeOrg?.name}</h4>
                <p className="text-slate-500 text-[11px]">TIN: 22839401-0001 • RC: {activeOrg?.cacNumber}</p>
              </div>
              <Badge status={selectedInvoice.status} />
            </div>

            <div className="grid grid-cols-2 gap-3 py-2 border-b border-slate-200">
              <div>
                <span className="text-slate-400 block text-[10px] uppercase font-bold">Billed To</span>
                <span className="font-bold text-slate-900 text-sm">{selectedInvoice.customerName}</span>
              </div>
              <div className="text-right">
                <span className="text-slate-400 block text-[10px] uppercase font-bold">Due Date</span>
                <span className="font-mono text-slate-700">{formatDate(selectedInvoice.dueDate)}</span>
              </div>
            </div>

            <div className="space-y-2 py-2 border-b border-slate-200 font-mono">
              <div className="flex justify-between">
                <span className="text-slate-600 font-sans">Subtotal (Services Delivered):</span>
                <span className="font-bold text-slate-900">{formatCurrency(selectedInvoice.subtotal)}</span>
              </div>
              <div className="flex justify-between text-slate-600">
                <span className="font-sans">Statutory VAT (7.5%):</span>
                <span>+{formatCurrency(selectedInvoice.vatAmount)}</span>
              </div>
              <div className="flex justify-between text-base font-bold text-slate-900 pt-2 border-t border-slate-200">
                <span className="font-sans">Total Gross Due:</span>
                <span>{formatCurrency(selectedInvoice.totalAmount)}</span>
              </div>
            </div>
          </div>
        </Modal>
      )}

      {/* PIN Modal for Payment Settlement */}
      <PinModal
        isOpen={showPinModal}
        onClose={() => setShowPinModal(false)}
        onConfirm={handlePinConfirm}
        title="Authorize Invoice Payment Settlement"
        amount={selectedInvoice ? formatCurrency(selectedInvoice.totalAmount) : '0.00'}
        recipient={selectedInvoice ? selectedInvoice.customerName : ''}
      />
    </div>
  );
}
