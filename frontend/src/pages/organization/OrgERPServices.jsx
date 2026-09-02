import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Tabs from '../../components/common/Tabs';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency } from '../../utils/formatters';
import { Compass, Plus, ArrowUpRight, ArrowDownLeft } from 'lucide-react';

export default function OrgERPServices() {
  const [activeTab, setActiveTab] = useState('rendered'); // 'rendered' | 'bought'
  const [showModal, setShowModal] = useState(false);
  const { showSuccess } = useToast();

  const [serviceName, setServiceName] = useState('');
  const [billingType, setBillingType] = useState('HOURLY');
  const [rate, setRate] = useState('45000');
  const [category, setCategory] = useState('Cloud Architecture');

  const [servicesRendered, setServicesRendered] = useState([]);
  const [servicesBought, setServicesBought] = useState([]);

  const handleCreate = (e) => {
    e.preventDefault();
    const newS = {
      id: `srv-${Date.now()}`,
      name: serviceName,
      category,
      billingType,
      rate: parseFloat(rate),
      currency: 'NGN',
      isActive: true
    };
    if (activeTab === 'rendered') {
      setServicesRendered((prev) => [newS, ...prev]);
    } else {
      setServicesBought((prev) => [{ ...newS, supplierName: 'External Vendor' }, ...prev]);
    }
    showSuccess('Service Cataloged', `${serviceName} saved.`);
    setShowModal(false);
    setServiceName('');
  };

  const columns = [
    {
      header: 'Service Name',
      accessor: 'name',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.name}</span>
          <span className="text-[11px] text-slate-400">{row.category || row.supplierName}</span>
        </div>
      )
    },
    {
      header: 'Billing Type',
      accessor: 'billingType',
      render: (row) => <Badge status="ACTIVE" label={row.billingType.replace('_', ' ')} size="sm" />
    },
    {
      header: 'Standard Rate',
      accessor: 'rate',
      render: (row) => <span className="font-mono font-bold text-slate-900">{formatCurrency(row.rate)}</span>
    },
    {
      header: 'Catalog Status',
      accessor: 'isActive',
      render: (row) => <Badge status={row.isActive ? 'ACTIVE' : 'DRAFT'} size="sm" />
    }
  ];

  return (
    <div>
      <PageHeader
        title="ERP: Services Catalog"
        subtitle="Maintain service price lists for services rendered to corporate clients and recurring vendor services bought."
        actions={
          <button
            onClick={() => setShowModal(true)}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
          >
            <Plus className="w-3.5 h-3.5" />
            Add {activeTab === 'rendered' ? 'Rendered Service' : 'Vendor Service'}
          </button>
        }
      />

      <Tabs
        tabs={[
          { id: 'rendered', label: 'Services Rendered (Sales Catalog)', count: servicesRendered.length, icon: ArrowUpRight },
          { id: 'bought', label: 'Services Bought (Vendor Subscriptions)', count: servicesBought.length, icon: ArrowDownLeft }
        ]}
        activeTab={activeTab}
        onChange={setActiveTab}
        className="mb-6"
      />

      <DataTable
        columns={columns}
        data={activeTab === 'rendered' ? servicesRendered : servicesBought}
        searchPlaceholder="Search services catalog..."
      />

      {/* Modal */}
      <Modal
        isOpen={showModal}
        onClose={() => setShowModal(false)}
        title={`Add ${activeTab === 'rendered' ? 'Rendered Service' : 'Vendor Service'}`}
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
            <button onClick={handleCreate} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Save to Catalog</button>
          </div>
        }
      >
        <form onSubmit={handleCreate} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Service Title</label>
            <input type="text" required value={serviceName} onChange={(e) => setServiceName(e.target.value)} placeholder="e.g. Enterprise Security Audit" className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-bold" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Billing Model</label>
              <select value={billingType} onChange={(e) => setBillingType(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl">
                <option value="HOURLY">Hourly Rate</option>
                <option value="FIXED_PROJECT">Fixed Project Fee</option>
                <option value="MONTHLY">Monthly Retainer</option>
              </select>
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Rate / Unit Fee (₦)</label>
              <input type="number" required value={rate} onChange={(e) => setRate(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold" />
            </div>
          </div>
        </form>
      </Modal>
    </div>
  );
}
