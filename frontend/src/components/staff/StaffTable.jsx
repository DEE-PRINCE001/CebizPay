import React, { useState } from 'react';
import Card from '../common/Card';
import Table from '../tables/Table';
import TableHeader from '../tables/TableHeader';
import TableRow from '../tables/TableRow';
import Badge from '../common/Badge';
import Skeleton from '../common/Skeleton';
import EmptyState from '../feedback/EmptyState';
import ErrorState from '../feedback/ErrorState';
import Pagination from '../tables/Pagination';
import Button from '../common/Button';
import ConfirmModal from '../feedback/ConfirmModal';
import { Users, MoreHorizontal, UserCheck, UserX, UserMinus, Edit3, Mail } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Staff directory and workforce management table.
 */
export default function StaffTable({
  staff = [],
  loading = false,
  error = null,
  currentPage = 1,
  totalPages = 1,
  onPageChange,
  onRetry,
  onRefresh,
  onAssignWorkforce,
  onAddStaff,
  className = ''
}) {
  const { showSuccess, showError } = useToast();

  const [activeStaffForAction, setActiveStaffForAction] = useState(null);
  const [confirmAction, setConfirmAction] = useState(null); // 'suspend' | 'reactivate' | 'terminate'
  const [actionLoading, setActionLoading] = useState(false);

  const getStatusBadge = (status) => {
    const s = (status || '').toLowerCase();
    if (s === 'active' || s === 'verified') {
      return <Badge variant="success" dot={true}>Active</Badge>;
    }
    if (s === 'pending' || s === 'invited') {
      return <Badge variant="warning" dot={true}>Invited</Badge>;
    }
    if (s === 'suspended') {
      return <Badge variant="danger" dot={true}>Suspended</Badge>;
    }
    if (s === 'terminated') {
      return <Badge variant="neutral">Terminated</Badge>;
    }
    return <Badge variant="neutral">{status || 'Active'}</Badge>;
  };

  const handleConfirmSubmit = async () => {
    if (!activeStaffForAction || !confirmAction) return;

    setActionLoading(true);
    try {
      if (confirmAction === 'suspend') {
        await apiClient.patch(`/org/staff/${activeStaffForAction.id}/suspend`, {
          reason: 'Administrative suspension'
        });
        showSuccess('Staff member suspended.');
      } else if (confirmAction === 'reactivate') {
        await apiClient.patch(`/org/staff/${activeStaffForAction.id}/reactivate`);
        showSuccess('Staff member reactivated.');
      } else if (confirmAction === 'terminate') {
        await apiClient.post(`/org/staff/${activeStaffForAction.id}/terminate`, {
          reason: 'Workforce termination'
        });
        showSuccess('Staff membership terminated.');
      }

      setConfirmAction(null);
      setActiveStaffForAction(null);
      if (onRefresh) onRefresh();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      showError(parsed.message || `Failed to ${confirmAction} staff member.`);
    } finally {
      setActionLoading(false);
    }
  };

  return (
    <Card padding="p-0" className={`overflow-hidden ${className}`}>
      {loading && (
        <div className="p-6 space-y-3">
          <Skeleton variant="table-row" count={6} />
        </div>
      )}

      {!loading && error && (
        <div className="p-6">
          <ErrorState
            title="Failed to load staff roster"
            message={error.message || 'Unable to retrieve staff members.'}
            onRetry={onRetry}
          />
        </div>
      )}

      {!loading && !error && staff.length === 0 && (
        <div className="p-8">
          <EmptyState
            icon={Users}
            title="No staff members found"
            description="Add or invite employees to configure workforce roles and process payroll."
            actionLabel="Add Staff Member"
            onAction={onAddStaff}
          />
        </div>
      )}

      {!loading && !error && staff.length > 0 && (
        <>
          <Table>
            <TableHeader
              columns={[
                { label: 'Staff Member' },
                { label: 'Department' },
                { label: 'Workforce Role' },
                { label: 'Salary Level' },
                { label: 'Status' },
                { label: 'Actions', align: 'right' }
              ]}
            />
            <tbody>
              {staff.map((member) => {
                const fullName = member.fullName || `${member.firstName || ''} ${member.lastName || ''}`.trim() || member.email;
                const initials = fullName
                  .split(' ')
                  .filter(Boolean)
                  .slice(0, 2)
                  .map((n) => n[0].toUpperCase())
                  .join('') || 'U';

                return (
                  <TableRow key={member.id}>
                    <td className="py-3.5 px-4">
                      <div className="flex items-center gap-2.5">
                        <div className="w-8 h-8 rounded-full bg-slate-900 text-white flex items-center justify-center font-bold text-xs shrink-0">
                          {initials}
                        </div>
                        <div className="min-w-0">
                          <div className="font-bold text-xs text-slate-900 truncate">{fullName}</div>
                          <div className="text-[11px] text-slate-400 truncate">{member.email}</div>
                        </div>
                      </div>
                    </td>

                    <td className="py-3.5 px-4 text-xs font-medium text-slate-700">
                      {member.departmentName || member.department || 'Unassigned'}
                    </td>

                    <td className="py-3.5 px-4 text-xs font-medium text-slate-700">
                      {member.workforceRoleTitle || member.roleTitle || member.role || 'General Staff'}
                    </td>

                    <td className="py-3.5 px-4 text-xs font-mono font-semibold text-slate-800">
                      {member.salaryLevelName || (member.baseSalary ? `₦${member.baseSalary.toLocaleString()}` : 'Default')}
                    </td>

                    <td className="py-3.5 px-4">
                      {getStatusBadge(member.status)}
                    </td>

                    <td className="py-3.5 px-4 text-right">
                      <div className="flex items-center justify-end gap-1.5">
                        <button
                          type="button"
                          onClick={() => onAssignWorkforce && onAssignWorkforce(member)}
                          className="px-2.5 py-1 text-xs font-semibold text-brand-600 hover:bg-brand-50 rounded-lg transition"
                          title="Assign Department & Role"
                        >
                          Assign
                        </button>

                        {member.status !== 'Suspended' && member.status !== 'Terminated' && (
                          <button
                            type="button"
                            onClick={() => {
                              setActiveStaffForAction(member);
                              setConfirmAction('suspend');
                            }}
                            className="p-1 text-slate-400 hover:text-amber-600 rounded-lg hover:bg-slate-100 transition"
                            title="Suspend"
                          >
                            <UserMinus size={15} />
                          </button>
                        )}

                        {member.status === 'Suspended' && (
                          <button
                            type="button"
                            onClick={() => {
                              setActiveStaffForAction(member);
                              setConfirmAction('reactivate');
                            }}
                            className="p-1 text-slate-400 hover:text-status-success rounded-lg hover:bg-slate-100 transition"
                            title="Reactivate"
                          >
                            <UserCheck size={15} />
                          </button>
                        )}

                        {member.status !== 'Terminated' && (
                          <button
                            type="button"
                            onClick={() => {
                              setActiveStaffForAction(member);
                              setConfirmAction('terminate');
                            }}
                            className="p-1 text-slate-400 hover:text-red-600 rounded-lg hover:bg-slate-100 transition"
                            title="Terminate"
                          >
                            <UserX size={15} />
                          </button>
                        )}
                      </div>
                    </td>
                  </TableRow>
                );
              })}
            </tbody>
          </Table>

          {totalPages > 1 && (
            <div className="p-4 border-t border-slate-100">
              <Pagination
                currentPage={currentPage}
                totalPages={totalPages}
                onPageChange={onPageChange}
                hasNextPage={currentPage < totalPages}
                hasPrevPage={currentPage > 1}
              />
            </div>
          )}
        </>
      )}

      {/* Confirmation Dialog */}
      {confirmAction && activeStaffForAction && (
        <ConfirmModal
          isOpen={true}
          onClose={() => {
            setConfirmAction(null);
            setActiveStaffForAction(null);
          }}
          onConfirm={handleConfirmSubmit}
          title={
            confirmAction === 'suspend'
              ? 'Suspend Staff Member'
              : confirmAction === 'reactivate'
              ? 'Reactivate Staff Member'
              : 'Terminate Staff Membership'
          }
          message={
            confirmAction === 'suspend'
              ? `Are you sure you want to suspend ${activeStaffForAction.fullName || activeStaffForAction.email}? They will not be eligible for payroll runs.`
              : confirmAction === 'reactivate'
              ? `Reactivate ${activeStaffForAction.fullName || activeStaffForAction.email} and restore payroll eligibility?`
              : `Are you sure you want to terminate ${activeStaffForAction.fullName || activeStaffForAction.email}? Any active corporate payroll loans will be converted.`
          }
          confirmText={
            confirmAction === 'suspend'
              ? 'Suspend'
              : confirmAction === 'reactivate'
              ? 'Reactivate'
              : 'Terminate'
          }
          confirmVariant={confirmAction === 'reactivate' ? 'primary' : 'danger'}
          loading={actionLoading}
        />
      )}
    </Card>
  );
}
