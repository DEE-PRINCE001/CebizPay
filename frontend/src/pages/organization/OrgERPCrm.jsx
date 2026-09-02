import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Tabs from '../../components/common/Tabs';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import PhoneInput from '../../components/common/PhoneInput';
import { Contact, Truck, Plus, Mail, Phone, MapPin } from 'lucide-react';

export default function OrgERPCrm() {
  const [activeTab, setActiveTab] = useState('customers'); // 'customers' | 'suppliers'
  const [showModal, setShowModal] = useState(false);
  const { showSuccess } = useToast();

  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [address, setAddress] = useState('');
  const [channelOrPolicy, setChannelOrPolicy] = useState('');

  const [customers, setCustomers] = useState([]);
  const [suppliers, setSuppliers] = useState([]);

  const handleCreate = (e) => {
    e.preventDefault();
    const newItem = {
      id: `${activeTab === 'customers' ? 'cust' : 'sup'}-${Date.now()}`,
      name,
      email,
      phone,
      address,
      ...(activeTab === 'customers'
        ? { acquisitionChannel: channelOrPolicy || 'Organic Referral', ordersCount: 0 }
        : { returnPolicy: channelOrPolicy || 'Standard Vendor Terms', activeOrders: 0 }),
      status: 'ACTIVE'
    };

    if (activeTab === 'customers') {
      setCustomers((prev) => [newItem, ...prev]);
    } else {
      setSuppliers((prev) => [newItem, ...prev]);
    }

    showSuccess(`${activeTab === 'customers' ? 'Customer' : 'Supplier'} Registered`, `${name} added to CRM.`);
    setShowModal(false);
    setName('');
    setEmail('');
    setPhone('');
    setAddress('');
    setChannelOrPolicy('');
  };

  const customerColumns = [
    {
      header: 'Customer / Client Name',
      accessor: 'name',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.name}</span>
          <span className="text-[11px] text-slate-400">{row.email}</span>
        </div>
      )
    },
    {
      header: 'Phone & Address',
      accessor: 'phone',
      render: (row) => (
        <div>
          <span className="text-slate-800 text-xs block font-mono">{row.phone}</span>
          <span className="text-[10px] text-slate-500 truncate max-w-xs block">{row.address}</span>
        </div>
      )
    },
    {
      header: 'Acquisition Channel',
      accessor: 'acquisitionChannel',
      render: (row) => <span className="text-slate-600 text-xs">{row.acquisitionChannel}</span>
    },
    {
      header: 'Total Orders',
      accessor: 'ordersCount',
      render: (row) => <span className="font-bold text-blue-700">{row.ordersCount} Orders</span>
    },
    {
      header: 'Status',
      accessor: 'status',
      render: (row) => <Badge status={row.status} size="sm" />
    }
  ];

  const supplierColumns = [
    {
      header: 'Supplier / Vendor Name',
      accessor: 'name',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.name}</span>
          <span className="text-[11px] text-slate-400">{row.email}</span>
        </div>
      )
    },
    {
      header: 'Phone & Address',
      accessor: 'phone',
      render: (row) => (
        <div>
          <span className="text-slate-800 text-xs block font-mono">{row.phone}</span>
          <span className="text-[10px] text-slate-500 truncate max-w-xs block">{row.address}</span>
        </div>
      )
    },
    {
      header: 'Return / SLA Policy',
      accessor: 'returnPolicy',
      render: (row) => <span className="text-slate-600 text-xs truncate max-w-xs block">{row.returnPolicy}</span>
    },
    {
      header: 'Active POs',
      accessor: 'activeOrders',
      render: (row) => <span className="font-bold text-purple-700">{row.activeOrders} Active POs</span>
    },
    {
      header: 'Status',
      accessor: 'status',
      render: (row) => <Badge status={row.status} size="sm" />
    }
  ];

  return (
    <div>
      <PageHeader
        title="ERP: Customer &amp; Supplier CRM Directory"
        subtitle="Manage business relationships, acquisition channels, return policies, and purchase history."
        actions={
          <button
            onClick={() => setShowModal(true)}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
          >
            <Plus className="w-3.5 h-3.5" />
            Add {activeTab === 'customers' ? 'Customer Profile' : 'Supplier Profile'}
          </button>
        }
      />

      <Tabs
        tabs={[
          { id: 'customers', label: 'Customer CRM', count: customers.length, icon: Contact },
          { id: 'suppliers', label: 'Supplier / Vendor CRM', count: suppliers.length, icon: Truck }
        ]}
        activeTab={activeTab}
        onChange={setActiveTab}
        className="mb-6"
      />

      <DataTable
        columns={activeTab === 'customers' ? customerColumns : supplierColumns}
        data={activeTab === 'customers' ? customers : suppliers}
        searchPlaceholder={`Search ${activeTab}...`}
      />

      {/* Modal */}
      <Modal
        isOpen={showModal}
        onClose={() => setShowModal(false)}
        title={`Register ${activeTab === 'customers' ? 'Customer' : 'Supplier'} Profile`}
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
            <button onClick={handleCreate} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Save Profile</button>
          </div>
        }
      >
        <form onSubmit={handleCreate} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Company / Entity Name</label>
            <input type="text" required value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Zenith Bank Corporate" className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-bold" />
          </div>
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Email Address</label>
            <input type="email" required value={email} onChange={(e) => setEmail(e.target.value)} placeholder="contact@company.com" className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl" />
          </div>
          <div>
            <PhoneInput
              label="Contact Phone Number"
              required
              value={phone}
              onChange={setPhone}
            />
          </div>
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Physical Address</label>
            <input type="text" required value={address} onChange={(e) => setAddress(e.target.value)} placeholder="Office location..." className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl" />
          </div>
          <div>
            <label className="block font-semibold text-slate-700 mb-1">
              {activeTab === 'customers' ? 'Acquisition Channel' : 'Return / RMA Terms'}
            </label>
            <input type="text" value={channelOrPolicy} onChange={(e) => setChannelOrPolicy(e.target.value)} placeholder={activeTab === 'customers' ? 'e.g. Referral / Inbound' : 'e.g. 14-Day Replacement'} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl" />
          </div>
        </form>
      </Modal>
    </div>
  );
}
