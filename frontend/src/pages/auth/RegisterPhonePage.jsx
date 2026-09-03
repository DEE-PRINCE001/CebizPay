import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ROUTES } from '../../constants/routes';
import AuthLayout from '../../layouts/AuthLayout';
import Input from '../../components/forms/Input';
import Button from '../../components/common/Button';
import Alert from '../../components/feedback/Alert';
import { Phone, Mail, User, Lock, ArrowRight, CheckCircle2 } from 'lucide-react';
import apiClient from '../../services/api/client';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Normalizes user-inputted phone number to international E.164 format (+234...).
 */
function normalizeToInternational(phone) {
  if (!phone) return '';
  const digits = phone.replace(/[^\d+]/g, '');
  if (digits.startsWith('+234') && digits.length === 14) return digits;
  if (digits.startsWith('234') && digits.length === 13) return `+${digits}`;
  if (digits.startsWith('0') && digits.length === 11) return `+234${digits.slice(1)}`;
  if (digits.length === 10) return `+234${digits}`;
  return digits.startsWith('+') ? digits : `+${digits}`;
}

function getOrGenerateDeviceId() {
  try {
    let deviceId = localStorage.getItem('cebizpay_device_id');
    if (!deviceId) {
      deviceId = 'web-' + Math.random().toString(36).substring(2, 12) + '-' + Date.now().toString(36);
      localStorage.setItem('cebizpay_device_id', deviceId);
    }
    return deviceId;
  } catch {
    return 'web-client-device';
  }
}

/**
 * Mobile Phone & Account Registration.
 * Initiates phone OTP verification via POST /api/v1/auth/register/phone.
 */
export default function RegisterPhonePage() {
  const navigate = useNavigate();

  const [formData, setFormData] = useState({
    firstName: '',
    lastName: '',
    email: '',
    phone: '',
    password: '',
    confirmPassword: ''
  });

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    if (error) setError(null);
  };

  const validatePassword = (pwd) => {
    if (!pwd || pwd.length < 7) return 'Password must be at least 7 characters.';
    if (!/[A-Z]/.test(pwd)) return 'Password must contain at least one uppercase letter.';
    if (!/[a-z]/.test(pwd)) return 'Password must contain at least one lowercase letter.';
    if (!/[0-9]/.test(pwd)) return 'Password must contain at least one number.';
    if (!/[\W_]/.test(pwd)) return 'Password must contain at least one special symbol (!@#$%^&*).';
    return null;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!formData.firstName.trim()) {
      setError('First name is required.');
      return;
    }

    if (!formData.lastName.trim()) {
      setError('Last name is required.');
      return;
    }

    if (!formData.email.trim() || !formData.email.includes('@')) {
      setError('A valid email address is required.');
      return;
    }

    const normalizedPhone = normalizeToInternational(formData.phone);
    if (!normalizedPhone || normalizedPhone.length < 11) {
      setError('Please enter a valid Nigerian mobile phone number (e.g. 08012345678).');
      return;
    }

    const pwdErr = validatePassword(formData.password);
    if (pwdErr) {
      setError(pwdErr);
      return;
    }

    if (formData.password !== formData.confirmPassword) {
      setError('Password and confirmation do not match.');
      return;
    }

    setLoading(true);
    setError(null);

    const deviceId = getOrGenerateDeviceId();

    try {
      const response = await apiClient.post('/auth/register/phone', {
        phone: normalizedPhone,
        deviceId
      });

      if (response.success) {
        navigate(ROUTES.VERIFY_OTP, {
          state: {
            phone: normalizedPhone,
            email: formData.email.trim(),
            firstName: formData.firstName.trim(),
            lastName: formData.lastName.trim(),
            password: formData.password,
            devOtpCode: response.otpCode
          }
        });
      } else {
        setError(response.message || 'Failed to send verification code.');
      }
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to initiate registration.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <AuthLayout
      title="Create Account"
      subtitle="Register your corporate identity or individual wallet on CebizPay"
      footer={
        <span>
          Already registered?{' '}
          <Link to={ROUTES.LOGIN} className="text-brand-600 font-semibold hover:underline">
            Sign In to Portal
          </Link>
        </span>
      }
    >
      {error && (
        <Alert variant="danger" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <form onSubmit={handleSubmit} className="space-y-3.5">
        <div className="grid grid-cols-2 gap-3">
          <Input
            label="First Name"
            id="firstName"
            name="firstName"
            type="text"
            required
            placeholder="Adebayo"
            value={formData.firstName}
            onChange={handleChange}
            icon={User}
          />
          <Input
            label="Last Name"
            id="lastName"
            name="lastName"
            type="text"
            required
            placeholder="Ogunlesi"
            value={formData.lastName}
            onChange={handleChange}
            icon={User}
          />
        </div>

        <Input
          label="Corporate / Personal Email"
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

        <Input
          label="Mobile Phone Number"
          id="phone"
          name="phone"
          type="tel"
          required
          placeholder="08012345678"
          helperText="We will send a 6-digit SMS verification code to this phone."
          value={formData.phone}
          onChange={handleChange}
          icon={Phone}
        />

        <div className="grid grid-cols-2 gap-3">
          <Input
            label="Password"
            id="password"
            name="password"
            type="password"
            autoComplete="new-password"
            required
            placeholder="••••••••••••"
            value={formData.password}
            onChange={handleChange}
            icon={Lock}
          />
          <Input
            label="Confirm Password"
            id="confirmPassword"
            name="confirmPassword"
            type="password"
            autoComplete="new-password"
            required
            placeholder="••••••••••••"
            value={formData.confirmPassword}
            onChange={handleChange}
            icon={Lock}
          />
        </div>

        <p className="text-[11px] text-slate-500">
          Must be at least 7 characters and contain uppercase, lowercase, numbers, and symbols.
        </p>

        <Button
          type="submit"
          variant="primary"
          size="md"
          loading={loading}
          icon={ArrowRight}
          iconPosition="right"
          className="w-full mt-2"
        >
          Continue to Verification
        </Button>
      </form>
    </AuthLayout>
  );
}
