import React, { useState } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { ROUTES } from '../../constants/routes';
import AuthLayout from '../../layouts/AuthLayout';
import Input from '../../components/forms/Input';
import Button from '../../components/common/Button';
import Alert from '../../components/feedback/Alert';
import SuccessModal from '../../components/feedback/SuccessModal';
import { Shield, User, Lock, Phone, ArrowRight } from 'lucide-react';
import apiClient from '../../services/api/client';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * SuperAdmin Invitation Token Redemption Viewport.
 * Calls POST /api/v1/auth/admin/redeem-invite.
 */
export default function RedeemAdminInvitePage() {
  const { token } = useParams();
  const navigate = useNavigate();

  const [formData, setFormData] = useState({
    fullName: '',
    password: '',
    confirmPassword: '',
    phoneNumber: ''
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [isSuccessOpen, setIsSuccessOpen] = useState(false);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    if (error) setError(null);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!token) {
      setError('Invalid or missing administrative invitation token.');
      return;
    }

    if (!formData.fullName || !formData.password || !formData.confirmPassword) {
      setError('Please complete all required fields.');
      return;
    }

    if (formData.password.length < 8) {
      setError('Password must be at least 8 characters long.');
      return;
    }

    if (formData.password !== formData.confirmPassword) {
      setError('Passwords do not match.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await apiClient.post('/auth/admin/redeem-invite', {
        token,
        fullName: formData.fullName.trim(),
        password: formData.password,
        phoneNumber: formData.phoneNumber.trim() || null
      });

      if (response.succeeded) {
        setIsSuccessOpen(true);
      } else {
        setError(response.message || 'Failed to redeem administrative invitation.');
      }
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Invitation token is invalid, expired, or already used.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <AuthLayout
      title="Admin Invitation"
      subtitle="Complete your administrative profile to access the CebizPay control console"
      footer={
        <span>
          Already activated?{' '}
          <Link to={ROUTES.LOGIN} className="text-brand-600 font-semibold hover:underline">
            Sign In
          </Link>
        </span>
      }
    >
      <div className="flex items-center gap-2 p-3 bg-brand-50 rounded-xl border border-brand-100 text-xs text-brand-700 mb-2">
        <Shield size={16} className="shrink-0 text-brand-600" />
        <span>Administrative Console Security Onboarding</span>
      </div>

      {error && (
        <Alert variant="danger" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label="Full Legal Name"
          id="fullName"
          name="fullName"
          type="text"
          required
          placeholder="e.g. Adebayo Ogunlesi"
          value={formData.fullName}
          onChange={handleChange}
          icon={User}
        />

        <Input
          label="Mobile Phone Number"
          id="phoneNumber"
          name="phoneNumber"
          type="tel"
          placeholder="e.g. 08012345678"
          helperText="Used for two-factor security alerts."
          value={formData.phoneNumber}
          onChange={handleChange}
          icon={Phone}
        />

        <Input
          label="Set Password"
          id="password"
          name="password"
          type="password"
          required
          placeholder="••••••••••••"
          helperText="Must be at least 8 characters."
          value={formData.password}
          onChange={handleChange}
          icon={Lock}
        />

        <Input
          label="Confirm Password"
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
          icon={ArrowRight}
          iconPosition="right"
          className="w-full mt-2"
        >
          Activate Admin Account
        </Button>
      </form>

      <SuccessModal
        isOpen={isSuccessOpen}
        onClose={() => {
          setIsSuccessOpen(false);
          navigate(ROUTES.LOGIN);
        }}
        title="Account Activated"
        message="Your administrative profile has been initialized successfully. Please sign in to proceed."
        buttonText="Proceed to Sign In"
      />
    </AuthLayout>
  );
}
