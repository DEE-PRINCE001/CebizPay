import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Tabs from '../../components/common/Tabs';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency } from '../../utils/formatters';
import { Layers, Briefcase, DollarSign, Plus, Trash2, Edit } from 'lucide-react';

export default function OrgDepartmentsRoles() {
  const [activeTab, setActiveTab] = useState('departments'); // 'departments' | 'roles' | 'levels'
  const { showSuccess } = useToast();

  const [showDeptModal, setShowDeptModal] = useState(false);
  const [deptName, setDeptName] = useState('');
  const [deptDesc, setDeptDesc] = useState('');

  const [showRoleModal, setShowRoleModal] = useState(false);
  const [roleTitle, setRoleTitle] = useState('');
  const [roleDept, setRoleDept] = useState('Engineering');

  const [showLevelModal, setShowLevelModal] = useState(false);
  const [levelName, setLevelName] = useState('');
  const [levelSalary, setLevelSalary] = useState('800000');

  const [departments, setDepartments] = useState([
    { id: 'dept-01', name: 'Engineering', description: 'Core software engineering and infrastructure', staffCount: 12 },
    { id: 'dept-02', name: 'Product & Design', description: 'Product strategy, UI/UX and user research', staffCount: 6 },
    { id: 'dept-03', name: 'Finance & Accounting', description: 'Corporate treasury, reconciliations, and tax', staffCount: 4 },
    { id: 'dept-04', name: 'Human Resources', description: 'People operations, talent acquisition, and culture', staffCount: 3 },
    { id: 'dept-05', name: 'Sales & Growth', description: 'B2B enterprise partnerships and revenue', staffCount: 3 }
  ]);

  const [roles, setRoles] = useState([
    { id: 'role-01', title: 'Senior Software Engineer', department: 'Engineering', staffCount: 5 },
    { id: 'role-02', title: 'Lead Product Manager', department: 'Product & Design', staffCount: 2 },
    { id: 'role-03', title: 'Financial Controller', department: 'Finance & Accounting', staffCount: 1 },
    { id: 'role-04', title: 'People Operations Lead', department: 'Human Resources', staffCount: 1 }
  ]);

  const [salaryLevels, setSalaryLevels] = useState([
    { id: 'lvl-01', name: 'L1 - Associate Entry', currency: 'NGN', baseAmount: 350000.0, staffCount: 4 },
    { id: 'lvl-02', name: 'L2 - Mid-Level Specialist', currency: 'NGN', baseAmount: 650000.0, staffCount: 10 },
    { id: 'lvl-03', name: 'L3 - Senior Specialist', currency: 'NGN', baseAmount: 950000.0, staffCount: 8 },
    { id: 'lvl-04', name: 'L4 - Principal / Lead', currency: 'NGN', baseAmount: 1400000.0, staffCount: 4 },
    { id: 'lvl-05', name: 'L5 - Director / Executive', currency: 'NGN', baseAmount: 2200000.0, staffCount: 2 }
  ]);

  const handleAddDept = (e) => {
    e.preventDefault();
    const newD = { id: `dept-${Date.now()}`, name: deptName, description: deptDesc, staffCount: 0 };
    setDepartments((prev) => [...prev, newD]);
    showSuccess('Department Created', `${deptName} added to organization hierarchy.`);
    setShowDeptModal(false);
    setDeptName('');
    setDeptDesc('');
  };

  const handleAddRole = (e) => {
    e.preventDefault();
    const newR = { id: `role-${Date.now()}`, title: roleTitle, department: roleDept, staffCount: 0 };
    setRoles((prev) => [...prev, newR]);
    showSuccess('Workforce Role Created', `${roleTitle} added to ${roleDept}.`);
    setShowRoleModal(false);
    setRoleTitle('');
  };

  const handleAddLevel = (e) => {
    e.preventDefault();
    const newL = { id: `lvl-${Date.now()}`, name: levelName, currency: 'NGN', baseAmount: parseFloat(levelSalary), staffCount: 0 };
    setSalaryLevels((prev) => [...prev, newL]);
    showSuccess('Salary Level Created', `${levelName} compensation band configured.`);
    setShowLevelModal(false);
    setLevelName('');
  };

  const deptColumns = [
    { header: 'Department Name', accessor: 'name', render: (row) => <span className="font-bold text-slate-900">{row.name}</span> },
    { header: 'Description', accessor: 'description', render: (row) => <span className="text-slate-600">{row.description}</span> },
    { header: 'Headcount', accessor: 'staffCount', render: (row) => <span className="font-bold text-slate-800">{row.staffCount} Staff</span> },
    {
      header: 'Actions',
      align: 'right',
      render: (row) => (
        <button
          onClick={() => {
            setDepartments((prev) => prev.filter((d) => d.id !== row.id));
            showSuccess('Department Removed', 'Hierarchy updated.');
          }}
          className="p-1 text-slate-400 hover:text-rose-600 rounded transition-colors"
        >
          <Trash2 className="w-4 h-4" />
        </button>
      )
    }
  ];

  const roleColumns = [
    { header: 'Role Title', accessor: 'title', render: (row) => <span className="font-bold text-slate-900">{row.title}</span> },
    { header: 'Department', accessor: 'department', render: (row) => <span className="font-semibold text-slate-700">{row.department}</span> },
    { header: 'Assigned Staff', accessor: 'staffCount', render: (row) => <span className="font-bold text-slate-800">{row.staffCount} Staff</span> },
    {
      header: 'Actions',
      align: 'right',
      render: (row) => (
        <button
          onClick={() => {
            setRoles((prev) => prev.filter((r) => r.id !== row.id));
            showSuccess('Role Removed', 'Workforce role updated.');
          }}
          className="p-1 text-slate-400 hover:text-rose-600 rounded transition-colors"
        >
          <Trash2 className="w-4 h-4" />
        </button>
      )
    }
  ];

  const levelColumns = [
    { header: 'Compensation Level', accessor: 'name', render: (row) => <span className="font-bold text-slate-900">{row.name}</span> },
    { header: 'Base Monthly Salary', accessor: 'baseAmount', render: (row) => <span className="font-mono font-bold text-slate-900">{formatCurrency(row.baseAmount)}</span> },
    { header: 'Assigned Staff', accessor: 'staffCount', render: (row) => <span className="font-bold text-slate-800">{row.staffCount} Staff</span> },
    {
      header: 'Actions',
      align: 'right',
      render: (row) => (
        <button
          onClick={() => {
            setSalaryLevels((prev) => prev.filter((l) => l.id !== row.id));
            showSuccess('Salary Level Removed', 'Compensation level updated.');
          }}
          className="p-1 text-slate-400 hover:text-rose-600 rounded transition-colors"
        >
          <Trash2 className="w-4 h-4" />
        </button>
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title="Departments, Roles &amp; Compensation Levels"
        subtitle="Define organizational structure, functional divisions, job titles, and standardized salary compensation bands."
        actions={
          <button
            onClick={() => {
              if (activeTab === 'departments') setShowDeptModal(true);
              else if (activeTab === 'roles') setShowRoleModal(true);
              else setShowLevelModal(true);
            }}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
          >
            <Plus className="w-3.5 h-3.5" />
            Add {activeTab === 'departments' ? 'Department' : activeTab === 'roles' ? 'Workforce Role' : 'Salary Level'}
          </button>
        }
      />

      <Tabs
        tabs={[
          { id: 'departments', label: 'Departments', count: departments.length, icon: Layers },
          { id: 'roles', label: 'Workforce Roles', count: roles.length, icon: Briefcase },
          { id: 'levels', label: 'Salary Levels (Bands)', count: salaryLevels.length, icon: DollarSign }
        ]}
        activeTab={activeTab}
        onChange={setActiveTab}
        className="mb-6"
      />

      {activeTab === 'departments' && <DataTable columns={deptColumns} data={departments} />}
      {activeTab === 'roles' && <DataTable columns={roleColumns} data={roles} />}
      {activeTab === 'levels' && <DataTable columns={levelColumns} data={salaryLevels} />}

      {/* Add Dept Modal */}
      <Modal
        isOpen={showDeptModal}
        onClose={() => setShowDeptModal(false)}
        title="Create Department"
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowDeptModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
            <button onClick={handleAddDept} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Create Department</button>
          </div>
        }
      >
        <form onSubmit={handleAddDept} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Department Name</label>
            <input type="text" required value={deptName} onChange={(e) => setDeptName(e.target.value)} placeholder="e.g. Data & Analytics" className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl" />
          </div>
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Description</label>
            <textarea rows={3} value={deptDesc} onChange={(e) => setDeptDesc(e.target.value)} placeholder="Functional responsibilities..." className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl" />
          </div>
        </form>
      </Modal>

      {/* Add Role Modal */}
      <Modal
        isOpen={showRoleModal}
        onClose={() => setShowRoleModal(false)}
        title="Create Workforce Role"
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowRoleModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
            <button onClick={handleAddRole} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Create Role</button>
          </div>
        }
      >
        <form onSubmit={handleAddRole} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Role Title</label>
            <input type="text" required value={roleTitle} onChange={(e) => setRoleTitle(e.target.value)} placeholder="e.g. Lead Backend Engineer" className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl" />
          </div>
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Assigned Department</label>
            <select value={roleDept} onChange={(e) => setRoleDept(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-bold">
              {departments.map((d) => (
                <option key={d.id} value={d.name}>{d.name}</option>
              ))}
            </select>
          </div>
        </form>
      </Modal>

      {/* Add Level Modal */}
      <Modal
        isOpen={showLevelModal}
        onClose={() => setShowLevelModal(false)}
        title="Create Salary Level Band"
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowLevelModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
            <button onClick={handleAddLevel} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Create Level</button>
          </div>
        }
      >
        <form onSubmit={handleAddLevel} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Level Name</label>
            <input type="text" required value={levelName} onChange={(e) => setLevelName(e.target.value)} placeholder="e.g. L3 - Technical Specialist" className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl" />
          </div>
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Base Monthly Gross Salary (₦)</label>
            <input type="number" required value={levelSalary} onChange={(e) => setLevelSalary(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold" />
          </div>
        </form>
      </Modal>
    </div>
  );
}
