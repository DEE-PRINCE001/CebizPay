import React, { useState } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { ROUTES } from '../../constants/routes';
import AuthLayout from '../../layouts/AuthLayout';
import Input from '../../components/forms/Input';
import Button from '../../components/common/Button';
import Alert from '../../components/feedback/Alert';
import MfaVerifyModal from './MfaVerifyModal';
import { Lock, Mail, ArrowRight } from 'lucide-react';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Standard Corporate & Individual Login Viewport.
 * Matches Log In.png (D264).
 */
export default function LoginPage() {
  const { login, verifyMfa, mfaChallenge, cancelMfa } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [formData, setFormData] = useState({
    email: '',
    password: ''
  });
  const [loading, setLoading] = useState(false);
  const [mfaLoading, setMfaLoading] = useState(false);
  const [error, setError] = useState(null);
  const [mfaError, setMfaError] = useState(null);

  const redirectTarget = location.state?.from?.pathname || ROUTES.DASHBOARD;

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    if (error) setError(null);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!formData.email || !formData.password) {
      setError('Please enter both email and password.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const result = await login(formData.email, formData.password);
      if (!result.requiresMfa) {
        // Direct login success
        if (result.user?.role === 'SuperAdmin') {
          navigate(ROUTES.ADMIN_DASHBOARD, { replace: true });
        } else {
          navigate(redirectTarget, { replace: true });
        }
      }
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Invalid credentials or login failed.');
    } finally {
      setLoading(false);
    }
  };

  const handleMfaVerify = async (code) => {
    setMfaLoading(true);
    setMfaError(null);

    try {
      const userData = await verifyMfa(code);
      if (userData?.role === 'SuperAdmin') {
        navigate(ROUTES.ADMIN_DASHBOARD, { replace: true });
      } else {
        navigate(redirectTarget, { replace: true });
      }
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setMfaError(parsed.message || 'Invalid MFA code. Please try again.');
    } finally {
      setMfaLoading(false);
    }
  };

  return (
    <AuthLayout
      title="Welcome Back"
      subtitle="Sign in to your CebizPay corporate portal or individual wallet"
      footer={
        <span>
          Don't have an account yet?{' '}
          <Link to={ROUTES.REGISTER_PHONE} className="text-brand-600 font-semibold hover:underline">
            Register with Phone
          </Link>
        </span>
      }
    >
      {error && (
        <Alert variant="danger" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label="Email Address"
          id="email"
          name="email"
          type="email"
          autoComplete="email"
          required
          placeholder="name@company.com"
          value={formData.email}
          onChange={handleChange}
          icon={Mail}
        />

        <div className="space-y-1">
          <Input
            label="Password"
            id="password"
            name="password"
            type="password"
            autoComplete="current-password"
            required
            placeholder="••••••••••••"
            value={formData.password}
            onChange={handleChange}
            icon={Lock}
          />
          <div className="flex justify-end pt-1">
            <Link
              to={ROUTES.FORGOT_PASSWORD}
              className="text-xs text-brand-600 hover:underline font-medium"
            >
              Forgot password?
            </Link>
          </div>
        </div>

        <Button
          type="submit"
          variant="primary"
          size="md"
          loading={loading}
          icon={ArrowRight}
          iconPosition="right"
          className="w-full mt-2"
        >
          Sign In
        </Button>
      </form>

      {/* MFA Challenge Modal */}
      <MfaVerifyModal
        isOpen={!!mfaChallenge}
        onClose={cancelMfa}
        onVerify={handleMfaVerify}
        email={mfaChallenge?.email}
        loading={mfaLoading}
        error={mfaError}
      />
    </AuthLayout>
  );
}
