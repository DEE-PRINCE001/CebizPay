import React, { useState } from 'react';
import Modal from '../common/Modal';
import Tabs from '../common/Tabs';
import Input from '../forms/Input';
import Select from '../forms/Select';
import Textarea from '../forms/Textarea';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { UserPlus, Mail, Users, Check } from 'lucide-react';
import apiClient from '../../services/api/client';
import { parseProblemDetails } from '../../utils/problemDetails';
import { useToast } from '../../hooks/useToast';

/**
 * Staff onboarding and invitation modal.
 */
export default function AddStaffModal({
  isOpen,
  onClose,
  departments = [],
  roles = [],
  salaryLevels = [],
  onSuccess
}) {
  const { showSuccess } = useToast();
  const [activeTab, setActiveTab] = useState('direct'); // 'direct' | 'invite' | 'bulk'

  // Direct Onboard Form State
  const [directForm, setDirectForm] = useState({
    email: '',
    firstName: '',
    lastName: '',
    phoneNumber: '',
    departmentId: '',
    workforceRoleId: '',
    salaryLevelId: '',
    role: 'Member'
  });

  // Invite Form State
  const [inviteEmail, setInviteEmail] = useState('');
  const [bulkEmails, setBulkEmails] = useState('');

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const tabs = [
    { id: 'direct', label: 'Direct Onboard', icon: UserPlus },
    { id: 'invite', label: 'Email Invite', icon: Mail },
    { id: 'bulk', label: 'Bulk Invites', icon: Users }
  ];

  const handleDirectChange = (e) => {
    const { name, value } = e.target;
    setDirectForm((prev) => ({ ...prev, [name]: value }));
    if (error) setError(null);
  };

  const handleDirectSubmit = async (e) => {
    e.preventDefault();
    if (!directForm.email || !directForm.firstName || !directForm.lastName) {
      setError('Please provide email, first name, and last name.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      await apiClient.post('/org/staff/create', {
        email: directForm.email.trim(),
        firstName: directForm.firstName.trim(),
        lastName: directForm.lastName.trim(),
        phoneNumber: directForm.phoneNumber.trim() || null,
        departmentId: directForm.departmentId || null,
        workforceRoleId: directForm.workforceRoleId || null,
        salaryLevelId: directForm.salaryLevelId || null,
        role: directForm.role || 'Member'
      });

      showSuccess(`Staff member ${directForm.firstName} onboarded successfully.`);
      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to onboard staff member.');
    } finally {
      setLoading(false);
    }
  };

  const handleInviteSubmit = async (e) => {
    e.preventDefault();
    if (!inviteEmail.trim()) {
      setError('Please enter an email address.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      await apiClient.post('/org/staff/invite', { email: inviteEmail.trim() });
      showSuccess(`Staff invitation sent to ${inviteEmail}.`);
      setInviteEmail('');
      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to send invitation.');
    } finally {
      setLoading(false);
    }
  };

  const handleBulkSubmit = async (e) => {
    e.preventDefault();
    const emailsList = bulkEmails
      .split(/[\n,;]/)
      .map((e) => e.trim())
      .filter((e) => e.length > 3 && e.includes('@'));

    if (emailsList.length === 0) {
      setError('Please provide at least one valid email address.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const res = await apiClient.post('/org/staff/invite-bulk', { emails: emailsList });
      showSuccess(`Sent ${res?.successfulCount || emailsList.length} staff invitations.`);
      setBulkEmails('');
      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to send bulk invitations.');
    } finally {
      setLoading(false);
    }
  };

  const departmentOptions = [
    { value: '', label: 'Select Department (Optional)' },
    ...departments.map((d) => ({ value: d.id, label: d.name }))
  ];

  const roleOptions = [
    { value: '', label: 'Select Workforce Role (Optional)' },
    ...roles.map((r) => ({ value: r.id, label: r.title }))
  ];

  const salaryOptions = [
    { value: '', label: 'Select Salary Level (Optional)' },
    ...salaryLevels.map((s) => ({
      value: s.id,
      label: `${s.levelName} — ₦${(s.baseAmount || 0).toLocaleString()}`
    }))
  ];

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Add Workforce Staff"
      subtitle="Onboard employees or send workspace invitation links"
      maxWidth="max-w-lg"
    >
      <div className="space-y-4 pt-1">
        <Tabs
          variant="segmented"
          tabs={tabs}
          activeTab={activeTab}
          onChange={(t) => {
            setActiveTab(t);
            setError(null);
          }}
          className="w-full"
        />

        {error && (
          <Alert variant="danger" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        {/* Tab 1: Direct Onboarding */}
        {activeTab === 'direct' && (
          <form onSubmit={handleDirectSubmit} className="space-y-3.5">
            <div className="grid grid-cols-2 gap-3">
              <Input
                label="First Name"
                name="firstName"
                value={directForm.firstName}
                onChange={handleDirectChange}
                required
              />
              <Input
                label="Last Name"
                name="lastName"
                value={directForm.lastName}
                onChange={handleDirectChange}
                required
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <Input
                label="Corporate Email"
                type="email"
                name="email"
                value={directForm.email}
                onChange={handleDirectChange}
                required
              />
              <Input
                label="Phone Number"
                type="tel"
                name="phoneNumber"
                placeholder="080..."
                value={directForm.phoneNumber}
                onChange={handleDirectChange}
              />
            </div>

            <Select
              label="Assigned Department"
              name="departmentId"
              options={departmentOptions}
              value={directForm.departmentId}
              onChange={handleDirectChange}
            />

            <div className="grid grid-cols-2 gap-3">
              <Select
                label="Workforce Role"
                name="workforceRoleId"
                options={roleOptions}
                value={directForm.workforceRoleId}
                onChange={handleDirectChange}
              />
              <Select
                label="Salary Level"
                name="salaryLevelId"
                options={salaryOptions}
                value={directForm.salaryLevelId}
                onChange={handleDirectChange}
              />
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
                Onboard Employee
              </Button>
            </div>
          </form>
        )}

        {/* Tab 2: Single Email Invite */}
        {activeTab === 'invite' && (
          <form onSubmit={handleInviteSubmit} className="space-y-4 pt-1">
            <Input
              label="Staff Email Address"
              type="email"
              placeholder="employee@company.com"
              value={inviteEmail}
              onChange={(e) => {
                setInviteEmail(e.target.value);
                if (error) setError(null);
              }}
              icon={Mail}
              helperText="An onboarding link will be emailed allowing the employee to activate their account."
              required
            />

            <div className="flex items-center gap-3 pt-2">
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
                icon={Mail}
                className="flex-1"
              >
                Send Invitation
              </Button>
            </div>
          </form>
        )}

        {/* Tab 3: Bulk Email Invites */}
        {activeTab === 'bulk' && (
          <form onSubmit={handleBulkSubmit} className="space-y-4 pt-1">
            <Textarea
              label="Staff Email Addresses"
              rows={5}
              placeholder="Paste email addresses separated by commas or line breaks..."
              value={bulkEmails}
              onChange={(e) => {
                setBulkEmails(e.target.value);
                if (error) setError(null);
              }}
              helperText="Format: john@company.com, jane@company.com"
              required
            />

            <div className="flex items-center gap-3 pt-2">
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
                icon={Users}
                className="flex-1"
              >
                Send Bulk Invites
              </Button>
            </div>
          </form>
        )}
      </div>
    </Modal>
  );
}
