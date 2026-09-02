import React, { useState, useEffect } from 'react';
import { useLocation, useNavigate, Link } from 'react-router-dom';
import { ROUTES } from '../../constants/routes';
import { useAuth } from '../../context/AuthContext';
import AuthLayout from '../../layouts/AuthLayout';
import Button from '../../components/common/Button';
import Alert from '../../components/feedback/Alert';
import { ArrowLeft, CheckCircle2, RotateCw } from 'lucide-react';
import apiClient from '../../services/api/client';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Mobile OTP Verification Step 2.
 * Verifies code via POST /api/v1/auth/register/otp/verify and completes onboarding.
 */
export default function VerifyOtpPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const { handleAuthSuccess } = useAuth();

  const phoneNumber = location.state?.phoneNumber || '';
  const initialSessionId = location.state?.verificationSessionId || null;

  const [otpCode, setOtpCode] = useState('');
  const [sessionId, setSessionId] = useState(initialSessionId);
  const [loading, setLoading] = useState(false);
  const [resending, setResending] = useState(false);
  const [cooldown, setCooldown] = useState(60);
  const [error, setError] = useState(null);
  const [infoMessage, setInfoMessage] = useState(null);

  // Redirect to step 1 if no phone number provided in router state
  useEffect(() => {
    if (!phoneNumber) {
      navigate(ROUTES.REGISTER_PHONE, { replace: true });
    }
  }, [phoneNumber, navigate]);

  // Resend cooldown timer
  useEffect(() => {
    if (cooldown > 0) {
      const timer = setTimeout(() => setCooldown(cooldown - 1), 1000);
      return () => clearTimeout(timer);
    }
  }, [cooldown]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (otpCode.length < 6) {
      setError('Please enter the complete 6-digit verification code.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await apiClient.post('/auth/register/otp/verify', {
        phoneNumber,
        otpCode,
        verificationSessionId: sessionId
      });

      if (response.success) {
        if (response.accessToken) {
          handleAuthSuccess({
            userId: response.userId,
            accessToken: response.accessToken,
            refreshToken: response.refreshToken,
            email: phoneNumber,
            role: 'User'
          });
          navigate(ROUTES.DASHBOARD, { replace: true });
        } else {
          // If password setup or additional onboarding step is needed
          navigate(ROUTES.LOGIN, {
            replace: true,
            state: { message: 'Phone verified successfully! Please sign in.' }
          });
        }
      } else {
        setError(response.message || 'Verification failed. Please check the code.');
      }
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to verify registration code.');
    } finally {
      setLoading(false);
    }
  };

  const handleResend = async () => {
    if (cooldown > 0 || resending) return;

    setResending(true);
    setError(null);
    setInfoMessage(null);

    try {
      const response = await apiClient.post('/auth/register/phone', {
        phoneNumber
      });

      if (response.success) {
        if (response.verificationSessionId) {
          setSessionId(response.verificationSessionId);
        }
        setCooldown(60);
        setInfoMessage('A new verification code has been dispatched to your phone.');
      } else {
        setError(response.message || 'Failed to resend code.');
      }
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to resend verification code.');
    } finally {
      setResending(false);
    }
  };

  return (
    <AuthLayout
      title="Verify Mobile Number"
      subtitle={`Enter the 6-digit code sent to ${phoneNumber}`}
      footer={
        <div className="flex items-center justify-center gap-2">
          <Link to={ROUTES.REGISTER_PHONE} className="inline-flex items-center gap-1 text-slate-500 hover:text-slate-800 text-xs">
            <ArrowLeft size={13} />
            <span>Change phone number</span>
          </Link>
        </div>
      }
    >
      {error && (
        <Alert variant="danger" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {infoMessage && (
        <Alert variant="info" onClose={() => setInfoMessage(null)}>
          {infoMessage}
        </Alert>
      )}

      <form onSubmit={handleSubmit} className="space-y-6">
        <div>
          <input
            type="text"
            inputMode="numeric"
            autoFocus
            maxLength={6}
            value={otpCode}
            onChange={(e) => setOtpCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
            placeholder="000000"
            className="w-full h-14 text-center text-2xl font-bold tracking-[0.4em] rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-brand-600 focus:border-brand-600 transition-all bg-white"
          />
        </div>

        <div className="flex items-center justify-between text-xs text-slate-500">
          <span>Didn't receive code?</span>
          <button
            type="button"
            onClick={handleResend}
            disabled={cooldown > 0 || resending}
            className="text-brand-600 font-semibold hover:underline disabled:opacity-50 disabled:cursor-not-allowed inline-flex items-center gap-1"
          >
            {resending ? <RotateCw size={12} className="animate-spin" /> : null}
            <span>{cooldown > 0 ? `Resend code (${cooldown}s)` : 'Resend code'}</span>
          </button>
        </div>

        <Button
          type="submit"
          variant="primary"
          size="md"
          loading={loading}
          disabled={otpCode.length < 6}
          icon={CheckCircle2}
          className="w-full"
        >
          Verify & Continue
        </Button>
      </form>
    </AuthLayout>
  );
}
