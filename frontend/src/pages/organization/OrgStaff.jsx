import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Modal from '../../components/common/Modal';
import ConfirmDialog from '../../components/common/ConfirmDialog';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatDate } from '../../utils/formatters';
import {
  UserCheck,
  UserPlus,
  Mail,
  Send,
  Sliders,
  AlertTriangle,
  CheckCircle,
  XCircle,
  Plus,
  Briefcase
} from 'lucide-react';

export default function OrgStaff() {
  const [showInviteModal, setShowInviteModal] = useState(false);
  const [showDirectCreateModal, setShowDirectCreateModal] = useState(false);
  const [showAssignModal, setShowAssignModal] = useState(false);
  const [showSuspendModal, setShowSuspendModal] = useState(false);
  const [showTerminateModal, setShowTerminateModal] = useState(false);
  const [selectedStaff, setSelectedStaff] = useState(null);

  const { showSuccess, showError } = useToast();

  // Invite states
  const [singleEmail, setSingleEmail] = useState('');
  const [bulkEmails, setBulkEmails] = useState('');

  // Direct create states
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [salary, setSalary] = useState('750000');

  // Suspension / Termination reason
  const [actionReason, setActionReason] = useState('');

  // Mock staff list
  const [staffList, setStaffList] = useState([
    {
      id: 'stf-01',
      fullName: 'Amina Adeleke',
      email: 'amina.adeleke@example.com',
      phone: '08012345678',
      department: 'Engineering',
      roleTitle: 'Senior Software Engineer',
      salaryLevel: 'L4 - Senior Lead',
      baseSalary: 1250000.0,
      membershipStatus: 'ACTIVE',
      joinedAt: '2026-03-15T09:00:00Z'
    },
    {
      id: 'stf-02',
      fullName: 'Babatunde Fashola',
      email: 'babatunde.f@apextech.com',
      phone: '08022334411',
      department: 'Product & Design',
      roleTitle: 'Lead Product Manager',
      salaryLevel: 'L4 - Senior Lead',
      baseSalary: 1150000.0,
      membershipStatus: 'ACTIVE',
      joinedAt: '2026-04-01T10:00:00Z'
    },
    {
      id: 'stf-03',
      fullName: 'Kazeem Oladipo',
      email: 'kazeem.o@apextech.com',
      phone: '08055667788',
      department: 'Finance & Accounting',
      roleTitle: 'Financial Analyst',
      salaryLevel: 'L2 - Mid Level',
      baseSalary: 650000.0,
      membershipStatus: 'SUSPENDED',
      joinedAt: '2026-05-10T11:30:00Z'
    },
    {
      id: 'stf-04',
      fullName: 'Chidinma Eze',
      email: 'chidinma.e@apextech.com',
      phone: '08099887766',
      department: 'Human Resources',
      roleTitle: 'People Ops Specialist',
      salaryLevel: 'L2 - Mid Level',
      baseSalary: 550000.0,
      membershipStatus: 'ACTIVE',
      joinedAt: '2026-06-01T08:45:00Z'
    }
  ]);

  const handleSingleInvite = (e) => {
    e.preventDefault();
    if (!singleEmail) return;
    showSuccess('Invitation Dispatched', `Unique joining code sent to ${singleEmail}.`);
    setShowInviteModal(false);
    setSingleEmail('');
  };

  const handleBulkInvite = (e) => {
    e.preventDefault();
    if (!bulkEmails) return;
    const count = bulkEmails.split(',').filter((x) => x.trim()).length;
    showSuccess('Bulk Invitations Dispatched', `${count} staff invitation links created and dispatched.`);
    setShowInviteModal(false);
    setBulkEmails('');
  };

  const handleDirectCreate = (e) => {
    e.preventDefault();
    const newStaff = {
      id: `stf-${Date.now()}`,
      fullName: `${firstName} ${lastName}`,
      email,
      phone: phoneNumber,
      department: 'Engineering',
      roleTitle: 'Software Engineer',
      salaryLevel: 'L3 - Specialist',
      baseSalary: parseFloat(salary),
      membershipStatus: 'ACTIVE',
      joinedAt: new Date().toISOString()
    };
    setStaffList((prev) => [newStaff, ...prev]);
    showSuccess('Staff Enrolled', `${newStaff.fullName} added to workforce roster.`);
    setShowDirectCreateModal(false);
    setFirstName('');
    setLastName('');
    setEmail('');
    setPhoneNumber('');
  };

  const handleSuspendStaff = () => {
    if (!actionReason) {
      showError('Reason Required', 'Mandatory audit rationale required to suspend staff membership.');
      return;
    }
    setStaffList((prev) =>
      prev.map((s) => (s.id === selectedStaff.id ? { ...s, membershipStatus: 'SUSPENDED' } : s))
    );
    showSuccess('Staff Suspended', `${selectedStaff.fullName} membership paused. Payroll excluded.`);
    setShowSuspendModal(false);
    setActionReason('');
  };

  const handleReactivateStaff = (staff) => {
    setStaffList((prev) =>
      prev.map((s) => (s.id === staff.id ? { ...s, membershipStatus: 'ACTIVE' } : s))
    );
    showSuccess('Staff Reactivated', `${staff.fullName} restored to active workforce.`);
  };

  const handleTerminateStaff = () => {
    if (!actionReason) {
      showError('Reason Required', 'Termination reason required for severance audit.');
      return;
    }
    setStaffList((prev) => prev.filter((s) => s.id !== selectedStaff.id));
    showSuccess(
      'Staff Terminated & Loan Converted',
      `${selectedStaff.fullName} terminated. Corporate loan contracts automatically converted to individual terms.`
    );
    setShowTerminateModal(false);
    setActionReason('');
  };

  const columns = [
    {
      header: 'Staff Member',
      accessor: 'fullName',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.fullName}</span>
          <span className="text-[11px] text-slate-400">{row.email}</span>
        </div>
      )
    },
    {
      header: 'Department',
      accessor: 'department',
      render: (row) => <span className="font-medium text-slate-700">{row.department}</span>
    },
    {
      header: 'Workforce Role & Level',
      accessor: 'roleTitle',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-800 text-xs block">{row.roleTitle}</span>
          <span className="text-[10px] text-slate-500">{row.salaryLevel}</span>
        </div>
      )
    },
    {
      header: 'Monthly Base Salary',
      accessor: 'baseSalary',
      render: (row) => <span className="font-mono font-bold text-slate-900">{formatCurrency(row.baseSalary)}</span>
    },
    {
      header: 'Status',
      accessor: 'membershipStatus',
      render: (row) => <Badge status={row.membershipStatus} size="sm" />
    },
    {
      header: 'Actions',
      align: 'right',
      render: (row) => (
        <div className="flex items-center justify-end gap-1.5">
          {row.membershipStatus === 'ACTIVE' ? (
            <button
              onClick={() => {
                setSelectedStaff(row);
                setShowSuspendModal(true);
              }}
              className="px-2.5 py-1 text-xs font-bold text-amber-700 bg-amber-50 hover:bg-amber-100 rounded-lg transition-colors"
            >
              Suspend
            </button>
          ) : (
            <button
              onClick={() => handleReactivateStaff(row)}
              className="px-2.5 py-1 text-xs font-bold text-emerald-700 bg-emerald-50 hover:bg-emerald-100 rounded-lg transition-colors"
            >
              Reactivate
            </button>
          )}
          <button
            onClick={() => {
              setSelectedStaff(row);
              setShowTerminateModal(true);
            }}
            className="px-2.5 py-1 text-xs font-bold text-rose-700 bg-rose-50 hover:bg-rose-100 rounded-lg transition-colors"
          >
            Terminate
          </button>
        </div>
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title="Workforce &amp; Staff Directory"
        subtitle="Manage employee memberships, workforce roles, compensation levels, and organizational hierarchy."
        actions={
          <div className="flex items-center gap-2">
            <button
              onClick={() => setShowInviteModal(true)}
              className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-slate-800 bg-white border border-slate-200 rounded-xl hover:bg-slate-50 shadow-xs"
            >
              <Mail className="w-3.5 h-3.5 text-blue-600" />
              Invite by Email
            </button>
            <button
              onClick={() => setShowDirectCreateModal(true)}
              className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
            >
              <Plus className="w-3.5 h-3.5" />
              Direct Enroll Staff
            </button>
          </div>
        }
      />

      <DataTable
        columns={columns}
        data={staffList}
        searchPlaceholder="Search staff by name, email, or department..."
      />

      {/* Invite Modal */}
      <Modal
        isOpen={showInviteModal}
        onClose={() => setShowInviteModal(false)}
        title="Invite Employees to Organization"
        subtitle="Invited staff receive an invitation token to join your corporate payroll and benefit schemes."
      >
        <div className="space-y-6 text-xs text-left">
          {/* Single Invite */}
          <form onSubmit={handleSingleInvite} className="space-y-3 p-4 bg-slate-50 rounded-2xl border border-slate-200">
            <span className="font-bold text-slate-900 block">Option A: Single Employee Email</span>
            <div className="flex gap-2">
              <input
                type="email"
                required
                value={singleEmail}
                onChange={(e) => setSingleEmail(e.target.value)}
                placeholder="colleague@company.com"
                className="flex-1 px-3.5 py-2 bg-white border border-slate-200 rounded-xl"
              />
              <button
                type="submit"
                className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-xl"
              >
                Send Invite
              </button>
            </div>
          </form>

          {/* Bulk Invite */}
          <form onSubmit={handleBulkInvite} className="space-y-3 p-4 bg-slate-50 rounded-2xl border border-slate-200">
            <span className="font-bold text-slate-900 block">Option B: Bulk Comma-Separated Emails</span>
            <textarea
              rows={3}
              required
              value={bulkEmails}
              onChange={(e) => setBulkEmails(e.target.value)}
              placeholder="e1@company.com, e2@company.com, e3@company.com..."
              className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl"
            />
            <button
              type="submit"
              className="w-full py-2 bg-slate-800 hover:bg-slate-900 text-white font-bold rounded-xl"
            >
              Dispatch Bulk Invitations
            </button>
          </form>
        </div>
      </Modal>

      {/* Direct Create Staff Modal */}
      <Modal
        isOpen={showDirectCreateModal}
        onClose={() => setShowDirectCreateModal(false)}
        title="Direct Enroll Staff Member"
        subtitle="Enrolls an employee immediately with defined department, role title, and base salary."
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button
              onClick={() => setShowDirectCreateModal(false)}
              className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl"
            >
              Cancel
            </button>
            <button
              onClick={handleDirectCreate}
              className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl"
            >
              Enroll Employee
            </button>
          </div>
        }
      >
        <form onSubmit={handleDirectCreate} className="space-y-4 text-xs text-left">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">First Name</label>
              <input
                type="text"
                required
                value={firstName}
                onChange={(e) => setFirstName(e.target.value)}
                placeholder="e.g. Oluwaseun"
                className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl"
              />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Last Name</label>
              <input
                type="text"
                required
                value={lastName}
                onChange={(e) => setLastName(e.target.value)}
                placeholder="e.g. Bakare"
                className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl"
              />
            </div>
          </div>

          <div>
            <label className="block font-semibold text-slate-700 mb-1">Email Address</label>
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="o.bakare@apextech.com"
              className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Phone Number</label>
              <input
                type="tel"
                required
                value={phoneNumber}
                onChange={(e) => setPhoneNumber(e.target.value)}
                placeholder="08033445566"
                className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl"
              />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Monthly Base Salary (₦)</label>
              <input
                type="number"
                required
                value={salary}
                onChange={(e) => setSalary(e.target.value)}
                className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold"
              />
            </div>
          </div>
        </form>
      </Modal>

      {/* Suspend Modal */}
      {selectedStaff && (
        <Modal
          isOpen={showSuspendModal}
          onClose={() => setShowSuspendModal(false)}
          title={`Suspend Membership: ${selectedStaff.fullName}`}
          subtitle="Suspended staff are excluded from payroll runs and cannot initiate salary advance loans."
          footer={
            <div className="flex items-center justify-end gap-3 w-full">
              <button
                onClick={() => setShowSuspendModal(false)}
                className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl"
              >
                Cancel
              </button>
              <button
                onClick={handleSuspendStaff}
                className="px-5 py-2 text-xs font-bold text-white bg-amber-600 rounded-xl"
              >
                Suspend Membership
              </button>
            </div>
          }
        >
          <div className="space-y-4 text-xs text-left">
            <div>
              <label className="block font-semibold text-slate-700 mb-1.5">Mandatory Audit Rationale</label>
              <textarea
                rows={3}
                required
                value={actionReason}
                onChange={(e) => setActionReason(e.target.value)}
                placeholder="State disciplinary or administrative reason for suspension..."
                className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl"
              />
            </div>
          </div>
        </Modal>
      )}

      {/* Terminate Modal */}
      {selectedStaff && (
        <Modal
          isOpen={showTerminateModal}
          onClose={() => setShowTerminateModal(false)}
          title={`Terminate Staff: ${selectedStaff.fullName}`}
          subtitle="Offboards employee. Active payroll loans will convert to standard individual repayment contracts."
          footer={
            <div className="flex items-center justify-end gap-3 w-full">
              <button
                onClick={() => setShowTerminateModal(false)}
                className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl"
              >
                Cancel
              </button>
              <button
                onClick={handleTerminateStaff}
                className="px-5 py-2 text-xs font-bold text-white bg-rose-600 rounded-xl"
              >
                Execute Termination
              </button>
            </div>
          }
        >
          <div className="space-y-4 text-xs text-left">
            <div className="p-3 bg-rose-50 rounded-xl border border-rose-200 text-rose-900">
              <strong>Offboarding Loan Conversion:</strong> Per section 4.7 of the PRD, terminating staff triggers automated conversion of corporate payroll deductions to direct debits.
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1.5">Termination Reason</label>
              <textarea
                rows={3}
                required
                value={actionReason}
                onChange={(e) => setActionReason(e.target.value)}
                placeholder="State contract conclusion, resignation, or termination details..."
                className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl"
              />
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
}
