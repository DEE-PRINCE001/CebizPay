import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ROUTES } from '../../constants/routes';
import { useAuth } from '../../context/AuthContext';
import AuthLayout from '../../layouts/AuthLayout';
import Input from '../../components/forms/Input';
import Button from '../../components/common/Button';
import Alert from '../../components/feedback/Alert';
import { Lock, Check } from 'lucide-react';
import apiClient from '../../services/api/client';
import { parseProblemDetails } from '../../utils/problemDetails';
import { useToast } from '../../hooks/useToast';

/**
 * Authenticated Password Change Page.
 * Calls POST /api/v1/auth/change-password.
 */
export default function ChangePasswordPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const { showSuccess } = useToast();

  const [formData, setFormData] = useState({
    currentPassword: '',
    newPassword: '',
    confirmPassword: ''
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    if (error) setError(null);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!formData.currentPassword || !formData.newPassword || !formData.confirmPassword) {
      setError('Please fill in all password fields.');
      return;
    }

    if (formData.newPassword.length < 8) {
      setError('New password must be at least 8 characters in length.');
      return;
    }

    if (formData.newPassword !== formData.confirmPassword) {
      setError('New password and confirmation password do not match.');
      return;
    }

    setLoading(true);
    setError(null);

    const userId = user?.userId || user?.id || '';

    try {
      const response = await apiClient.post('/auth/change-password', {
        userId,
        currentPassword: formData.currentPassword,
        newPassword: formData.newPassword,
        isMobile: false
      });

      if (response.succeeded) {
        showSuccess('Password updated successfully.');
        navigate(ROUTES.DASHBOARD);
      } else {
        setError(response.errorMessage || response.message || 'Failed to update password.');
      }
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to change password. Please check your current password.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <AuthLayout
      title="Change Password"
      subtitle="Ensure your account stays secure with a strong password"
      footer={
        <button
          type="button"
          onClick={() => navigate(-1)}
          className="text-xs text-slate-500 hover:text-slate-800"
        >
          Cancel and return
        </button>
      }
    >
      {error && (
        <Alert variant="danger" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label="Current Password"
          id="currentPassword"
          name="currentPassword"
          type="password"
          required
          placeholder="••••••••••••"
          value={formData.currentPassword}
          onChange={handleChange}
          icon={Lock}
        />

        <Input
          label="New Password"
          id="newPassword"
          name="newPassword"
          type="password"
          required
          placeholder="••••••••••••"
          helperText="Minimum 8 characters with letters, numbers, and symbols."
          value={formData.newPassword}
          onChange={handleChange}
          icon={Lock}
        />

        <Input
          label="Confirm New Password"
          id="confirmPassword"
          name="confirmPassword"
          type="password"
          required
          placeholder="••••••••••••"
          value={formData.confirmPassword}
          onChange={handleChange}
          icon={Lock}
        />

        <Button
          type="submit"
          variant="primary"
          size="md"
          loading={loading}
          icon={Check}
          className="w-full mt-2"
        >
          Update Password
        </Button>
      </form>
    </AuthLayout>
  );
}
