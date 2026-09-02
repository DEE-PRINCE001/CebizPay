import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ROUTES } from '../../constants/routes';
import AuthLayout from '../../layouts/AuthLayout';
import Input from '../../components/forms/Input';
import Button from '../../components/common/Button';
import Alert from '../../components/feedback/Alert';
import { Phone, ArrowRight } from 'lucide-react';
import apiClient from '../../services/api/client';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Mobile Phone Registration Step 1.
 * Initiates phone OTP verification via POST /api/v1/auth/register/phone.
 */
export default function RegisterPhonePage() {
  const navigate = useNavigate();
  const [phoneNumber, setPhoneNumber] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!phoneNumber.trim()) {
      setError('Please enter a valid mobile phone number.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await apiClient.post('/auth/register/phone', {
        phoneNumber: phoneNumber.trim()
      });

      if (response.success) {
        navigate(ROUTES.VERIFY_OTP, {
          state: {
            phoneNumber: phoneNumber.trim(),
            verificationSessionId: response.verificationSessionId
          }
        });
      } else {
        setError(response.message || 'Failed to send verification code.');
      }
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to initiate phone registration.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <AuthLayout
      title="Create Account"
      subtitle="Enter your mobile phone number to receive a one-time registration code"
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

      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label="Mobile Phone Number"
          id="phoneNumber"
          name="phoneNumber"
          type="tel"
          required
          placeholder="e.g. 08012345678 or +234..."
          helperText="We will send a 6-digit SMS verification code to this number."
          value={phoneNumber}
          onChange={(e) => {
            setPhoneNumber(e.target.value);
            if (error) setError(null);
          }}
          icon={Phone}
        />

        <Button
          type="submit"
          variant="primary"
          size="md"
          loading={loading}
          disabled={!phoneNumber.trim()}
          icon={ArrowRight}
          iconPosition="right"
          className="w-full mt-2"
        >
          Send Verification Code
        </Button>
      </form>
    </AuthLayout>
  );
}
