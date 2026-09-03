import React, { useState, useEffect } from 'react';
import Modal from '../common/Modal';
import Select from '../forms/Select';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { UserCheck } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Modal to reassign department, workforce role, and salary level to a staff member.
 */
export default function AssignWorkforceModal({
  isOpen,
  onClose,
  staff,
  departments = [],
  roles = [],
  salaryLevels = [],
  onSuccess
}) {
  const { showSuccess } = useToast();

  const [departmentId, setDepartmentId] = useState('');
  const [workforceRoleId, setWorkforceRoleId] = useState('');
  const [salaryLevelId, setSalaryLevelId] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (staff) {
      setDepartmentId(staff.departmentId || '');
      setWorkforceRoleId(staff.workforceRoleId || '');
      setSalaryLevelId(staff.salaryLevelId || '');
      setError(null);
    }
  }, [staff]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!staff) return;

    setLoading(true);
    setError(null);

    try {
      await apiClient.put(`/org/staff/${staff.id}/assign`, {
        departmentId: departmentId || null,
        workforceRoleId: workforceRoleId || null,
        salaryLevelId: salaryLevelId || null
      });

      showSuccess(`Workforce details updated for ${staff.fullName || staff.email}.`);
      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to update workforce assignment.');
    } finally {
      setLoading(false);
    }
  };

  const departmentOptions = [
    { value: '', label: 'None (Unassigned)' },
    ...departments.map((d) => ({ value: d.id, label: d.name }))
  ];

  const roleOptions = [
    { value: '', label: 'None (General Staff)' },
    ...roles.map((r) => ({ value: r.id, label: r.title }))
  ];

  const salaryOptions = [
    { value: '', label: 'Default Band' },
    ...salaryLevels.map((s) => ({
      value: s.id,
      label: `${s.levelName} — ₦${(s.baseAmount || 0).toLocaleString()}`
    }))
  ];

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Assign Workforce Structure"
      subtitle={`Staff: ${staff?.fullName || staff?.email || 'Employee'}`}
      maxWidth="max-w-md"
    >
      <form onSubmit={handleSubmit} className="space-y-4 pt-1">
        {error && (
          <Alert variant="danger" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        <Select
          label="Operational Department"
          options={departmentOptions}
          value={departmentId}
          onChange={(e) => setDepartmentId(e.target.value)}
        />

        <Select
          label="Workforce Role"
          options={roleOptions}
          value={workforceRoleId}
          onChange={(e) => setWorkforceRoleId(e.target.value)}
        />

        <Select
          label="Salary Level & Compensation Band"
          options={salaryOptions}
          value={salaryLevelId}
          onChange={(e) => setSalaryLevelId(e.target.value)}
        />

        <div className="flex items-center gap-3 pt-3 border-t border-slate-100">
          <Button
            variant="outline"
            size="md"
            onClick={onClose}
            disabled={loading}
            className="flex-1"
          >
            Cancel
          </Button>
          <Button
            type="submit"
            variant="primary"
            size="md"
            loading={loading}
            icon={UserCheck}
            className="flex-1"
          >
            Update Assignment
          </Button>
        </div>
      </form>
    </Modal>
  );
}
