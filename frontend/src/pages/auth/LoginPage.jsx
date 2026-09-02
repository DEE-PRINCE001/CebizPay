import React, { useState } from 'react';
import { useAuth, ROLES } from '../../context/AuthContext';
import { useToast } from '../../context/ToastContext';
import { useNavigate, Link } from 'react-router-dom';
import { Lock, Mail, ShieldAlert, Building2, User, ArrowRight, Sparkles } from 'lucide-react';
import { DEMO_USERS } from '../../utils/constants';

export default function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const { login, loginAsDemo } = useAuth();
  const { showSuccess, showError } = useToast();
  const navigate = useNavigate();

  const handleLogin = async (e) => {
    e.preventDefault();
    if (!email || !password) {
      showError('Required Fields', 'Please enter both your email address and password.');
      return;
    }

    setIsLoading(true);
    try {
      const res = await login(email, password);
      showSuccess('Welcome Back', 'Authentication successful.');
      if (email.toLowerCase().includes('admin') || email.toLowerCase().includes('honour')) {
        navigate('/admin');
      } else if (email.toLowerCase().includes('org') || email.toLowerCase().includes('ceo')) {
        navigate('/org');
      } else {
        navigate('/consumer');
      }
    } catch (err) {
      // Allow demo login fallback if backend isn't actively seeded yet
      showError('Authentication Notice', err.message || 'Invalid credentials.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleQuickDemo = (roleType) => {
    loginAsDemo(roleType);
    showSuccess('Demo Session Activated', `Logged in as ${roleType} role.`);
    if (roleType === ROLES.SUPER_ADMIN) navigate('/admin');
    else if (roleType === ROLES.ORGANIZATION) navigate('/org');
    else navigate('/consumer');
  };

  return (
    <div className="max-w-md w-full mx-auto">
      {/* Login Card */}
      <div className="bg-white rounded-3xl border border-slate-200/80 shadow-xl p-6 sm:p-8">
        <div className="text-center mb-8">
          <div className="w-12 h-12 rounded-2xl bg-blue-50 text-blue-600 flex items-center justify-center mx-auto mb-3 border border-blue-100">
            <Lock className="w-6 h-6" />
          </div>
          <h2 className="text-2xl font-bold tracking-tight text-slate-900">Sign in to CebizPay</h2>
          <p className="text-xs text-slate-500 mt-1">
            Access the unified multi-tenant financial operating system
          </p>
        </div>

        {/* 1-Click Quick Demo Selectors */}
        <div className="mb-6 p-3 bg-slate-50 rounded-2xl border border-slate-200/80">
          <div className="flex items-center gap-1.5 text-slate-700 font-bold text-xs mb-2">
            <Sparkles className="w-3.5 h-3.5 text-blue-600" />
            <span>1-Click Multi-Role Demo Logins</span>
          </div>
          <div className="grid grid-cols-3 gap-1.5">
            <button
              type="button"
              onClick={() => handleQuickDemo(ROLES.SUPER_ADMIN)}
              className="px-2 py-2 rounded-xl bg-purple-50 border border-purple-200 text-purple-900 hover:bg-purple-100 text-[11px] font-bold transition-all text-center flex flex-col items-center gap-1"
            >
              <ShieldAlert className="w-4 h-4 text-purple-600" />
              Super Admin
            </button>
            <button
              type="button"
              onClick={() => handleQuickDemo(ROLES.ORGANIZATION)}
              className="px-2 py-2 rounded-xl bg-blue-50 border border-blue-200 text-blue-900 hover:bg-blue-100 text-[11px] font-bold transition-all text-center flex flex-col items-center gap-1"
            >
              <Building2 className="w-4 h-4 text-blue-600" />
              Org (B2B)
            </button>
            <button
              type="button"
              onClick={() => handleQuickDemo(ROLES.CONSUMER)}
              className="px-2 py-2 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-900 hover:bg-emerald-100 text-[11px] font-bold transition-all text-center flex flex-col items-center gap-1"
            >
              <User className="w-4 h-4 text-emerald-600" />
              Staff / User
            </button>
          </div>
        </div>

        {/* Form */}
        <form onSubmit={handleLogin} className="space-y-4">
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1.5">Email Address</label>
            <div className="relative">
              <Mail className="w-4 h-4 text-slate-400 absolute left-3.5 top-1/2 -translate-y-1/2 pointer-events-none" />
              <input
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="name@company.com"
                className="w-full pl-10 pr-4 py-2.5 text-xs bg-white border border-slate-200 rounded-xl focus:border-blue-600 focus:ring-2 focus:ring-blue-500/20 outline-hidden transition-all placeholder:text-slate-400"
              />
            </div>
          </div>

          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label className="block text-xs font-semibold text-slate-700">Password</label>
              <span className="text-[11px] text-blue-600 hover:underline cursor-pointer">Forgot?</span>
            </div>
            <div className="relative">
              <Lock className="w-4 h-4 text-slate-400 absolute left-3.5 top-1/2 -translate-y-1/2 pointer-events-none" />
              <input
                type="password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••••••"
                className="w-full pl-10 pr-4 py-2.5 text-xs bg-white border border-slate-200 rounded-xl focus:border-blue-600 focus:ring-2 focus:ring-blue-500/20 outline-hidden transition-all placeholder:text-slate-400"
              />
            </div>
          </div>

          <button
            type="submit"
            disabled={isLoading}
            className="w-full mt-2 py-3 px-4 bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold rounded-xl transition-all shadow-sm flex items-center justify-center gap-2 disabled:opacity-50"
          >
            {isLoading ? (
              <span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
            ) : (
              <>
                <span>Sign In</span>
                <ArrowRight className="w-4 h-4" />
              </>
            )}
          </button>
        </form>

        <div className="mt-6 text-center text-xs text-slate-500">
          Looking to join or register?{' '}
          <Link to="/register" className="font-bold text-blue-600 hover:underline">
            Consumer Phone Registration
          </Link>
        </div>
      </div>
    </div>
  );
}
