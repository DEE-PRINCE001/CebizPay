import React, { useState } from 'react';
import Modal from '../common/Modal';
import Tabs from '../common/Tabs';
import Input from '../forms/Input';
import Select from '../forms/Select';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import Skeleton from '../common/Skeleton';
import ConfirmModal from '../feedback/ConfirmModal';
import { Briefcase, Layers, Plus, Edit2, Trash2, Check } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Workforce roles and salary tiers management modal.
 */
export default function WorkforceRolesModal({
  isOpen,
  onClose,
  departments = [],
  onChanged
}) {
  const { showSuccess, showError } = useToast();
  const [subTab, setSubTab] = useState('roles'); // 'roles' | 'levels'

  // Roles State
  const [roleTitle, setRoleTitle] = useState('');
  const [roleDeptId, setRoleDeptId] = useState('');
  const [roleDesc, setRoleDesc] = useState('');
  const [editingRole, setEditingRole] = useState(null);
  const [deletingRole, setDeletingRole] = useState(null);

  // Salary Levels State
  const [levelName, setLevelName] = useState('');
  const [baseAmount, setBaseAmount] = useState('');
  const [editingLevel, setEditingLevel] = useState(null);
  const [deletingLevel, setDeletingLevel] = useState(null);

  const [loadingAction, setLoadingAction] = useState(false);
  const [error, setError] = useState(null);

  const subTabs = [
    { id: 'roles', label: 'Workforce Roles', icon: Briefcase },
    { id: 'levels', label: 'Salary Levels', icon: Layers }
  ];

  // 1. Fetch Roles
  const {
    data: rolesData,
    loading: rolesLoading,
    refetch: refetchRoles
  } = useApiQuery(
    () => apiClient.get('/org/roles', { params: { pageSize: 50 } }),
    { enabled: isOpen }
  );

  // 2. Fetch Salary Levels
  const {
    data: levelsData,
    loading: levelsLoading,
    refetch: refetchLevels
  } = useApiQuery(
    () => apiClient.get('/org/levels', { params: { pageSize: 50 } }),
    { enabled: isOpen }
  );

  const roles = rolesData?.items || [];
  const salaryLevels = levelsData?.items || [];

  // Submit Role Create / Edit
  const handleRoleSubmit = async (e) => {
    e.preventDefault();
    if (!roleTitle.trim()) {
      setError('Role title is required.');
      return;
    }

    setLoadingAction(true);
    setError(null);

    try {
      if (editingRole) {
        await apiClient.put(`/org/roles/${editingRole.id}`, {
          title: roleTitle.trim(),
          departmentId: roleDeptId || null,
          description: roleDesc.trim() || null
        });
        showSuccess('Workforce role updated.');
      } else {
        await apiClient.post('/org/roles', {
          title: roleTitle.trim(),
          departmentId: roleDeptId || null,
          description: roleDesc.trim() || null
        });
        showSuccess('Workforce role created.');
      }

      setRoleTitle('');
      setRoleDeptId('');
      setRoleDesc('');
      setEditingRole(null);
      refetchRoles();
      if (onChanged) onChanged();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to save workforce role.');
    } finally {
      setLoadingAction(false);
    }
  };

  // Submit Salary Level Create / Edit
  const handleLevelSubmit = async (e) => {
    e.preventDefault();
    const amount = parseFloat(baseAmount);
    if (!levelName.trim() || isNaN(amount) || amount <= 0) {
      setError('Please provide a valid level name and positive base amount.');
      return;
    }

    setLoadingAction(true);
    setError(null);

    try {
      if (editingLevel) {
        await apiClient.put(`/org/levels/${editingLevel.id}`, {
          levelName: levelName.trim(),
          baseAmount: amount,
          currency: 'NGN'
        });
        showSuccess('Salary level updated.');
      } else {
        await apiClient.post('/org/levels', {
          levelName: levelName.trim(),
          baseAmount: amount,
          currency: 'NGN'
        });
        showSuccess('Salary level created.');
      }

      setLevelName('');
      setBaseAmount('');
      setEditingLevel(null);
      refetchLevels();
      if (onChanged) onChanged();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to save salary level.');
    } finally {
      setLoadingAction(false);
    }
  };

  const handleDeleteRole = async () => {
    if (!deletingRole) return;
    try {
      await apiClient.delete(`/org/roles/${deletingRole.id}`);
      showSuccess('Role deleted.');
      setDeletingRole(null);
      refetchRoles();
      if (onChanged) onChanged();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      showError(parsed.message || 'Could not delete role.');
    }
  };

  const handleDeleteLevel = async () => {
    if (!deletingLevel) return;
    try {
      await apiClient.delete(`/org/levels/${deletingLevel.id}`);
      showSuccess('Salary level deleted.');
      setDeletingLevel(null);
      refetchLevels();
      if (onChanged) onChanged();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      showError(parsed.message || 'Could not delete salary level.');
    }
  };

  const deptOptions = [
    { value: '', label: 'No Department (Global Role)' },
    ...departments.map((d) => ({ value: d.id, label: d.name }))
  ];

  return (
    <>
      <Modal
        isOpen={isOpen}
        onClose={onClose}
        title="Workforce Roles & Salary Levels"
        subtitle="Configure job titles and compensation bands"
        maxWidth="max-w-xl"
      >
        <div className="space-y-4 pt-1">
          <Tabs
            variant="segmented"
            tabs={subTabs}
            activeTab={subTab}
            onChange={(t) => {
              setSubTab(t);
              setError(null);
            }}
          />

          {error && (
            <Alert variant="danger" onClose={() => setError(null)}>
              {error}
            </Alert>
          )}

          {/* VIEWPORT 1: Workforce Roles */}
          {subTab === 'roles' && (
            <div className="space-y-4">
              <form onSubmit={handleRoleSubmit} className="p-4 bg-slate-50 border border-slate-200/80 rounded-2xl space-y-3">
                <span className="text-xs font-bold text-slate-900 block">
                  {editingRole ? 'Edit Workforce Role' : 'Create Workforce Role'}
                </span>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <Input
                    label="Role Title"
                    placeholder="e.g. Senior Frontend Engineer"
                    value={roleTitle}
                    onChange={(e) => {
                      setRoleTitle(e.target.value);
                      if (error) setError(null);
                    }}
                    required
                  />
                  <Select
                    label="Department"
                    options={deptOptions}
                    value={roleDeptId}
                    onChange={(e) => setRoleDeptId(e.target.value)}
                  />
                </div>

                <div className="flex items-center justify-end gap-2 pt-1">
                  {editingRole && (
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        setEditingRole(null);
                        setRoleTitle('');
                        setRoleDeptId('');
                      }}
                    >
                      Cancel
                    </Button>
                  )}
                  <Button
                    type="submit"
                    variant="primary"
                    size="sm"
                    loading={loadingAction}
                    icon={editingRole ? Check : Plus}
                  >
                    {editingRole ? 'Save Role' : 'Add Role'}
                  </Button>
                </div>
              </form>

              {/* Roles List */}
              <div className="space-y-2">
                <span className="text-xs font-bold text-slate-900 block">
                  Active Roles ({roles.length})
                </span>

                {rolesLoading && <Skeleton variant="card" />}

                {!rolesLoading && roles.length === 0 && (
                  <div className="text-center py-6 text-xs text-slate-400">
                    No workforce roles defined yet.
                  </div>
                )}

                {!rolesLoading && roles.length > 0 && (
                  <div className="space-y-2 max-h-56 overflow-y-auto pr-1">
                    {roles.map((r) => (
                      <div
                        key={r.id}
                        className="p-3 bg-white border border-slate-200 rounded-xl flex items-center justify-between text-xs"
                      >
                        <div>
                          <span className="font-bold text-slate-900 block">{r.title}</span>
                          <span className="text-[11px] text-slate-400 block">{r.departmentName || 'Global Role'}</span>
                        </div>
                        <div className="flex items-center gap-1">
                          <button
                            type="button"
                            onClick={() => {
                              setEditingRole(r);
                              setRoleTitle(r.title);
                              setRoleDeptId(r.departmentId || '');
                            }}
                            className="p-1.5 text-slate-400 hover:text-brand-600 rounded-lg hover:bg-slate-50 transition"
                          >
                            <Edit2 size={14} />
                          </button>
                          <button
                            type="button"
                            onClick={() => setDeletingRole(r)}
                            className="p-1.5 text-slate-400 hover:text-red-600 rounded-lg hover:bg-red-50 transition"
                          >
                            <Trash2 size={14} />
                          </button>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}

          {/* VIEWPORT 2: Salary Levels */}
          {subTab === 'levels' && (
            <div className="space-y-4">
              <form onSubmit={handleLevelSubmit} className="p-4 bg-slate-50 border border-slate-200/80 rounded-2xl space-y-3">
                <span className="text-xs font-bold text-slate-900 block">
                  {editingLevel ? 'Edit Salary Level' : 'Create Salary Level'}
                </span>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <Input
                    label="Level Name"
                    placeholder="e.g. Level 1, Executive Band"
                    value={levelName}
                    onChange={(e) => {
                      setLevelName(e.target.value);
                      if (error) setError(null);
                    }}
                    required
                  />
                  <Input
                    label="Base Salary (₦)"
                    type="number"
                    step="1000"
                    placeholder="e.g. 350000"
                    value={baseAmount}
                    onChange={(e) => {
                      setBaseAmount(e.target.value);
                      if (error) setError(null);
                    }}
                    required
                  />
                </div>

                <div className="flex items-center justify-end gap-2 pt-1">
                  {editingLevel && (
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        setEditingLevel(null);
                        setLevelName('');
                        setBaseAmount('');
                      }}
                    >
                      Cancel
                    </Button>
                  )}
                  <Button
                    type="submit"
                    variant="primary"
                    size="sm"
                    loading={loadingAction}
                    icon={editingLevel ? Check : Plus}
                  >
                    {editingLevel ? 'Save Level' : 'Add Level'}
                  </Button>
                </div>
              </form>

              {/* Levels List */}
              <div className="space-y-2">
                <span className="text-xs font-bold text-slate-900 block">
                  Configured Salary Bands ({salaryLevels.length})
                </span>

                {levelsLoading && <Skeleton variant="card" />}

                {!levelsLoading && salaryLevels.length === 0 && (
                  <div className="text-center py-6 text-xs text-slate-400">
                    No salary levels configured yet.
                  </div>
                )}

                {!levelsLoading && salaryLevels.length > 0 && (
                  <div className="space-y-2 max-h-56 overflow-y-auto pr-1">
                    {salaryLevels.map((lvl) => (
                      <div
                        key={lvl.id}
                        className="p-3 bg-white border border-slate-200 rounded-xl flex items-center justify-between text-xs"
                      >
                        <div>
                          <span className="font-bold text-slate-900 block">{lvl.levelName}</span>
                          <span className="font-mono font-semibold text-brand-600 block">
                            ₦{(lvl.baseAmount || 0).toLocaleString()} / month
                          </span>
                        </div>
                        <div className="flex items-center gap-1">
                          <button
                            type="button"
                            onClick={() => {
                              setEditingLevel(lvl);
                              setLevelName(lvl.levelName);
                              setBaseAmount(lvl.baseAmount?.toString() || '');
                            }}
                            className="p-1.5 text-slate-400 hover:text-brand-600 rounded-lg hover:bg-slate-50 transition"
                          >
                            <Edit2 size={14} />
                          </button>
                          <button
                            type="button"
                            onClick={() => setDeletingLevel(lvl)}
                            className="p-1.5 text-slate-400 hover:text-red-600 rounded-lg hover:bg-red-50 transition"
                          >
                            <Trash2 size={14} />
                          </button>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}
        </div>
      </Modal>

      {/* Delete Role Confirmation */}
      {deletingRole && (
        <ConfirmModal
          isOpen={true}
          onClose={() => setDeletingRole(null)}
          onConfirm={handleDeleteRole}
          title="Delete Workforce Role"
          message={`Are you sure you want to delete the "${deletingRole.title}" role?`}
          confirmText="Delete Role"
          confirmVariant="danger"
        />
      )}

      {/* Delete Level Confirmation */}
      {deletingLevel && (
        <ConfirmModal
          isOpen={true}
          onClose={() => setDeletingLevel(null)}
          onConfirm={handleDeleteLevel}
          title="Delete Salary Level"
          message={`Are you sure you want to delete the "${deletingLevel.levelName}" salary level?`}
          confirmText="Delete Level"
          confirmVariant="danger"
        />
      )}
    </>
  );
}
