import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth, ROLES } from '../../context/AuthContext';
import { useToast } from '../../context/ToastContext';
import { authApi } from '../../api/authApi';
import { Lock, Mail, ArrowRight, ShieldCheck, Building, User, AlertCircle } from 'lucide-react';

export default function LoginPage() {
  const [email, setEmail] = useState('honour@gmail.com');
  const [password, setPassword] = useState('CephHonSec.123tryit');
  const [formError, setFormError] = useState(null);
  const [isLoading, setIsLoading] = useState(false);

  const { loginWithPreset, setActiveRole, setUser } = useAuth();
  const { showSuccess, showError } = useToast();
  const navigate = useNavigate();

  const handleLogin = async (e) => {
    e.preventDefault();
    setFormError(null);
    setIsLoading(true);

    try {
      const response = await authApi.login(email, password);
      if (response && response.accessToken) {
        localStorage.setItem('cebizpay_token', response.accessToken);
        if (response.refreshToken) {
          localStorage.setItem('cebizpay_refresh_token', response.refreshToken);
        }

        const userObj = {
          id: response.userId || 'usr_current',
          email: email,
          name: response.firstName ? `${response.firstName} ${response.lastName}` : email.split('@')[0],
          roles: response.roles || ['SuperAdmin']
        };
        setUser(userObj);

        // Determine destination portal based on email or roles
        if (email.includes('honour') || response.roles?.includes('SuperAdmin')) {
          setActiveRole(ROLES.SUPER_ADMIN);
          showSuccess('Authenticated as Super Admin', 'Connected to live Central Double-Entry Ledger.');
          navigate('/admin');
        } else if (email.includes('apex') || email.includes('org')) {
          setActiveRole(ROLES.ORGANIZATION);
          showSuccess('Authenticated as Corporate Organization', 'Apex Global Technologies Ltd treasury loaded.');
          navigate('/org');
        } else {
          setActiveRole(ROLES.CONSUMER);
          showSuccess('Authenticated as Consumer', 'Personal wallet & workplace benefits loaded.');
          navigate('/consumer');
        }
      } else {
        const errorMsg = response?.errors?.[0] || 'Authentication failed. Please check your credentials.';
        setFormError(errorMsg);
        showError('Login Failed', errorMsg);
      }
    } catch (err) {
      const errorMsg = err.message || 'Invalid email or password. Please verify and try again.';
      setFormError(errorMsg);
      showError('Authentication Error', errorMsg);
    } finally {
      setIsLoading(false);
    }
  };

  const handleQuickPreset = (role) => {
    setFormError(null);
    loginWithPreset(role);
    if (role === ROLES.SUPER_ADMIN) {
      setEmail('honour@gmail.com');
      setPassword('CephHonSec.123tryit');
      navigate('/admin');
    } else if (role === ROLES.ORGANIZATION) {
      setEmail('org@apextech.com');
      setPassword('CorporatePass123!');
      navigate('/org');
    } else {
      setEmail('newuser.test@example.com');
      setPassword('ConsumerPass123!');
      navigate('/consumer');
    }
  };

  return (
    <div className="bg-white rounded-3xl border border-slate-200/80 p-8 sm:p-10 shadow-xl max-w-md w-full mx-auto text-left">
      <div className="text-center mb-6">
        <h2 className="text-2xl font-bold text-slate-900 tracking-tight">Sign In to CebizPay</h2>
        <p className="text-xs text-slate-500 mt-1">Multi-Tenant Financial OS &amp; Payroll Platform</p>
      </div>

      {/* Prominent Error Banner */}
      {formError && (
        <div className="mb-5 p-3.5 bg-rose-50 rounded-2xl border border-rose-200 text-rose-900 flex items-start gap-2.5 text-xs animate-in fade-in">
          <AlertCircle className="w-4 h-4 text-rose-600 shrink-0 mt-0.5" />
          <div className="flex-1 min-w-0">
            <span className="font-bold block">Authentication Error</span>
            <span className="text-[11px] leading-relaxed text-rose-700">{formError}</span>
          </div>
        </div>
      )}

      {/* Quick Role Presets for Instant Testing */}
      <div className="mb-6 p-3 bg-slate-50 rounded-2xl border border-slate-100">
        <span className="text-[10px] font-bold uppercase tracking-wider text-slate-400 block mb-2 text-center">
          1-Click Live Test Presets
        </span>
        <div className="grid grid-cols-3 gap-1.5">
          <button
            type="button"
            onClick={() => handleQuickPreset(ROLES.SUPER_ADMIN)}
            className="px-2 py-2 text-[11px] font-bold bg-white hover:bg-blue-50 text-blue-700 border border-slate-200 rounded-xl transition-all shadow-2xs flex flex-col items-center gap-1 cursor-pointer"
          >
            <ShieldCheck className="w-3.5 h-3.5 text-blue-600" />
            Super Admin
          </button>
          <button
            type="button"
            onClick={() => handleQuickPreset(ROLES.ORGANIZATION)}
            className="px-2 py-2 text-[11px] font-bold bg-white hover:bg-purple-50 text-purple-700 border border-slate-200 rounded-xl transition-all shadow-2xs flex flex-col items-center gap-1 cursor-pointer"
          >
            <Building className="w-3.5 h-3.5 text-purple-600" />
            Org (B2B)
          </button>
          <button
            type="button"
            onClick={() => handleQuickPreset(ROLES.CONSUMER)}
            className="px-2 py-2 text-[11px] font-bold bg-white hover:bg-emerald-50 text-emerald-700 border border-slate-200 rounded-xl transition-all shadow-2xs flex flex-col items-center gap-1 cursor-pointer"
          >
            <User className="w-3.5 h-3.5 text-emerald-600" />
            Staff (B2C)
          </button>
        </div>
      </div>

      <form onSubmit={handleLogin} className="space-y-4 text-xs">
        <div>
          <label className="block font-semibold text-slate-700 mb-1.5">Email Address</label>
          <div className="relative">
            <Mail className="w-4 h-4 text-slate-400 absolute left-3.5 top-1/2 -translate-y-1/2 pointer-events-none" />
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="name@company.com"
              className="w-full pl-10 pr-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-medium focus:border-blue-600 focus:ring-2 focus:ring-blue-500/20 outline-hidden"
            />
          </div>
        </div>

        <div>
          <label className="block font-semibold text-slate-700 mb-1.5">Password</label>
          <div className="relative">
            <Lock className="w-4 h-4 text-slate-400 absolute left-3.5 top-1/2 -translate-y-1/2 pointer-events-none" />
            <input
              type="password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
              className="w-full pl-10 pr-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-medium focus:border-blue-600 focus:ring-2 focus:ring-blue-500/20 outline-hidden"
            />
          </div>
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
              <span>Sign In to Portal</span>
              <ArrowRight className="w-4 h-4" />
            </>
          )}
        </button>
      </form>

      <div className="mt-6 pt-4 border-t border-slate-100 text-center text-xs text-slate-500">
        New to CebizPay?{' '}
        <Link to="/register" className="font-bold text-blue-600 hover:underline">
          Create an Account
        </Link>
      </div>
    </div>
  );
}
