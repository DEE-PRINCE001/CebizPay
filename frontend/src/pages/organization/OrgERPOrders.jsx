import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Tabs from '../../components/common/Tabs';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatDate } from '../../utils/formatters';
import { ShoppingCart, Truck, Plus, CheckCircle, PackageCheck } from 'lucide-react';

export default function OrgERPOrders() {
  const [activeTab, setActiveTab] = useState('purchase'); // 'purchase' | 'sales'
  const [showModal, setShowModal] = useState(false);
  const { showSuccess } = useToast();

  const [counterparty, setCounterparty] = useState('');
  const [itemDescription, setItemDescription] = useState('');
  const [qty, setQty] = useState('10');
  const [unitPrice, setUnitPrice] = useState('280000');

  const [purchaseOrders, setPurchaseOrders] = useState([
    {
      id: 'PO-2026-001',
      supplierName: 'Dell Technologies West Africa',
      item: 'Dell UltraSharp 27" Monitors (Qty: 20)',
      totalAmount: 5600000.0,
      currency: 'NGN',
      status: 'CONFIRMED',
      receivedQuantity: 20,
      orderedQuantity: 20,
      createdAt: '2026-08-15T10:00:00Z'
    },
    {
      id: 'PO-2026-002',
      supplierName: 'Cisco Networking Systems Ltd',
      item: 'Cisco Catalyst 24-Port Gigabit Switches (Qty: 5)',
      totalAmount: 3200000.0,
      currency: 'NGN',
      status: 'DRAFT',
      receivedQuantity: 0,
      orderedQuantity: 5,
      createdAt: '2026-08-28T14:00:00Z'
    }
  ]);

  const [salesOrders, setSalesOrders] = useState([
    {
      id: 'SO-2026-001',
      customerName: 'FirstBank Digital Innovations Unit',
      item: 'Custom Core Banking Integration SOW Phase 1',
      totalAmount: 8500000.0,
      currency: 'NGN',
      status: 'CONFIRMED',
      fulfilledQuantity: 1,
      orderedQuantity: 1,
      createdAt: '2026-08-18T11:30:00Z'
    }
  ]);

  const handleCreateOrder = (e) => {
    e.preventDefault();
    const q = parseInt(qty);
    const p = parseFloat(unitPrice);
    const newO = {
      id: `${activeTab === 'purchase' ? 'PO' : 'SO'}-2026-${Date.now().toString().slice(-3)}`,
      ...(activeTab === 'purchase' ? { supplierName: counterparty } : { customerName: counterparty }),
      item: `${itemDescription} (Qty: ${q})`,
      totalAmount: q * p,
      currency: 'NGN',
      status: 'DRAFT',
      ...(activeTab === 'purchase'
        ? { receivedQuantity: 0, orderedQuantity: q }
        : { fulfilledQuantity: 0, orderedQuantity: q }),
      createdAt: new Date().toISOString()
    };

    if (activeTab === 'purchase') {
      setPurchaseOrders((prev) => [newO, ...prev]);
    } else {
      setSalesOrders((prev) => [newO, ...prev]);
    }

    showSuccess(`${activeTab === 'purchase' ? 'Purchase Order' : 'Sales Order'} Created`, `${newO.id} drafted.`);
    setShowModal(false);
    setCounterparty('');
    setItemDescription('');
  };

  const handleConfirmOrder = (orderId) => {
    if (activeTab === 'purchase') {
      setPurchaseOrders((prev) =>
        prev.map((o) => (o.id === orderId ? { ...o, status: 'CONFIRMED' } : o))
      );
    } else {
      setSalesOrders((prev) =>
        prev.map((o) => (o.id === orderId ? { ...o, status: 'CONFIRMED' } : o))
      );
    }
    showSuccess('Order Confirmed', `${orderId} status set to CONFIRMED.`);
  };

  const columns = [
    {
      header: 'Order Reference',
      accessor: 'id',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 font-mono block">{row.id}</span>
          <span className="text-[11px] text-slate-400">{row.supplierName || row.customerName}</span>
        </div>
      )
    },
    {
      header: 'Line Items Description',
      accessor: 'item',
      render: (row) => <span className="font-medium text-slate-700 text-xs">{row.item}</span>
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
      header: 'Date',
      accessor: 'createdAt',
      render: (row) => formatDate(row.createdAt, true)
    },
    {
      header: 'Actions',
      align: 'right',
      render: (row) => (
        <div className="flex items-center justify-end gap-1.5">
          {row.status === 'DRAFT' && (
            <button
              onClick={() => handleConfirmOrder(row.id)}
              className="px-3 py-1.5 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-xs font-bold transition-colors"
            >
              Confirm Order
            </button>
          )}
        </div>
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title="ERP: Purchase &amp; Sales Orders"
        subtitle="Manage end-to-end procurement purchase orders and customer sales order fulfillment workflows."
        actions={
          <button
            onClick={() => setShowModal(true)}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
          >
            <Plus className="w-3.5 h-3.5" />
            Create {activeTab === 'purchase' ? 'Purchase Order' : 'Sales Order'}
          </button>
        }
      />

      <Tabs
        tabs={[
          { id: 'purchase', label: 'Purchase Orders (Procurement)', count: purchaseOrders.length, icon: Truck },
          { id: 'sales', label: 'Sales Orders (Fulfillment)', count: salesOrders.length, icon: ShoppingCart }
        ]}
        activeTab={activeTab}
        onChange={setActiveTab}
        className="mb-6"
      />

      <DataTable
        columns={columns}
        data={activeTab === 'purchase' ? purchaseOrders : salesOrders}
        searchPlaceholder="Search orders..."
      />

      {/* Modal */}
      <Modal
        isOpen={showModal}
        onClose={() => setShowModal(false)}
        title={`Draft ${activeTab === 'purchase' ? 'Purchase Order' : 'Sales Order'}`}
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
            <button onClick={handleCreateOrder} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Draft Order</button>
          </div>
        }
      >
        <form onSubmit={handleCreateOrder} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1">
              {activeTab === 'purchase' ? 'Supplier Name' : 'Customer Name'}
            </label>
            <input type="text" required value={counterparty} onChange={(e) => setCounterparty(e.target.value)} placeholder="Company name..." className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-bold" />
          </div>
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Item / Service Description</label>
            <input type="text" required value={itemDescription} onChange={(e) => setItemDescription(e.target.value)} placeholder="Product description..." className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Quantity</label>
              <input type="number" required value={qty} onChange={(e) => setQty(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold" />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Unit Price (₦)</label>
              <input type="number" required value={unitPrice} onChange={(e) => setUnitPrice(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold" />
            </div>
          </div>
        </form>
      </Modal>
    </div>
  );
}
