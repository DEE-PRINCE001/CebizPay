import React, { useState } from 'react';
import AdminLayout from '../../layouts/AdminLayout';
import InviteAdminModal from '../../components/admin/InviteAdminModal';

import Table from '../../components/tables/Table';
import TableHeader from '../../components/tables/TableHeader';
import TableRow from '../../components/tables/TableRow';
import Pagination from '../../components/tables/Pagination';
import TableExport from '../../components/tables/TableExport';
import Card from '../../components/common/Card';
import Badge from '../../components/common/Badge';
import StatCard from '../../components/common/StatCard';
import Button from '../../components/common/Button';
import SearchInput from '../../components/forms/SearchInput';
import Select from '../../components/forms/Select';
import Skeleton from '../../components/common/Skeleton';
import EmptyState from '../../components/feedback/EmptyState';
import ErrorState from '../../components/feedback/ErrorState';
import ConfirmModal from '../../components/feedback/ConfirmModal';

import { Users, UserPlus, Shield, Trash2, CheckCircle2, RefreshCw } from 'lucide-react';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useToast } from '../../hooks/useToast';
import apiClient from '../../services/api/client';
import { parseProblemDetails } from '../../utils/problemDetails';

const ROLE_FILTERS = [
  { value: '', label: 'All Admin Roles' },
  { value: '1', label: 'SuperAdmin' },
  { value: '2', label: 'Admin' },
  { value: '3', label: 'Compliance Officer' },
  { value: '4', label: 'Auditor' },
  { value: '5', label: 'Support Agent' }
];

/**
 * SuperAdmin platform staff and administrative permissions directory.
 */
export default function AdminUsersPage() {
  const { showSuccess, showError } = useToast();

  const [search, setSearch] = useState('');
  const [role, setRole] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 20;

  const [isInviteOpen, setIsInviteOpen] = useState(false);
  const [deletingAdmin, setDeletingAdmin] = useState(null);
  const [deleteLoading, setDeleteLoading] = useState(false);

  const {
    data: adminData,
    loading,
    error,
    refetch
  } = useApiQuery(
    () => apiClient.get('/admin/manage', {
      params: {
        search: search.trim() || undefined,
        role: role ? parseInt(role, 10) : undefined,
        pageNumber: currentPage,
        pageSize
      }
    }),
    { deps: [search, role, currentPage] }
  );

  const admins = adminData?.items || [];
  const totalPages = adminData?.totalPages || 1;
  const totalCount = adminData?.totalCount || admins.length;

  const handleToggleStatus = async (adminProfileId, currentStatus) => {
    try {
      await apiClient.patch('/admin/manage/toggle-status', {
        adminProfileId,
        isActive: !currentStatus
      });
      showSuccess(`Administrative user status updated.`);
      refetch();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      showError(parsed.message || 'Failed to update admin status.');
    }
  };

  const handleDelete = async () => {
    if (!deletingAdmin) return;
    setDeleteLoading(true);
    try {
      await apiClient.delete(`/admin/manage/${deletingAdmin.id}`);
      showSuccess(`Administrative profile archived.`);
      setDeletingAdmin(null);
      refetch();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      showError(parsed.message || 'Could not delete administrator.');
    } finally {
      setDeleteLoading(false);
    }
  };

  const getRoleBadge = (r) => {
    const roleStr = typeof r === 'string' ? r : r === 1 ? 'SuperAdmin' : r === 2 ? 'Admin' : r === 3 ? 'Compliance' : 'Auditor';
    if (roleStr === 'SuperAdmin') return <Badge variant="brand">SuperAdmin</Badge>;
    if (roleStr === 'Admin') return <Badge variant="neutral">Admin</Badge>;
    if (roleStr === 'Compliance') return <Badge variant="warning">Compliance</Badge>;
    return <Badge variant="neutral">{roleStr}</Badge>;
  };

  return (
    <AdminLayout
      title="Platform Administrative Directory"
      subtitle="Staff role assignments, access levels, and security privileges"
      headerAction={
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            icon={RefreshCw}
            onClick={refetch}
            className="hidden sm:inline-flex"
          >
            Refresh
          </Button>
          <Button
            variant="primary"
            size="sm"
            icon={UserPlus}
            onClick={() => setIsInviteOpen(true)}
          >
            Invite Admin
          </Button>
        </div>
      }
    >
      <div className="space-y-6">
        {/* Metric Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <StatCard
            icon={Users}
            label="Total Platform Staff"
            value={totalCount.toString()}
            loading={loading}
          />
          <StatCard
            icon={Shield}
            label="Super Administrators"
            value={admins.filter((a) => a.role === 'SuperAdmin' || a.role === 1).length.toString()}
            loading={loading}
          />
          <StatCard
            icon={CheckCircle2}
            label="Active Profiles"
            value={admins.filter((a) => a.isActive).length.toString()}
            loading={loading}
          />
        </div>

        {/* Search & Filter */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <SearchInput
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onClear={() => setSearch('')}
            placeholder="Search by email..."
            className="w-full sm:max-w-xs"
          />

          <div className="flex items-center gap-2">
            <div className="w-44">
              <Select
                options={ROLE_FILTERS}
                value={role}
                onChange={(e) => {
                  setRole(e.target.value);
                  setCurrentPage(1);
                }}
              />
            </div>

            <TableExport
              label="Export"
              onExportCsv={() => {
                const csvContent =
                  'data:text/csv;charset=utf-8,' +
                  ['Email,Role,IsActive,CreatedAt']
                    .concat(
                      admins.map(
                        (a) =>
                          `"${a.email || ''}","${a.role || ''}",${a.isActive ? 'Active' : 'Inactive'},"${a.createdAtUtc || ''}"`
                      )
                    )
                    .join('\n');
                const encodedUri = encodeURI(csvContent);
                const link = document.createElement('a');
                link.setAttribute('href', encodedUri);
                link.setAttribute('download', `admin_users_${new Date().toISOString().slice(0, 10)}.csv`);
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
              }}
            />
          </div>
        </div>

        {/* Directory Table */}
        <Card padding="p-0" className="overflow-hidden">
          {loading && (
            <div className="p-6 space-y-3">
              <Skeleton variant="table-row" count={5} />
            </div>
          )}

          {!loading && error && (
            <div className="p-6">
              <ErrorState
                title="Failed to load administrative users"
                message={error.message || 'Unable to retrieve admin directory.'}
                onRetry={refetch}
              />
            </div>
          )}

          {!loading && !error && admins.length === 0 && (
            <div className="p-8">
              <EmptyState
                icon={Users}
                title="No administrative users found"
                description="Invite staff members with assigned SuperAdmin, Compliance, or Auditor roles."
                actionLabel="Invite Admin"
                onAction={() => setIsInviteOpen(true)}
              />
            </div>
          )}

          {!loading && !error && admins.length > 0 && (
            <>
              <Table>
                <TableHeader
                  columns={[
                    { label: 'Admin User' },
                    { label: 'Role & Privilege' },
                    { label: 'Account State' },
                    { label: 'Actions', align: 'right' }
                  ]}
                />
                <tbody>
                  {admins.map((adm) => (
                    <TableRow key={adm.id}>
                      <td className="py-3 px-4">
                        <div className="flex items-center gap-2.5">
                          <div className="w-8 h-8 rounded-full bg-slate-900 text-white flex items-center justify-center font-bold text-xs shrink-0">
                            {(adm.email || 'A')[0].toUpperCase()}
                          </div>
                          <div>
                            <div className="font-bold text-xs text-slate-900">{adm.email}</div>
                            <div className="text-[10px] text-slate-400 font-mono">ID: {adm.id?.slice(0, 8)}...</div>
                          </div>
                        </div>
                      </td>

                      <td className="py-3 px-4">
                        {getRoleBadge(adm.role)}
                      </td>

                      <td className="py-3 px-4">
                        <button
                          type="button"
                          onClick={() => handleToggleStatus(adm.id, adm.isActive)}
                          className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-hidden ${
                            adm.isActive ? 'bg-brand-600' : 'bg-slate-200'
                          }`}
                        >
                          <span
                            className={`pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow-sm ring-0 transition duration-200 ease-in-out ${
                              adm.isActive ? 'translate-x-4' : 'translate-x-0'
                            }`}
                          />
                        </button>
                      </td>

                      <td className="py-3 px-4 text-right">
                        <button
                          type="button"
                          onClick={() => setDeletingAdmin(adm)}
                          className="p-1.5 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition"
                          title="Archive Profile"
                        >
                          <Trash2 size={14} />
                        </button>
                      </td>
                    </TableRow>
                  ))}
                </tbody>
              </Table>

              {totalPages > 1 && (
                <div className="p-4 border-t border-slate-100">
                  <Pagination
                    currentPage={currentPage}
                    totalPages={totalPages}
                    onPageChange={(p) => setCurrentPage(p)}
                    hasNextPage={currentPage < totalPages}
                    hasPrevPage={currentPage > 1}
                  />
                </div>
              )}
            </>
          )}
        </Card>
      </div>

      <InviteAdminModal
        isOpen={isInviteOpen}
        onClose={() => setIsInviteOpen(false)}
        onSuccess={refetch}
      />

      {deletingAdmin && (
        <ConfirmModal
          isOpen={true}
          onClose={() => setDeletingAdmin(null)}
          onConfirm={handleDelete}
          title="Archive Administrative Account"
          message={`Are you sure you want to revoke access and archive the administrative profile for "${deletingAdmin.email}"?`}
          confirmText="Archive Admin"
          confirmVariant="danger"
          loading={deleteLoading}
        />
      )}
    </AdminLayout>
  );
}
