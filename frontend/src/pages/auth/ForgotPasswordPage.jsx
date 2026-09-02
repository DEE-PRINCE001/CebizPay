import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { ROUTES } from '../../constants/routes';
import AuthLayout from '../../layouts/AuthLayout';
import Input from '../../components/forms/Input';
import Button from '../../components/common/Button';
import Alert from '../../components/feedback/Alert';
import { Mail, ArrowLeft, Send } from 'lucide-react';

/**
 * Forgot Password Request Page.
 */
export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [loading, setLoading] = useState(false);

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!email.trim()) return;

    setLoading(true);
    // Standard secure acknowledgment
    setTimeout(() => {
      setLoading(false);
      setSubmitted(true);
    }, 600);
  };

  return (
    <AuthLayout
      title="Reset Password"
      subtitle="Enter your registered account email to receive recovery instructions"
      footer={
        <Link to={ROUTES.LOGIN} className="inline-flex items-center gap-1.5 text-brand-600 font-semibold hover:underline">
          <ArrowLeft size={14} />
          <span>Back to Sign In</span>
        </Link>
      }
    >
      {submitted ? (
        <div className="space-y-4 text-center py-2">
          <div className="w-12 h-12 rounded-full bg-emerald-50 text-emerald-600 flex items-center justify-center mx-auto ring-8 ring-emerald-50/50">
            <Mail size={22} />
          </div>
          <h3 className="text-base font-bold text-slate-900">Check Your Inbox</h3>
          <p className="text-xs text-slate-500 leading-relaxed">
            If an account exists for <span className="font-semibold text-slate-700">{email}</span>, we have sent password reset instructions to your address.
          </p>
          <div className="pt-2">
            <Link
              to={ROUTES.LOGIN}
              className="inline-block w-full py-2.5 px-4 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium rounded-full transition shadow-xs"
            >
              Return to Login
            </Link>
          </div>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="space-y-4">
          <Input
            label="Registered Email Address"
            id="email"
            name="email"
            type="email"
            required
            placeholder="name@company.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            icon={Mail}
          />

          <Button
            type="submit"
            variant="primary"
            size="md"
            loading={loading}
            icon={Send}
            className="w-full mt-2"
          >
            Send Recovery Link
          </Button>
        </form>
      )}
    </AuthLayout>
  );
}
