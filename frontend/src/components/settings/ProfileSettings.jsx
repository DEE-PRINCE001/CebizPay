import React, { useState } from 'react';
import Card from '../common/Card';
import Input from '../forms/Input';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { User, Mail, Phone, Building2, Shield, Save } from 'lucide-react';
import { useAuth } from '../../context/AuthContext';
import { useOrg } from '../../context/OrgContext';
import { useToast } from '../../hooks/useToast';

/**
 * User personal and corporate profile settings card.
 */
export default function ProfileSettings({ className = '' }) {
  const { user, updateUserData } = useAuth();
  const { currentOrg } = useOrg();
  const { showSuccess } = useToast();

  const [firstName, setFirstName] = useState(user?.firstName || '');
  const [lastName, setLastName] = useState(user?.lastName || '');
  const [phone, setPhone] = useState(user?.phoneNumber || '');
  const [loading, setLoading] = useState(false);
  const [saved, setSaved] = useState(false);

  const fullName = `${firstName} ${lastName}`.trim() || user?.fullName || 'User';
  const initials = fullName
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((n) => n[0].toUpperCase())
    .join('') || 'U';

  const handleSave = (e) => {
    e.preventDefault();
    setLoading(true);

    setTimeout(() => {
      updateUserData({
        firstName,
        lastName,
        fullName: `${firstName} ${lastName}`.trim(),
        phoneNumber: phone
      });
      setLoading(false);
      setSaved(true);
      showSuccess('Profile updated successfully.');
      setTimeout(() => setSaved(false), 3000);
    }, 400);
  };

  return (
    <Card padding="p-6" className={`bg-white border border-slate-200/80 ${className}`}>
      <div className="flex items-center gap-4 pb-6 mb-6 border-b border-slate-100">
        <div className="w-16 h-16 rounded-full bg-slate-900 text-white flex items-center justify-center font-bold text-xl shrink-0">
          {initials}
        </div>
        <div className="min-w-0">
          <h3 className="text-lg font-bold text-slate-900 truncate">{fullName}</h3>
          <p className="text-xs text-slate-500 truncate">{user?.email}</p>
          <div className="inline-flex items-center gap-1 mt-1 text-[11px] font-semibold text-brand-700 bg-brand-50 px-2 py-0.5 rounded-full">
            <Shield size={11} />
            <span>Role: {user?.role || 'Member'}</span>
          </div>
        </div>
      </div>

      {saved && (
        <Alert variant="success" className="mb-4">
          Profile changes saved successfully.
        </Alert>
      )}

      <form onSubmit={handleSave} className="space-y-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <Input
            label="First Name"
            value={firstName}
            onChange={(e) => setFirstName(e.target.value)}
            icon={User}
            required
          />
          <Input
            label="Last Name"
            value={lastName}
            onChange={(e) => setLastName(e.target.value)}
            icon={User}
            required
          />
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <Input
            label="Corporate Email Address"
            type="email"
            value={user?.email || ''}
            icon={Mail}
            disabled
            helperText="Email is managed by your workspace identity provider."
          />
          <Input
            label="Phone Number"
            type="tel"
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            icon={Phone}
            placeholder="0801 234 5678"
          />
        </div>

        <div className="p-3 bg-slate-50 border border-slate-100 rounded-xl flex items-center justify-between text-xs text-slate-600">
          <div className="flex items-center gap-2">
            <Building2 size={16} className="text-brand-600" />
            <span>Active Organization: <strong className="text-slate-900">{currentOrg?.name || 'Personal Workspace'}</strong></span>
          </div>
          <span className="font-mono text-[11px] text-slate-400">
            {user?.userId ? `ID: ${user.userId.slice(0, 8)}...` : ''}
          </span>
        </div>

        <div className="flex justify-end pt-2">
          <Button
            type="submit"
            variant="primary"
            size="md"
            loading={loading}
            icon={Save}
          >
            Save Profile Changes
          </Button>
        </div>
      </form>
    </Card>
  );
}
