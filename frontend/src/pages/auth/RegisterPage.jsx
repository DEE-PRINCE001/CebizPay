import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth, ROLES } from '../../context/AuthContext';
import { useToast } from '../../context/ToastContext';
import { authApi } from '../../api/authApi';
import PhoneInput from '../../components/common/PhoneInput';
import { Smartphone, Lock, User, Mail, ArrowRight, ShieldCheck, CheckCircle2, AlertCircle } from 'lucide-react';

export default function RegisterPage() {
  const [step, setStep] = useState(1); // 1: Phone, 2: OTP & Profile
  const [phone, setPhone] = useState('+2348099883344');
  const [otpCode, setOtpCode] = useState('');
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('ConsumerPass123!');
  const [formError, setFormError] = useState(null);
  const [isLoading, setIsLoading] = useState(false);

  const { setUser, setActiveRole } = useAuth();
  const { showSuccess, showError } = useToast();
  const navigate = useNavigate();

  const handleSendOtp = async (e) => {
    e.preventDefault();
    setFormError(null);
    setIsLoading(true);

    try {
      const res = await authApi.registerPhone(phone);
      if (res && res.success) {
        if (res.otpCode) {
          setOtpCode(res.otpCode);
          showSuccess('OTP Dispatched', `Development OTP: ${res.otpCode}`);
        } else {
          showSuccess('OTP Dispatched', 'Verification code sent to your mobile device.');
        }
        setStep(2);
      } else {
        const err = res?.message || 'Failed to dispatch verification OTP.';
        setFormError(err);
        showError('OTP Request Failed', err);
      }
    } catch (err) {
      const errMsg = err.message || 'Unable to send verification code. Please try again.';
      setFormError(errMsg);
      showError('OTP Request Failed', errMsg);
    } finally {
      setIsLoading(false);
    }
  };

  const handleCompleteRegistration = async (e) => {
    e.preventDefault();
    setFormError(null);
    setIsLoading(true);

    try {
      const res = await authApi.verifyOtp({
        phone,
        code: otpCode,
        email,
        password,
        firstName,
        lastName,
      });

      if (res && res.success) {
        if (res.accessToken) {
          localStorage.setItem('cebizpay_token', res.accessToken);
        }
        if (res.refreshToken) {
          localStorage.setItem('cebizpay_refresh_token', res.refreshToken);
        }

        setUser({
          id: res.userId,
          name: `${firstName} ${lastName}`,
          email,
          phone,
          roles: ['Consumer'],
        });
        setActiveRole(ROLES.CONSUMER);
        showSuccess('Registration Complete', `Welcome to CebizPay, ${firstName}! Your wallet is ready.`);
        navigate('/consumer');
      } else {
        const errorList = res?.errors?.join(', ') || 'Registration failed. Please check the information provided.';
        setFormError(errorList);
        showError('Registration Failed', errorList);
      }
    } catch (err) {
      const errMsg = err.message || 'OTP verification failed. Please ensure the code is correct and not expired.';
      setFormError(errMsg);
      showError('Registration Failed', errMsg);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="bg-white rounded-3xl border border-slate-200/80 p-8 sm:p-10 shadow-xl max-w-md w-full mx-auto text-left">
      <div className="text-center mb-6">
        <h2 className="text-2xl font-bold text-slate-900 tracking-tight">
          {step === 1 ? 'Create Personal Account' : 'Verify & Set Password'}
        </h2>
        <p className="text-xs text-slate-500 mt-1">
          {step === 1 ? 'Get an instant Dedicated Virtual Account & Wallet' : `Enter the 6-digit OTP code sent to ${phone}`}
        </p>
      </div>

      {/* Prominent Error Banner */}
      {formError && (
        <div className="mb-5 p-3.5 bg-rose-50 rounded-2xl border border-rose-200 text-rose-900 flex items-start gap-2.5 text-xs animate-in fade-in">
          <AlertCircle className="w-4 h-4 text-rose-600 shrink-0 mt-0.5" />
          <div className="flex-1 min-w-0">
            <span className="font-bold block">Action Required</span>
            <span className="text-[11px] leading-relaxed text-rose-700">{formError}</span>
          </div>
        </div>
      )}

      {step === 1 ? (
        <form onSubmit={handleSendOtp} className="space-y-4 text-xs">
          {/* Reusable International Phone Input */}
          <PhoneInput
            label="Mobile Phone Number"
            required
            value={phone}
            onChange={setPhone}
          />

          <div className="p-3 bg-slate-50 rounded-xl border border-slate-100 text-slate-500 leading-relaxed text-[11px]">
            Protected by Redis rate limiting (max 3 requests per 15 minutes) and NIBSS KYC Tier 1 validation.
          </div>

          <button
            type="submit"
            disabled={isLoading}
            className="w-full py-3 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-xl shadow-xs transition-all flex items-center justify-center gap-2 disabled:opacity-50 cursor-pointer"
          >
            {isLoading ? (
              <span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
            ) : (
              <>
                <span>Send Verification OTP</span>
                <ArrowRight className="w-4 h-4" />
              </>
            )}
          </button>
        </form>
      ) : (
        <form onSubmit={handleCompleteRegistration} className="space-y-3.5 text-xs">
          <div>
            <div className="flex justify-between items-center mb-1">
              <label className="font-semibold text-slate-700">6-Digit OTP Code</label>
              <button
                type="button"
                onClick={() => setStep(1)}
                className="text-[11px] font-semibold text-blue-600 hover:underline cursor-pointer"
              >
                Change Phone
              </button>
            </div>
            <input
              type="text"
              required
              maxLength={6}
              value={otpCode}
              onChange={(e) => setOtpCode(e.target.value)}
              placeholder="e.g. 747862"
              className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-mono text-center text-lg font-bold tracking-widest focus:border-blue-600 focus:ring-2 focus:ring-blue-500/20 outline-hidden"
            />
          </div>

          <div className="grid grid-cols-2 gap-2">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">First Name</label>
              <input
                type="text"
                required
                value={firstName}
                onChange={(e) => setFirstName(e.target.value)}
                placeholder="Amina"
                className="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl font-medium focus:border-blue-600 outline-hidden"
              />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Last Name</label>
              <input
                type="text"
                required
                value={lastName}
                onChange={(e) => setLastName(e.target.value)}
                placeholder="Adeleke"
                className="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl font-medium focus:border-blue-600 outline-hidden"
              />
            </div>
          </div>

          <div>
            <label className="block font-semibold text-slate-700 mb-1">Email Address</label>
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="amina@example.com"
              className="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl font-medium focus:border-blue-600 outline-hidden"
            />
          </div>

          <div>
            <label className="block font-semibold text-slate-700 mb-1">Create Password</label>
            <input
              type="password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
              className="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl font-medium focus:border-blue-600 outline-hidden"
            />
            <span className="text-[10px] text-slate-400 mt-0.5 block">
              Must include 8+ chars, uppercase, number, &amp; symbol
            </span>
          </div>

          <button
            type="submit"
            disabled={isLoading}
            className="w-full py-3 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-xl shadow-xs transition-all flex items-center justify-center gap-2 disabled:opacity-50 mt-2 cursor-pointer"
          >
            {isLoading ? (
              <span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
            ) : (
              <>
                <span>Complete Registration &amp; Open Wallet</span>
                <CheckCircle2 className="w-4 h-4" />
              </>
            )}
          </button>
        </form>
      )}

      <div className="mt-6 pt-4 border-t border-slate-100 text-center text-xs text-slate-500">
        Already have an account?{' '}
        <Link to="/login" className="font-bold text-blue-600 hover:underline">
          Sign In
        </Link>
      </div>
    </div>
  );
}
