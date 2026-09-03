import React, { useState } from 'react';
import CustomerLayout from '../../layouts/CustomerLayout';
import StaffTable from '../../components/staff/StaffTable';
import AddStaffModal from '../../components/staff/AddStaffModal';
import DepartmentsModal from '../../components/staff/DepartmentsModal';
import WorkforceRolesModal from '../../components/staff/WorkforceRolesModal';
import AssignWorkforceModal from '../../components/staff/AssignWorkforceModal';

import SearchInput from '../../components/forms/SearchInput';
import Select from '../../components/forms/Select';
import TableFilter from '../../components/tables/TableFilter';
import TableExport from '../../components/tables/TableExport';
import Button from '../../components/common/Button';
import StatCard from '../../components/common/StatCard';

import { Users, UserPlus, Building2, Briefcase, RefreshCw } from 'lucide-react';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useOrg } from '../../context/OrgContext';
import apiClient from '../../services/api/client';

/**
 * Workforce directory, department assignments, and staff lifecycle operations.
 */
export default function StaffPage() {
  const { currentOrgId } = useOrg();

  // Search and Filter state
  const [search, setSearch] = useState('');
  const [departmentId, setDepartmentId] = useState('');
  const [roleId, setRoleId] = useState('');
  const [status, setStatus] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 20;

  // Modals state
  const [isAddOpen, setIsAddOpen] = useState(false);
  const [isDeptsOpen, setIsDeptsOpen] = useState(false);
  const [isRolesOpen, setIsRolesOpen] = useState(false);
  const [assigningStaff, setAssigningStaff] = useState(null);

  // 1. Fetch Staff Directory
  const {
    data: staffData,
    loading: staffLoading,
    error: staffError,
    refetch: refetchStaff
  } = useApiQuery(
    () => {
      if (!currentOrgId) return Promise.resolve({ items: [], totalPages: 1, totalCount: 0 });
      return apiClient.get('/org/staff', {
        params: {
          search: search.trim() || undefined,
          departmentId: departmentId || undefined,
          roleId: roleId || undefined,
          status: status || undefined,
          pageNumber: currentPage,
          pageSize
        }
      });
    },
    { deps: [currentOrgId, search, departmentId, roleId, status, currentPage] }
  );

  // 2. Fetch Departments for dropdown filters
  const {
    data: deptData,
    refetch: refetchDepts
  } = useApiQuery(
    () => {
      if (!currentOrgId) return Promise.resolve({ items: [] });
      return apiClient.get('/org/departments', { params: { pageSize: 100 } });
    },
    { deps: [currentOrgId] }
  );

  // 3. Fetch Workforce Roles
  const {
    data: rolesData,
    refetch: refetchRoles
  } = useApiQuery(
    () => {
      if (!currentOrgId) return Promise.resolve({ items: [] });
      return apiClient.get('/org/roles', { params: { pageSize: 100 } });
    },
    { deps: [currentOrgId] }
  );

  // 4. Fetch Salary Levels
  const {
    data: levelsData,
    refetch: refetchLevels
  } = useApiQuery(
    () => {
      if (!currentOrgId) return Promise.resolve({ items: [] });
      return apiClient.get('/org/levels', { params: { pageSize: 100 } });
    },
    { deps: [currentOrgId] }
  );

  const staffList = staffData?.items || [];
  const totalPages = staffData?.totalPages || 1;
  const totalCount = staffData?.totalCount || staffList.length;

  const departments = deptData?.items || [];
  const roles = rolesData?.items || [];
  const salaryLevels = levelsData?.items || [];

  const handleRefreshAll = () => {
    refetchStaff();
    refetchDepts();
    refetchRoles();
    refetchLevels();
  };

  const departmentFilterOptions = [
    { value: '', label: 'All Departments' },
    ...departments.map((d) => ({ value: d.id, label: d.name }))
  ];

  const roleFilterOptions = [
    { value: '', label: 'All Roles' },
    ...roles.map((r) => ({ value: r.id, label: r.title }))
  ];

  const statusFilterOptions = [
    { value: '', label: 'All Statuses' },
    { value: 'Active', label: 'Active' },
    { value: 'Pending', label: 'Invited' },
    { value: 'Suspended', label: 'Suspended' },
    { value: 'Terminated', label: 'Terminated' }
  ];

  return (
    <CustomerLayout
      title="Staff & Workforce"
      subtitle="Corporate employees, departmental structure, roles, and compensation bands"
      headerAction={
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            icon={Building2}
            onClick={() => setIsDeptsOpen(true)}
          >
            Departments
          </Button>
          <Button
            variant="outline"
            size="sm"
            icon={Briefcase}
            onClick={() => setIsRolesOpen(true)}
          >
            Roles & Levels
          </Button>
          <Button
            variant="primary"
            size="sm"
            icon={UserPlus}
            onClick={() => setIsAddOpen(true)}
          >
            Add Staff
          </Button>
        </div>
      }
    >
      <div className="space-y-6">
        {/* Metric Summary Cards */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
          <StatCard
            icon={Users}
            label="Total Headcount"
            value={totalCount.toString()}
            loading={staffLoading}
          />
          <StatCard
            icon={Building2}
            label="Departments"
            value={departments.length.toString()}
          />
          <StatCard
            icon={Briefcase}
            label="Workforce Roles"
            value={roles.length.toString()}
          />
          <StatCard
            icon={Users}
            label="Salary Levels"
            value={salaryLevels.length.toString()}
          />
        </div>

        {/* Search & Filters Toolbar */}
        <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-3">
          <SearchInput
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onClear={() => setSearch('')}
            placeholder="Search by staff name or email..."
            className="w-full lg:max-w-xs"
          />

          <div className="flex flex-wrap items-center gap-2">
            <div className="w-36">
              <Select
                options={departmentFilterOptions}
                value={departmentId}
                onChange={(e) => {
                  setDepartmentId(e.target.value);
                  setCurrentPage(1);
                }}
              />
            </div>

            <div className="w-36">
              <Select
                options={roleFilterOptions}
                value={roleId}
                onChange={(e) => {
                  setRoleId(e.target.value);
                  setCurrentPage(1);
                }}
              />
            </div>

            <div className="w-32">
              <Select
                options={statusFilterOptions}
                value={status}
                onChange={(e) => {
                  setStatus(e.target.value);
                  setCurrentPage(1);
                }}
              />
            </div>

            <TableExport
              label="Export"
              onExportCsv={() => {
                const csvContent =
                  'data:text/csv;charset=utf-8,' +
                  ['Name,Email,Department,Role,SalaryLevel,Status']
                    .concat(
                      staffList.map(
                        (s) =>
                          `"${s.fullName || s.firstName || ''}","${s.email || ''}","${s.departmentName || ''}","${s.workforceRoleTitle || ''}","${s.salaryLevelName || ''}","${s.status || ''}"`
                      )
                    )
                    .join('\n');
                const encodedUri = encodeURI(csvContent);
                const link = document.createElement('a');
                link.setAttribute('href', encodedUri);
                link.setAttribute('download', `staff_${new Date().toISOString().slice(0, 10)}.csv`);
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
              }}
            />
          </div>
        </div>

        {/* Staff Table */}
        <StaffTable
          staff={staffList}
          loading={staffLoading}
          error={staffError}
          currentPage={currentPage}
          totalPages={totalPages}
          onPageChange={(p) => setCurrentPage(p)}
          onRetry={refetchStaff}
          onRefresh={handleRefreshAll}
          onAssignWorkforce={(member) => setAssigningStaff(member)}
          onAddStaff={() => setIsAddOpen(true)}
        />
      </div>

      {/* Add Staff Modal */}
      <AddStaffModal
        isOpen={isAddOpen}
        onClose={() => setIsAddOpen(false)}
        departments={departments}
        roles={roles}
        salaryLevels={salaryLevels}
        onSuccess={handleRefreshAll}
      />

      {/* Departments Manager Modal */}
      <DepartmentsModal
        isOpen={isDeptsOpen}
        onClose={() => setIsDeptsOpen(false)}
        onChanged={handleRefreshAll}
      />

      {/* Workforce Roles & Salary Levels Modal */}
      <WorkforceRolesModal
        isOpen={isRolesOpen}
        onClose={() => setIsRolesOpen(false)}
        departments={departments}
        onChanged={handleRefreshAll}
      />

      {/* Assign Workforce Modal */}
      <AssignWorkforceModal
        isOpen={!!assigningStaff}
        onClose={() => setAssigningStaff(null)}
        staff={assigningStaff}
        departments={departments}
        roles={roles}
        salaryLevels={salaryLevels}
        onSuccess={handleRefreshAll}
      />
    </CustomerLayout>
  );
}
