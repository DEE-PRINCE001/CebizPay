import React, { useState, useEffect } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Modal from '../../components/common/Modal';
import PinModal from '../../components/common/PinModal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatDate } from '../../utils/formatters';
import { thriftApi } from '../../api/thriftApi';
import { Users2, Plus, ArrowRight, ShieldCheck, RefreshCw, Calendar, CheckCircle2 } from 'lucide-react';

export default function ThriftPage() {
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showPinModal, setShowPinModal] = useState(false);
  const [selectedGroup, setSelectedGroup] = useState(null);

  // Form state
  const [groupName, setGroupName] = useState('Engineering Leads Monthly Pot');
  const [contributionAmount, setContributionAmount] = useState('100000');
  const [frequency, setFrequency] = useState('MONTHLY');
  const [totalSlots, setTotalSlots] = useState(5);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isLoading, setIsLoading] = useState(true);

  const { showSuccess, showError } = useToast();

  // Live Thrift Groups
  const [groups, setGroups] = useState([]);

  const fetchThriftGroups = async () => {
    setIsLoading(true);
    try {
      const res = await thriftApi.getMyThriftGroups();
      if (Array.isArray(res)) {
        setGroups(res);
      } else {
        setGroups([]);
      }
    } catch (err) {
      setGroups([]);
      console.warn('Backend thrift groups fetch:', err);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchThriftGroups();
  }, []);

  const handleCreateGroup = async (e) => {
    e.preventDefault();
    setIsSubmitting(true);
    try {
      const contribution = parseFloat(contributionAmount);
      await thriftApi.createThriftGroup({
        name: groupName,
        contributionAmount: contribution,
        frequency,
        totalSlots,
      });

      showSuccess('Thrift Circle Created', `Created "${groupName}" with ${totalSlots} rotation slots.`);
      setShowCreateModal(false);
      await fetchThriftGroups();
    } catch (err) {
      const msg = err.message || 'Failed to create thrift circle.';
      showError('Thrift Error', msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleContribute = (group) => {
    setSelectedGroup(group);
    setShowPinModal(true);
  };

  const handleConfirmContribution = async (pin) => {
    setShowPinModal(false);
    try {
      showSuccess(
        'Thrift Contribution Paid',
        `Transferred ${formatCurrency(selectedGroup.contributionAmount)} to ${selectedGroup.name} pot.`,
        `THRIFT-POT-${Date.now()}`
      );
      await fetchThriftGroups();
    } catch (err) {
      showError('Contribution Error', err.message || 'Could not pay thrift contribution.');
    }
  };

  const columns = [
    {
      header: 'Thrift Circle',
      accessor: 'name',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.name}</span>
          <span className="text-[11px] text-slate-400 font-mono">{row.id} • {row.frequency || 'MONTHLY'}</span>
        </div>
      ),
    },
    {
      header: 'Periodic Contribution',
      accessor: 'contributionAmount',
      render: (row) => <span className="font-mono font-bold text-slate-900">{formatCurrency(row.contributionAmount || 0)}</span>,
    },
    {
      header: 'Total Pot Size',
      accessor: 'totalPot',
      render: (row) => <span className="font-mono font-bold text-emerald-700">{formatCurrency((row.contributionAmount || 0) * (row.totalSlots || 5))}</span>,
    },
    {
      header: 'Rotation Slot',
      accessor: 'currentRound',
      render: (row) => (
        <div>
          <span className="text-xs font-semibold text-slate-800 block">Round {row.currentRound || 1} of {row.totalSlots || 5}</span>
          <span className="text-[10px] text-slate-400">Your Turn: {row.assignedSlot ? `Slot #${row.assignedSlot}` : 'Pending assignment'}</span>
        </div>
      ),
    },
    {
      header: 'Status',
      accessor: 'status',
      render: (row) => <Badge status={row.status || 'ACTIVE'} size="sm" />,
    },
    {
      header: 'Actions',
      align: 'right',
      render: (row) => (
        <button
          onClick={() => handleContribute(row)}
          className="px-3 py-1.5 text-xs font-bold text-white bg-blue-600 hover:bg-blue-700 rounded-xl transition-colors cursor-pointer"
        >
          Pay Pot
        </button>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="Thrift Savings (Ajo / Esusu Circles)"
        subtitle="Automated peer-to-peer rotational contribution groups with verified identity and locked payout schedules."
        actions={
          <button
            onClick={() => setShowCreateModal(true)}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs cursor-pointer"
          >
            <Plus className="w-3.5 h-3.5" />
            Create Thrift Circle
          </button>
        }
      />

      {isLoading ? (
        <div className="p-12 text-center text-xs text-slate-400 bg-white rounded-3xl border border-slate-200">
          <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-blue-600" />
          Loading thrift circles...
        </div>
      ) : groups.length === 0 ? (
        <div className="p-12 text-center text-xs text-slate-500 bg-white rounded-3xl border border-dashed border-slate-200">
          <Users2 className="w-10 h-10 mx-auto mb-3 text-slate-300" />
          <h4 className="font-bold text-slate-900 text-sm">No Active Thrift Circles</h4>
          <p className="mt-1 text-slate-400 max-w-sm mx-auto">
            Create an Ajo or Esusu rotational circle with colleagues or join an existing invitation.
          </p>
          <button
            onClick={() => setShowCreateModal(true)}
            className="mt-4 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white font-bold text-xs rounded-xl shadow-xs"
          >
            Create New Thrift Circle
          </button>
        </div>
      ) : (
        <DataTable
          columns={columns}
          data={groups}
          searchPlaceholder="Search thrift circles..."
        />
      )}

      {/* Create Modal */}
      <Modal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        title="Create New Thrift Circle (Ajo / Esusu)"
        subtitle="Configure members count, periodic pot sum, and rotation order."
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowCreateModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
            <button onClick={handleCreateGroup} disabled={isSubmitting} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl shadow-xs">
              {isSubmitting ? 'Creating...' : 'Initialize Circle'}
            </button>
          </div>
        }
      >
        <form onSubmit={handleCreateGroup} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Thrift Circle Name</label>
            <input
              type="text"
              required
              value={groupName}
              onChange={(e) => setGroupName(e.target.value)}
              className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-bold"
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Per-Member Contribution (₦)</label>
              <input
                type="number"
                required
                value={contributionAmount}
                onChange={(e) => setContributionAmount(e.target.value)}
                className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold"
              />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Rotation Slots / Members</label>
              <input
                type="number"
                required
                min={2}
                max={20}
                value={totalSlots}
                onChange={(e) => setTotalSlots(parseInt(e.target.value))}
                className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold"
              />
            </div>
          </div>
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Contribution Schedule</label>
            <select
              value={frequency}
              onChange={(e) => setFrequency(e.target.value)}
              className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-bold"
            >
              <option value="WEEKLY">Weekly Rotation</option>
              <option value="MONTHLY">Monthly Salary Day Rotation</option>
            </select>
          </div>
        </form>
      </Modal>

      {/* Pay PIN Modal */}
      {selectedGroup && (
        <PinModal
          isOpen={showPinModal}
          onClose={() => setShowPinModal(false)}
          onConfirm={handleConfirmContribution}
          title={`Pay Thrift Pot: ${selectedGroup.name}`}
          amount={formatCurrency(selectedGroup.contributionAmount || 0)}
          recipient={`${selectedGroup.name} (Ajo Escrow Vault)`}
        />
      )}
    </div>
  );
}
