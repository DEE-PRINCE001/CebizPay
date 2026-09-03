import React, { useState } from 'react';
import Modal from '../common/Modal';
import Input from '../forms/Input';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import Skeleton from '../common/Skeleton';
import EmptyState from '../feedback/EmptyState';
import ConfirmModal from '../feedback/ConfirmModal';
import { Building2, Plus, Edit2, Trash2, Check } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Department management modal for creating and updating departments.
 */
export default function DepartmentsModal({
  isOpen,
  onClose,
  onChanged
}) {
  const { showSuccess, showError } = useToast();

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [editingDept, setEditingDept] = useState(null);
  const [deletingDept, setDeletingDept] = useState(null);

  const [formLoading, setFormLoading] = useState(false);
  const [error, setError] = useState(null);

  const {
    data: deptData,
    loading,
    refetch
  } = useApiQuery(
    () => apiClient.get('/org/departments', { params: { pageSize: 50 } }),
    { enabled: isOpen }
  );

  const departments = deptData?.items || [];

  const handleFormSubmit = async (e) => {
    e.preventDefault();
    if (!name.trim()) {
      setError('Department name is required.');
      return;
    }

    setFormLoading(true);
    setError(null);

    try {
      if (editingDept) {
        await apiClient.put(`/org/departments/${editingDept.id}`, {
          name: name.trim(),
          description: description.trim() || null
        });
        showSuccess('Department updated successfully.');
      } else {
        await apiClient.post('/org/departments', {
          name: name.trim(),
          description: description.trim() || null
        });
        showSuccess('Department created successfully.');
      }

      setName('');
      setDescription('');
      setEditingDept(null);
      refetch();
      if (onChanged) onChanged();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to save department.');
    } finally {
      setFormLoading(false);
    }
  };

  const handleDelete = async () => {
    if (!deletingDept) return;
    try {
      await apiClient.delete(`/org/departments/${deletingDept.id}`);
      showSuccess('Department deleted.');
      setDeletingDept(null);
      refetch();
      if (onChanged) onChanged();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      showError(parsed.message || 'Could not delete department.');
    }
  };

  const startEdit = (dept) => {
    setEditingDept(dept);
    setName(dept.name);
    setDescription(dept.description || '');
    setError(null);
  };

  const cancelEdit = () => {
    setEditingDept(null);
    setName('');
    setDescription('');
    setError(null);
  };

  return (
    <>
      <Modal
        isOpen={isOpen}
        onClose={onClose}
        title="Manage Departments"
        subtitle="Organize staff members into functional operational units"
        maxWidth="max-w-xl"
      >
        <div className="space-y-5 pt-1">
          {error && (
            <Alert variant="danger" onClose={() => setError(null)}>
              {error}
            </Alert>
          )}

          {/* Create / Edit Form */}
          <form onSubmit={handleFormSubmit} className="p-4 bg-slate-50 border border-slate-200/80 rounded-2xl space-y-3">
            <span className="text-xs font-bold text-slate-900 block">
              {editingDept ? 'Edit Department' : 'Create New Department'}
            </span>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <Input
                label="Department Name"
                placeholder="e.g. Engineering, Finance"
                value={name}
                onChange={(e) => {
                  setName(e.target.value);
                  if (error) setError(null);
                }}
                required
              />
              <Input
                label="Description"
                placeholder="Operational purpose (optional)"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
              />
            </div>

            <div className="flex items-center justify-end gap-2 pt-1">
              {editingDept && (
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={cancelEdit}
                >
                  Cancel
                </Button>
              )}
              <Button
                type="submit"
                variant="primary"
                size="sm"
                loading={formLoading}
                icon={editingDept ? Check : Plus}
              >
                {editingDept ? 'Save Changes' : 'Add Department'}
              </Button>
            </div>
          </form>

          {/* Department List */}
          <div className="space-y-2">
            <span className="text-xs font-bold text-slate-900 block">
              Existing Departments ({departments.length})
            </span>

            {loading && (
              <div className="space-y-2">
                <Skeleton variant="card" count={2} />
              </div>
            )}

            {!loading && departments.length === 0 && (
              <div className="text-center py-6 text-xs text-slate-400">
                No departments configured yet.
              </div>
            )}

            {!loading && departments.length > 0 && (
              <div className="space-y-2 max-h-60 overflow-y-auto pr-1">
                {departments.map((dept) => (
                  <div
                    key={dept.id}
                    className="p-3 bg-white border border-slate-200 rounded-xl flex items-center justify-between gap-3 text-xs"
                  >
                    <div className="flex items-center gap-2.5 min-w-0">
                      <div className="w-7 h-7 rounded-lg bg-brand-50 text-brand-600 flex items-center justify-center shrink-0">
                        <Building2 size={15} />
                      </div>
                      <div className="min-w-0">
                        <span className="font-bold text-slate-900 block truncate">{dept.name}</span>
                        {dept.description && (
                          <span className="text-[11px] text-slate-400 block truncate">{dept.description}</span>
                        )}
                      </div>
                    </div>

                    <div className="flex items-center gap-1 shrink-0">
                      <button
                        type="button"
                        onClick={() => startEdit(dept)}
                        className="p-1.5 text-slate-400 hover:text-brand-600 hover:bg-slate-50 rounded-lg transition"
                        title="Edit Department"
                      >
                        <Edit2 size={14} />
                      </button>
                      <button
                        type="button"
                        onClick={() => setDeletingDept(dept)}
                        className="p-1.5 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition"
                        title="Delete Department"
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
      </Modal>

      {/* Delete Confirmation */}
      {deletingDept && (
        <ConfirmModal
          isOpen={true}
          onClose={() => setDeletingDept(null)}
          onConfirm={handleDelete}
          title="Delete Department"
          message={`Are you sure you want to delete the "${deletingDept.name}" department?`}
          confirmText="Delete Department"
          confirmVariant="danger"
        />
      )}
    </>
  );
}
