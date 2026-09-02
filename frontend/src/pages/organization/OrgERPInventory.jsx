import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatDate } from '../../utils/formatters';
import { Package, Plus, RefreshCw, AlertTriangle, ArrowUpRight } from 'lucide-react';

export default function OrgERPInventory() {
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showRestockModal, setShowRestockModal] = useState(false);
  const [selectedItem, setSelectedItem] = useState(null);
  const [restockQty, setRestockQty] = useState('50');
  const [restockCost, setRestockCost] = useState('12000');
  const { showSuccess } = useToast();

  const [itemName, setItemName] = useState('');
  const [sku, setSku] = useState('');
  const [unitCost, setUnitCost] = useState('15000');
  const [sellingPrice, setSellingPrice] = useState('24000');
  const [initialQty, setInitialQty] = useState('100');
  const [reorderLevel, setReorderLevel] = useState('20');

  const [inventory, setInventory] = useState([
    {
      id: 'inv-01',
      name: 'Dell UltraSharp 27" 4K Monitor',
      sku: 'HW-MON-4K-27',
      category: 'Hardware Assets',
      unitCost: 280000.0,
      sellingPrice: 380000.0,
      quantityOnHand: 42,
      reorderLevel: 10,
      status: 'IN_STOCK',
      valuation: 11760000.0
    },
    {
      id: 'inv-02',
      name: 'MacBook Pro M3 Max 16"',
      sku: 'HW-MBP-M3-16',
      category: 'Hardware Assets',
      unitCost: 2850000.0,
      sellingPrice: 3450000.0,
      quantityOnHand: 4,
      reorderLevel: 5,
      status: 'LOW_STOCK',
      valuation: 11400000.0
    },
    {
      id: 'inv-03',
      name: 'Logitech MX Master 3S Mouse',
      sku: 'ACC-LOG-MX3S',
      category: 'Peripherals',
      unitCost: 85000.0,
      sellingPrice: 130000.0,
      quantityOnHand: 0,
      reorderLevel: 15,
      status: 'OUT_OF_STOCK',
      valuation: 0
    }
  ]);

  const handleCreateItem = (e) => {
    e.preventDefault();
    const qty = parseInt(initialQty);
    const reorder = parseInt(reorderLevel);
    const cost = parseFloat(unitCost);
    const newItem = {
      id: `inv-${Date.now()}`,
      name: itemName,
      sku,
      category: 'General Inventory',
      unitCost: cost,
      sellingPrice: parseFloat(sellingPrice),
      quantityOnHand: qty,
      reorderLevel: reorder,
      status: qty === 0 ? 'OUT_OF_STOCK' : qty <= reorder ? 'LOW_STOCK' : 'IN_STOCK',
      valuation: qty * cost
    };
    setInventory((prev) => [newItem, ...prev]);
    showSuccess('Inventory Item Created', `${itemName} registered in item catalog.`);
    setShowCreateModal(false);
    setItemName('');
    setSku('');
  };

  const handleRestock = (e) => {
    e.preventDefault();
    const added = parseInt(restockQty);
    const newQty = selectedItem.quantityOnHand + added;
    setInventory((prev) =>
      prev.map((item) =>
        item.id === selectedItem.id
          ? {
              ...item,
              quantityOnHand: newQty,
              status: newQty <= item.reorderLevel ? 'LOW_STOCK' : 'IN_STOCK',
              valuation: newQty * item.unitCost
            }
          : item
      )
    );
    showSuccess('Restock Recorded', `Added ${added} units of ${selectedItem.name}.`);
    setShowRestockModal(false);
  };

  const columns = [
    {
      header: 'Item & SKU',
      accessor: 'name',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.name}</span>
          <span className="text-[11px] text-slate-400 font-mono">{row.sku}</span>
        </div>
      )
    },
    {
      header: 'Unit Cost',
      accessor: 'unitCost',
      render: (row) => <span className="font-mono text-slate-600">{formatCurrency(row.unitCost)}</span>
    },
    {
      header: 'Selling Price',
      accessor: 'sellingPrice',
      render: (row) => <span className="font-mono font-bold text-slate-900">{formatCurrency(row.sellingPrice)}</span>
    },
    {
      header: 'On Hand',
      accessor: 'quantityOnHand',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-800 text-xs block">{row.quantityOnHand} Units</span>
          <span className="text-[10px] text-slate-400">Reorder at: {row.reorderLevel}</span>
        </div>
      )
    },
    {
      header: 'Total Valuation',
      accessor: 'valuation',
      render: (row) => <span className="font-mono font-bold text-blue-700">{formatCurrency(row.valuation)}</span>
    },
    {
      header: 'Stock Status',
      accessor: 'status',
      render: (row) => <Badge status={row.status} size="sm" />
    },
    {
      header: 'Actions',
      align: 'right',
      render: (row) => (
        <button
          onClick={() => {
            setSelectedItem(row);
            setShowRestockModal(true);
          }}
          className="px-3 py-1.5 bg-blue-50 text-blue-700 hover:bg-blue-100 rounded-lg text-xs font-bold transition-colors"
        >
          Restock
        </button>
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title="ERP: Inventory &amp; Stock Management"
        subtitle="Catalog goods, track stock movements, automate reorder thresholds, and calculate total balance sheet valuation."
        actions={
          <button
            onClick={() => setShowCreateModal(true)}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
          >
            <Plus className="w-3.5 h-3.5" />
            Add Inventory Item
          </button>
        }
      />

      <DataTable
        columns={columns}
        data={inventory}
        searchPlaceholder="Search inventory by product name or SKU..."
      />

      {/* Create Modal */}
      <Modal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        title="Register Inventory Item"
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowCreateModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
            <button onClick={handleCreateItem} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Save Item</button>
          </div>
        }
      >
        <form onSubmit={handleCreateItem} className="space-y-4 text-xs text-left">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Item / Product Name</label>
              <input type="text" required value={itemName} onChange={(e) => setItemName(e.target.value)} placeholder="e.g. Server Rack Unit" className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl" />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">SKU (Stock Keeping Unit)</label>
              <input type="text" required value={sku} onChange={(e) => setSku(e.target.value)} placeholder="SRV-RCK-01" className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono" />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Unit Cost Price (₦)</label>
              <input type="number" required value={unitCost} onChange={(e) => setUnitCost(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono" />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Selling Price (₦)</label>
              <input type="number" required value={sellingPrice} onChange={(e) => setSellingPrice(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold" />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Initial Quantity on Hand</label>
              <input type="number" required value={initialQty} onChange={(e) => setInitialQty(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono" />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Reorder Level Alert Threshold</label>
              <input type="number" required value={reorderLevel} onChange={(e) => setReorderLevel(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono" />
            </div>
          </div>
        </form>
      </Modal>

      {/* Restock Modal */}
      {selectedItem && (
        <Modal
          isOpen={showRestockModal}
          onClose={() => setShowRestockModal(false)}
          title={`Restock: ${selectedItem.name}`}
          subtitle={`Current stock: ${selectedItem.quantityOnHand} units`}
          footer={
            <div className="flex items-center justify-end gap-3 w-full">
              <button onClick={() => setShowRestockModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
              <button onClick={handleRestock} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Confirm Restock</button>
            </div>
          }
        >
          <form onSubmit={handleRestock} className="space-y-4 text-xs text-left">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Additional Units Received</label>
              <input type="number" required value={restockQty} onChange={(e) => setRestockQty(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold" />
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}
