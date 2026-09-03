import React, { useState } from 'react';
import Modal from '../common/Modal';
import Input from '../forms/Input';
import Select from '../forms/Select';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { UserPlus, Mail, Shield, Check } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

const ADMIN_ROLES = [
  { value: 2, label: 'Platform Administrator (Admin)' },
  { value: 1, label: 'Super Administrator (SuperAdmin)' },
  { value: 3, label: 'Compliance & Risk Officer' },
  { value: 4, label: 'Financial Auditor' },
  { value: 5, label: 'Customer Support Agent' }
];

/**
 * Modal to issue single-use administrative invitations.
 */
export default function InviteAdminModal({
  isOpen,
  onClose,
  onSuccess
}) {
  const { showSuccess } = useToast();

  const [email, setEmail] = useState('');
  const [role, setRole] = useState(2);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!email.trim() || !email.includes('@')) {
      setError('Please provide a valid corporate email address.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      await apiClient.post('/admin/manage/invite', {
        email: email.trim(),
        role: parseInt(role, 10)
      });

      showSuccess(`Administrative invitation sent to ${email}.`);
      setEmail('');
      setRole(2);
      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to issue administrative invite.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Invite Administrative User"
      subtitle="Issues a single-use 24-hour credential setup token to a staff member"
      maxWidth="max-w-md"
    >
      <form onSubmit={handleSubmit} className="space-y-4 pt-1">
        {error && (
          <Alert variant="danger" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        <Input
          label="Corporate Email Address"
          type="email"
          placeholder="admin@cebizpay.com"
          value={email}
          onChange={(e) => {
            setEmail(e.target.value);
            if (error) setError(null);
          }}
          icon={Mail}
          required
        />

        <Select
          label="Assigned Administrative Role"
          options={ADMIN_ROLES}
          value={role}
          onChange={(e) => setRole(e.target.value)}
          icon={Shield}
        />

        <div className="p-3 bg-slate-50 border border-slate-100 rounded-xl text-xs text-slate-500">
          <span className="font-semibold text-slate-700 block mb-1">Security Notice:</span>
          Invitations expire automatically after 24 hours. The recipient will be required to configure their password and authenticate with 2FA.
        </div>

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
            icon={UserPlus}
            className="flex-1"
          >
            Send Invitation
          </Button>
        </div>
      </form>
    </Modal>
  );
}
