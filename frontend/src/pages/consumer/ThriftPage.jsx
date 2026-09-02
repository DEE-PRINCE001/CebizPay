import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Tabs from '../../components/common/Tabs';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatDate } from '../../utils/formatters';
import { Users2, Plus, Lock, KeyRound, CheckCircle2, ArrowRight, UserPlus, ShieldCheck } from 'lucide-react';

export default function ThriftPage() {
  const [activeTab, setActiveTab] = useState('my-groups'); // 'my-groups' | 'join'
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showInviteModal, setShowInviteModal] = useState(false);
  const [showDetailsModal, setShowDetailsModal] = useState(false);
  const [selectedGroup, setSelectedGroup] = useState(null);

  const [groupTitle, setGroupTitle] = useState('');
  const [contributionAmount, setContributionAmount] = useState('100000');
  const [slotsCount, setSlotsCount] = useState(6);
  const [cycleFrequency, setCycleFrequency] = useState('MONTHLY');
  const [joinCode, setJoinCode] = useState('');
  const [inviteEmail, setInviteEmail] = useState('');

  const { showSuccess, showError } = useToast();

  // Active Thrift groups
  const [groups, setGroups] = useState([
    {
      id: 'THRIFT-GRP-01',
      title: 'Apex Tech Senior Engineers Ajo Pool',
      contributionAmount: 100000.0,
      totalPoolPerCycle: 600000.0,
      slotsCount: 6,
      currentCycle: 3,
      myPosition: 3, // Payout this month!
      cycleFrequency: 'MONTHLY',
      invitationCode: 'AJO-APEX-8849',
      isLocked: true,
      status: 'ACTIVE',
      members: [
        { name: 'Amina Adeleke (You)', position: 3, isPaidCurrentCycle: true },
        { name: 'Babatunde Fashola', position: 1, isPaidCurrentCycle: true },
        { name: 'Chidinma Eze', position: 2, isPaidCurrentCycle: true },
        { name: 'Emeka Nwosu', position: 4, isPaidCurrentCycle: false },
        { name: 'Tariq Alabi', position: 5, isPaidCurrentCycle: false },
        { name: 'Zainab Ahmed', position: 6, isPaidCurrentCycle: false }
      ]
    }
  ]);

  const handleCreateGroup = (e) => {
    e.preventDefault();
    const cont = parseFloat(contributionAmount);
    const slots = parseInt(slotsCount);
    const newG = {
      id: `THRIFT-GRP-${Date.now().toString().slice(-4)}`,
      title: groupTitle,
      contributionAmount: cont,
      totalPoolPerCycle: cont * slots,
      slotsCount: slots,
      currentCycle: 1,
      myPosition: 1,
      cycleFrequency,
      invitationCode: `AJO-${Math.random().toString(36).substring(2, 8).toUpperCase()}`,
      isLocked: false,
      status: 'PENDING',
      members: [{ name: 'Amina Adeleke (You)', position: 1, isPaidCurrentCycle: false }]
    };
    setGroups((prev) => [newG, ...prev]);
    showSuccess('Thrift Group Created', `${groupTitle} formed. Share code ${newG.invitationCode} to invite members.`);
    setShowCreateModal(false);
    setGroupTitle('');
  };

  const handleJoinWithCode = (e) => {
    e.preventDefault();
    if (!joinCode) return;
    showSuccess('Joined Thrift Group', `You have been added to rotation pool (${joinCode}).`);
    setJoinCode('');
  };

  const handleLockPositions = (group) => {
    setGroups((prev) =>
      prev.map((g) => (g.id === group.id ? { ...g, isLocked: true, status: 'ACTIVE' } : g))
    );
    showSuccess('Rotation Positions Locked', 'The rotational disbursement schedule is now active.');
  };

  const columns = [
    {
      header: 'Thrift Group (Ajo / Esusu)',
      accessor: 'title',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.title}</span>
          <span className="text-[11px] text-slate-400 font-mono">Code: {row.invitationCode}</span>
        </div>
      )
    },
    {
      header: 'Contribution',
      accessor: 'contributionAmount',
      render: (row) => <span className="font-mono font-bold text-slate-900">{formatCurrency(row.contributionAmount)}/mo</span>
    },
    {
      header: 'Lump-Sum Payout Pool',
      accessor: 'totalPoolPerCycle',
      render: (row) => <span className="font-mono font-bold text-emerald-700">{formatCurrency(row.totalPoolPerCycle)}</span>
    },
    {
      header: 'My Position',
      accessor: 'myPosition',
      render: (row) => (
        <span className="font-bold text-blue-700 bg-blue-50 px-2.5 py-1 rounded-xl text-xs">
          Position #{row.myPosition} {row.myPosition === row.currentCycle ? '🎯 (Current Payout)' : ''}
        </span>
      )
    },
    {
      header: 'Cycle Progress',
      accessor: 'currentCycle',
      render: (row) => <span className="text-slate-700 font-semibold text-xs">Cycle {row.currentCycle} of {row.slotsCount}</span>
    },
    {
      header: 'Status',
      accessor: 'status',
      render: (row) => <Badge status={row.isLocked ? 'ACTIVE' : 'DRAFT'} label={row.isLocked ? 'Locked & Active' : 'Forming'} size="sm" />
    },
    {
      header: 'Actions',
      align: 'right',
      render: (row) => (
        <div className="flex items-center justify-end gap-1.5">
          <button
            onClick={() => {
              setSelectedGroup(row);
              setShowDetailsModal(true);
            }}
            className="px-3 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-800 rounded-lg text-xs font-bold transition-colors"
          >
            Members &amp; Roster
          </button>
        </div>
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title="Thrift (Ajo / Esusu) Rotational Groups"
        subtitle="Peer rotational savings pools with automated wallet debits, position locking, and lump-sum rotational payouts."
        actions={
          <div className="flex items-center gap-2">
            <button
              onClick={() => setShowCreateModal(true)}
              className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
            >
              <Plus className="w-3.5 h-3.5" />
              Create Ajo Group
            </button>
          </div>
        }
      />

      <Tabs
        tabs={[
          { id: 'my-groups', label: 'My Active Ajo Pools', count: groups.length, icon: Users2 },
          { id: 'join', label: 'Join with Invitation Code', icon: KeyRound }
        ]}
        activeTab={activeTab}
        onChange={setActiveTab}
        className="mb-6"
      />

      {activeTab === 'my-groups' && (
        <DataTable
          columns={columns}
          data={groups}
          searchPlaceholder="Search thrift groups..."
        />
      )}

      {activeTab === 'join' && (
        <div className="bg-white rounded-3xl border border-slate-200/80 p-6 sm:p-8 max-w-lg mx-auto text-xs text-left shadow-xs">
          <h3 className="text-sm font-bold text-slate-900 mb-2">Join an Existing Rotational Group</h3>
          <p className="text-slate-500 mb-6">Enter the 6-digit alphanumeric group code provided by the group creator.</p>

          <form onSubmit={handleJoinWithCode} className="space-y-4">
            <div>
              <label className="block font-semibold text-slate-700 mb-1.5">Group Invitation Code</label>
              <input
                type="text"
                required
                value={joinCode}
                onChange={(e) => setJoinCode(e.target.value)}
                placeholder="e.g. AJO-APEX-8849"
                className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-mono text-base font-bold uppercase tracking-wider"
              />
            </div>
            <button
              type="submit"
              className="w-full py-3 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-xl shadow-xs"
            >
              Verify Code &amp; Join Roster
            </button>
          </form>
        </div>
      )}

      {/* Create Group Modal */}
      <Modal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        title="Create Peer Rotational Thrift (Ajo) Group"
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowCreateModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
            <button onClick={handleCreateGroup} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Create Group</button>
          </div>
        }
      >
        <form onSubmit={handleCreateGroup} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Group Title</label>
            <input type="text" required value={groupTitle} onChange={(e) => setGroupTitle(e.target.value)} placeholder="e.g. Engineering Leads 2026 Ajo" className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-bold" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Monthly Contribution (₦)</label>
              <input type="number" required value={contributionAmount} onChange={(e) => setContributionAmount(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold" />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Total Member Slots</label>
              <input type="number" required min={2} max={20} value={slotsCount} onChange={(e) => setSlotsCount(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold" />
            </div>
          </div>
          <div className="p-3 bg-emerald-50 rounded-xl border border-emerald-200 text-emerald-900 font-mono">
            Each cycle payout = ₦{(parseFloat(contributionAmount || 0) * parseInt(slotsCount || 0)).toLocaleString()} (Lump sum)
          </div>
        </form>
      </Modal>

      {/* Roster Details Modal */}
      {selectedGroup && (
        <Modal
          isOpen={showDetailsModal}
          onClose={() => setShowDetailsModal(false)}
          title={`Rotation Roster: ${selectedGroup.title}`}
          subtitle={`Invitation Code: ${selectedGroup.invitationCode}`}
          footer={
            <div className="flex items-center justify-between w-full">
              {!selectedGroup.isLocked && (
                <button
                  onClick={() => handleLockPositions(selectedGroup)}
                  className="px-4 py-2 text-xs font-bold text-white bg-blue-600 hover:bg-blue-700 rounded-xl flex items-center gap-1.5"
                >
                  <Lock className="w-3.5 h-3.5" /> Lock Positions &amp; Start
                </button>
              )}
              <button
                onClick={() => setShowDetailsModal(false)}
                className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl ml-auto"
              >
                Close
              </button>
            </div>
          }
        >
          <div className="space-y-3 text-xs text-left">
            <div className="border border-slate-200 rounded-xl overflow-hidden">
              <table className="w-full text-left">
                <thead className="bg-slate-50 text-slate-500 font-semibold border-b border-slate-200">
                  <tr>
                    <th className="p-2.5">Position</th>
                    <th className="p-2.5">Member Name</th>
                    <th className="p-2.5">Current Cycle Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {selectedGroup.members.map((m, idx) => (
                    <tr key={idx}>
                      <td className="p-2.5 font-bold font-mono text-blue-700">Slot #{m.position}</td>
                      <td className="p-2.5 font-bold text-slate-900">{m.name}</td>
                      <td className="p-2.5">
                        <Badge status={m.isPaidCurrentCycle ? 'VERIFIED' : 'PENDING'} label={m.isPaidCurrentCycle ? 'Contribution Paid' : 'Pending Debit'} size="sm" />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
}
