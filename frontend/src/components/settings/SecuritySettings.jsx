import React, { useState } from 'react';
import Card from '../common/Card';
import Input from '../forms/Input';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { Lock, KeyRound, Shield, Check, AlertCircle, Smartphone } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Security and authentication settings card.
 */
export default function SecuritySettings({ className = '' }) {
  const { showSuccess } = useToast();

  // Change Password State
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [pwdLoading, setPwdLoading] = useState(false);
  const [pwdError, setPwdError] = useState(null);
  const [pwdSuccess, setPwdSuccess] = useState(false);

  // MFA State
  const [mfaEnabled, setMfaEnabled] = useState(false);
  const [mfaLoading, setMfaLoading] = useState(false);
  const [mfaError, setMfaError] = useState(null);

  const handlePasswordSubmit = async (e) => {
    e.preventDefault();

    if (newPassword.length < 8) {
      setPwdError('New password must be at least 8 characters long.');
      return;
    }

    if (newPassword !== confirmPassword) {
      setPwdError('New password and confirmation do not match.');
      return;
    }

    setPwdLoading(true);
    setPwdError(null);
    setPwdSuccess(false);

    const userId = user?.userId || user?.id || '';

    try {
      await apiClient.post('/auth/change-password', {
        userId,
        currentPassword,
        newPassword,
        isMobile: false
      });

      setPwdSuccess(true);
      showSuccess('Password changed successfully.');
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setPwdError(parsed.message || 'Password update failed. Please check your current password.');
    } finally {
      setPwdLoading(false);
    }
  };

  const handleToggleMfa = async () => {
    setMfaLoading(true);
    setMfaError(null);

    const userId = user?.userId || user?.id || '';

    try {
      const targetState = !mfaEnabled;
      await apiClient.post('/auth/mfa/toggle', {
        userId,
        enable: targetState
      });

      setMfaEnabled(targetState);
      showSuccess(`Two-Factor Authentication ${targetState ? 'enabled' : 'disabled'}.`);
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setMfaError(parsed.message || 'Failed to update MFA settings.');
    } finally {
      setMfaLoading(false);
    }
  };

  return (
    <div className={`space-y-6 ${className}`}>
      {/* 1. Change Password Card */}
      <Card padding="p-6" className="bg-white border border-slate-200/80">
        <div className="flex items-center gap-3 pb-4 mb-4 border-b border-slate-100">
          <div className="w-10 h-10 rounded-2xl bg-brand-50 text-brand-600 flex items-center justify-center">
            <Lock size={20} />
          </div>
          <div>
            <h3 className="text-base font-bold text-slate-900">Change Password</h3>
            <p className="text-xs text-slate-500">Update your account login credentials</p>
          </div>
        </div>

        {pwdError && (
          <Alert variant="danger" onClose={() => setPwdError(null)} className="mb-4">
            {pwdError}
          </Alert>
        )}

        {pwdSuccess && (
          <Alert variant="success" onClose={() => setPwdSuccess(false)} className="mb-4">
            Your password has been changed successfully.
          </Alert>
        )}

        <form onSubmit={handlePasswordSubmit} className="space-y-4">
          <Input
            label="Current Password"
            type="password"
            value={currentPassword}
            onChange={(e) => {
              setCurrentPassword(e.target.value);
              if (pwdError) setPwdError(null);
            }}
            icon={KeyRound}
            required
          />

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <Input
              label="New Password"
              type="password"
              value={newPassword}
              onChange={(e) => {
                setNewPassword(e.target.value);
                if (pwdError) setPwdError(null);
              }}
              icon={Lock}
              required
            />
            <Input
              label="Confirm New Password"
              type="password"
              value={confirmPassword}
              onChange={(e) => {
                setConfirmPassword(e.target.value);
                if (pwdError) setPwdError(null);
              }}
              icon={Lock}
              required
            />
          </div>

          <div className="p-3 bg-slate-50 border border-slate-100 rounded-xl text-xs text-slate-500 space-y-1">
            <span className="font-semibold text-slate-700 block">Password Requirements:</span>
            <ul className="list-disc list-inside space-y-0.5 text-[11px]">
              <li>Minimum 8 characters in length</li>
              <li>Must contain numbers and special characters</li>
              <li>Cannot reuse any of your last 3 passwords</li>
            </ul>
          </div>

          <div className="flex justify-end pt-2">
            <Button
              type="submit"
              variant="primary"
              size="md"
              loading={pwdLoading}
              icon={Lock}
            >
              Update Password
            </Button>
          </div>
        </form>
      </Card>

      {/* 2. Two-Factor Authentication (MFA) Card */}
      <Card padding="p-6" className="bg-white border border-slate-200/80">
        <div className="flex items-center justify-between pb-4 mb-4 border-b border-slate-100">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-2xl bg-purple-50 text-purple-600 flex items-center justify-center">
              <Smartphone size={20} />
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-900">Two-Factor Authentication (2FA)</h3>
              <p className="text-xs text-slate-500">Require an authenticator code when signing in</p>
            </div>
          </div>

          <button
            type="button"
            onClick={handleToggleMfa}
            disabled={mfaLoading}
            className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-hidden ${
              mfaEnabled ? 'bg-brand-600' : 'bg-slate-200'
            }`}
          >
            <span
              className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow-sm ring-0 transition duration-200 ease-in-out ${
                mfaEnabled ? 'translate-x-5' : 'translate-x-0'
              }`}
            />
          </button>
        </div>

        {mfaError && (
          <Alert variant="danger" onClose={() => setMfaError(null)} className="mb-4">
            {mfaError}
          </Alert>
        )}

        <div className="text-xs text-slate-600 leading-relaxed space-y-2">
          <p>
            Two-factor authentication adds an extra layer of security to your corporate account by requiring access to your phone in addition to your password.
          </p>
          <div className="flex items-center gap-2 text-slate-700 font-semibold pt-1">
            <Shield size={14} className="text-brand-600" />
            <span>Status: {mfaEnabled ? 'Active and Enforced' : 'Disabled (Standard Password)'}</span>
          </div>
        </div>
      </Card>
    </div>
  );
}
