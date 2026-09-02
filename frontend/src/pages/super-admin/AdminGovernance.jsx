import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import { formatDate } from '../../utils/formatters';
import { Users2, UserPlus, Shield, Check, X, Lock } from 'lucide-react';

export default function AdminGovernance() {
  const [showInviteModal, setShowInviteModal] = useState(false);
  const [showPermissionModal, setShowPermissionModal] = useState(false);
  const [selectedAdmin, setSelectedAdmin] = useState(null);
  const { showSuccess } = useToast();

  const [inviteName, setInviteName] = useState('');
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviteRole, setInviteRole] = useState('Admin');

  const [admins, setAdmins] = useState([
    {
      id: 'adm-01',
      name: 'Honour Ajani',
      email: 'honour@gmail.com',
      role: 'SuperAdmin',
      isActive: true,
      permissions: ['AuditView', 'UserManage', 'KycVerify', 'KybVerify', 'FeeManage', 'ReconciliationManage'],
      createdAt: '2026-07-01T00:00:00Z'
    },
    {
      id: 'adm-02',
      name: 'Dr. Chidi Nwosu',
      email: 'chidi.compliance@cebizpay.com',
      role: 'Admin',
      isActive: true,
      permissions: ['KycVerify', 'KybVerify', 'AuditView'],
      createdAt: '2026-08-10T09:00:00Z'
    },
    {
      id: 'adm-03',
      name: 'Fatima Bello',
      email: 'fatima.auditor@cebizpay.com',
      role: 'Auditor',
      isActive: true,
      permissions: ['AuditView'],
      createdAt: '2026-08-15T14:30:00Z'
    }
  ]);

  const allAvailablePermissions = [
    { code: 'KycVerify', name: 'Verify Individual KYC Documents' },
    { code: 'KybVerify', name: 'Verify Corporate KYB Submissions' },
    { code: 'FeeManage', name: 'Alter Platform Fee Policies' },
    { code: 'ReconciliationManage', name: 'Execute Financial Reconciliation' },
    { code: 'AuditView', name: 'Read-Only Audit Trail Inspection' },
    { code: 'UserManage', name: 'Suspend / Reactivate User Profiles' }
  ];

  const handleToggleStatus = (adminId) => {
    setAdmins((prev) =>
      prev.map((a) => (a.id === adminId ? { ...a, isActive: !a.isActive } : a))
    );
    showSuccess('Admin Status Toggled', 'Administrative account status updated.');
  };

  const handleInvite = (e) => {
    e.preventDefault();
    const newAdmin = {
      id: `adm-${Date.now()}`,
      name: inviteName,
      email: inviteEmail,
      role: inviteRole,
      isActive: false, // Dispatched 24-hr token
      permissions: inviteRole === 'Auditor' ? ['AuditView'] : ['KycVerify', 'AuditView'],
      createdAt: new Date().toISOString()
    };
    setAdmins((prev) => [newAdmin, ...prev]);
    showSuccess('Invitation Dispatched', `24-hour invitation token sent to ${inviteEmail}.`);
    setShowInviteModal(false);
    setInviteName('');
    setInviteEmail('');
  };

  const handleTogglePermission = (permCode) => {
    if (!selectedAdmin) return;
    const exists = selectedAdmin.permissions.includes(permCode);
    const updated = exists
      ? selectedAdmin.permissions.filter((p) => p !== permCode)
      : [...selectedAdmin.permissions, permCode];

    setSelectedAdmin({ ...selectedAdmin, permissions: updated });
    setAdmins((prev) =>
      prev.map((a) => (a.id === selectedAdmin.id ? { ...a, permissions: updated } : a))
    );
    showSuccess('Permission Updated', `${permCode} ${exists ? 'revoked' : 'granted'}.`);
  };

  const columns = [
    {
      header: 'Admin Name',
      accessor: 'name',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.name}</span>
          <span className="text-[11px] text-slate-400">{row.email}</span>
        </div>
      )
    },
    {
      header: 'System Role',
      accessor: 'role',
      render: (row) => (
        <Badge
          status={row.role === 'SuperAdmin' ? 'VERIFIED' : row.role === 'Admin' ? 'TIER_2' : 'DRAFT'}
          label={row.role}
          size="sm"
        />
      )
    },
    {
      header: 'Delegated Permissions',
      accessor: 'permissions',
      render: (row) => (
        <div className="flex flex-wrap gap-1 max-w-xs">
          {row.permissions.map((p) => (
            <span key={p} className="px-1.5 py-0.5 rounded bg-slate-100 text-slate-700 text-[10px] font-mono">
              {p}
            </span>
          ))}
        </div>
      )
    },
    {
      header: 'Account Status',
      accessor: 'isActive',
      render: (row) => (
        <Badge
          status={row.isActive ? 'ACTIVE' : 'SUSPENDED'}
          label={row.isActive ? 'Active (ON)' : 'Pending / Off'}
          size="sm"
        />
      )
    },
    {
      header: 'Actions',
      align: 'right',
      render: (row) => (
        <div className="flex items-center justify-end gap-2">
          {row.role !== 'SuperAdmin' && (
            <>
              <button
                onClick={() => {
                  setSelectedAdmin(row);
                  setShowPermissionModal(true);
                }}
                className="px-2.5 py-1 text-xs font-bold text-blue-700 bg-blue-50 hover:bg-blue-100 rounded-lg transition-colors"
              >
                Permissions
              </button>
              <button
                onClick={() => handleToggleStatus(row.id)}
                className={`px-2.5 py-1 text-xs font-bold rounded-lg transition-colors ${
                  row.isActive
                    ? 'text-rose-700 bg-rose-50 hover:bg-rose-100'
                    : 'text-emerald-700 bg-emerald-50 hover:bg-emerald-100'
                }`}
              >
                {row.isActive ? 'Deactivate' : 'Activate'}
              </button>
            </>
          )}
        </div>
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title="Admin Governance &amp; Delegated Access (RBAC)"
        subtitle="Manage platform operator profiles, invite operational staff, and configure granular delegated permissions."
        actions={
          <button
            onClick={() => setShowInviteModal(true)}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
          >
            <UserPlus className="w-3.5 h-3.5" />
            Invite New Admin
          </button>
        }
      />

      <DataTable
        columns={columns}
        data={admins}
        searchPlaceholder="Search admin users..."
      />

      {/* Invite Admin Modal */}
      <Modal
        isOpen={showInviteModal}
        onClose={() => setShowInviteModal(false)}
        title="Invite Administrative Operator"
        subtitle="Dispatches a secure 24-hour invitation token to the recipient email."
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button
              onClick={() => setShowInviteModal(false)}
              className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl"
            >
              Cancel
            </button>
            <button
              onClick={handleInvite}
              className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl"
            >
              Send Invitation
            </button>
          </div>
        }
      >
        <form onSubmit={handleInvite} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1.5">Full Name</label>
            <input
              type="text"
              required
              value={inviteName}
              onChange={(e) => setInviteName(e.target.value)}
              placeholder="e.g. Babajide Williams"
              className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl outline-hidden"
            />
          </div>
          <div>
            <label className="block font-semibold text-slate-700 mb-1.5">Official Email Address</label>
            <input
              type="email"
              required
              value={inviteEmail}
              onChange={(e) => setInviteEmail(e.target.value)}
              placeholder="b.williams@cebizpay.com"
              className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl outline-hidden"
            />
          </div>
          <div>
            <label className="block font-semibold text-slate-700 mb-1.5">Administrative Role</label>
            <select
              value={inviteRole}
              onChange={(e) => setInviteRole(e.target.value)}
              className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-bold"
            >
              <option value="Admin">Admin (Operational Reviewer)</option>
              <option value="Auditor">Auditor (Read-Only Log Inspector)</option>
            </select>
          </div>
        </form>
      </Modal>

      {/* Permissions Modal */}
      {selectedAdmin && (
        <Modal
          isOpen={showPermissionModal}
          onClose={() => setShowPermissionModal(false)}
          title={`Configure Permissions: ${selectedAdmin.name}`}
          subtitle="Toggle delegated permissions granted by Super Admin."
          footer={
            <button
              onClick={() => setShowPermissionModal(false)}
              className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl"
            >
              Done
            </button>
          }
        >
          <div className="space-y-2 text-xs text-left">
            {allAvailablePermissions.map((p) => {
              const hasPerm = selectedAdmin.permissions.includes(p.code);
              return (
                <div
                  key={p.code}
                  className="flex items-center justify-between p-3 rounded-xl border border-slate-200 hover:bg-slate-50 transition-colors"
                >
                  <div>
                    <span className="font-bold text-slate-900 block">{p.name}</span>
                    <span className="font-mono text-[10px] text-slate-400">{p.code}</span>
                  </div>
                  <button
                    onClick={() => handleTogglePermission(p.code)}
                    className={`px-3 py-1 rounded-lg font-bold text-xs transition-colors ${
                      hasPerm
                        ? 'bg-emerald-100 text-emerald-800'
                        : 'bg-slate-100 text-slate-500 hover:bg-slate-200'
                    }`}
                  >
                    {hasPerm ? 'Enabled' : 'Disabled'}
                  </button>
                </div>
              );
            })}
          </div>
        </Modal>
      )}
    </div>
  );
}
