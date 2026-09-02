import React, { useState } from 'react';
import { useAuth, ROLES } from '../../context/AuthContext';
import { formatCurrency } from '../../utils/formatters';
import {
  ShieldAlert,
  Building2,
  User,
  Eye,
  EyeOff,
  LogOut,
  ChevronDown,
  Sparkles,
  Briefcase,
  Layers,
  ArrowRightLeft
} from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';

export default function Navbar() {
  const {
    user,
    activeRole,
    switchRole,
    activeOrg,
    balanceVisible,
    toggleBalancePrivacy,
    logout,
    loginAsDemo,
    isAuthenticated
  } = useAuth();

  const [showRoleMenu, setShowRoleMenu] = useState(false);
  const [showUserMenu, setShowUserMenu] = useState(false);
  const navigate = useNavigate();

  // Role details
  const getRoleBadge = () => {
    if (activeRole === ROLES.SUPER_ADMIN) {
      return {
        label: 'Super Admin',
        icon: ShieldAlert,
        bg: 'bg-purple-100 text-purple-800 border-purple-200'
      };
    }
    if (activeRole === ROLES.ORGANIZATION) {
      return {
        label: 'Organization (B2B)',
        icon: Building2,
        bg: 'bg-blue-100 text-blue-800 border-blue-200'
      };
    }
    return {
      label: 'Consumer / Staff',
      icon: User,
      bg: 'bg-emerald-100 text-emerald-800 border-emerald-200'
    };
  };

  const roleInfo = getRoleBadge();
  const RoleIcon = roleInfo.icon;

  const handleRoleChange = (newRole) => {
    switchRole(newRole);
    setShowRoleMenu(false);
    if (newRole === ROLES.SUPER_ADMIN) {
      navigate('/admin');
    } else if (newRole === ROLES.ORGANIZATION) {
      navigate('/org');
    } else {
      navigate('/consumer');
    }
  };

  const handleDemoSwitch = (role) => {
    loginAsDemo(role);
    setShowRoleMenu(false);
    if (role === ROLES.SUPER_ADMIN) navigate('/admin');
    else if (role === ROLES.ORGANIZATION) navigate('/org');
    else navigate('/consumer');
  };

  return (
    <header className="sticky top-0 z-40 bg-white/95 backdrop-blur-md border-b border-slate-200/80 px-4 lg:px-8 py-2.5 transition-all">
      <div className="flex items-center justify-between gap-4">
        {/* Left: Brand & Active Workspace */}
        <div className="flex items-center gap-6">
          <Link to="/" className="flex items-center gap-2.5 group">
            <div className="w-9 h-9 rounded-xl bg-blue-600 text-white flex items-center justify-center font-black text-lg tracking-wider shadow-xs shadow-blue-500/20 group-hover:scale-105 transition-transform">
              C
            </div>
            <div>
              <span className="font-extrabold text-base tracking-tight text-slate-900 block leading-tight">
                CebizPay
              </span>
              <span className="text-[10px] font-semibold text-slate-400 uppercase tracking-widest block">
                Fintech &amp; ERP
              </span>
            </div>
          </Link>

          {/* Active Tenant indicator */}
          {activeRole === ROLES.ORGANIZATION && activeOrg && (
            <div className="hidden md:flex items-center gap-2 px-3 py-1.5 rounded-xl bg-slate-100 border border-slate-200 text-xs">
              <Building2 className="w-3.5 h-3.5 text-slate-500" />
              <span className="font-bold text-slate-800 truncate max-w-[180px]">{activeOrg.name}</span>
              <span className="text-[10px] bg-emerald-100 text-emerald-800 px-1.5 py-0.5 rounded font-bold">
                {activeOrg.kybStatus}
              </span>
            </div>
          )}
        </div>

        {/* Center: Multi-Role Surface Switcher (Interactive for Presentation) */}
        <div className="hidden lg:flex items-center bg-slate-100/80 p-1 rounded-xl border border-slate-200/60 text-xs font-semibold gap-1">
          <button
            onClick={() => handleRoleChange(ROLES.SUPER_ADMIN)}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg transition-all ${
              activeRole === ROLES.SUPER_ADMIN
                ? 'bg-white text-purple-900 shadow-xs font-bold border border-slate-200/50'
                : 'text-slate-600 hover:text-slate-900 hover:bg-slate-200/50'
            }`}
          >
            <ShieldAlert className="w-3.5 h-3.5 text-purple-600" />
            Super Admin
          </button>
          <button
            onClick={() => handleRoleChange(ROLES.ORGANIZATION)}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg transition-all ${
              activeRole === ROLES.ORGANIZATION
                ? 'bg-white text-blue-900 shadow-xs font-bold border border-slate-200/50'
                : 'text-slate-600 hover:text-slate-900 hover:bg-slate-200/50'
            }`}
          >
            <Building2 className="w-3.5 h-3.5 text-blue-600" />
            Organization (B2B)
          </button>
          <button
            onClick={() => handleRoleChange(ROLES.CONSUMER)}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg transition-all ${
              activeRole === ROLES.CONSUMER
                ? 'bg-white text-emerald-900 shadow-xs font-bold border border-slate-200/50'
                : 'text-slate-600 hover:text-slate-900 hover:bg-slate-200/50'
            }`}
          >
            <User className="w-3.5 h-3.5 text-emerald-600" />
            Staff &amp; Consumer
          </button>
          <Link
            to="/careers"
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-slate-500 hover:text-slate-800 transition-colors"
          >
            <Briefcase className="w-3.5 h-3.5" />
            Job Board
          </Link>
        </div>

        {/* Right: Balance Quick Pill, Role Switcher Mobile & User Menu */}
        <div className="flex items-center gap-3">
          {/* Wallet Balance Pill */}
          <div className="flex items-center gap-2 px-3 py-1.5 rounded-xl bg-slate-50 border border-slate-200 text-xs">
            <span className="text-slate-500 font-medium hidden sm:inline">Balance:</span>
            <span className="font-bold text-slate-900 font-mono">
              {balanceVisible
                ? formatCurrency(activeRole === ROLES.ORGANIZATION ? (activeOrg?.balance || 14250000) : 485500.00)
                : '••••••••'}
            </span>
            <button
              onClick={toggleBalancePrivacy}
              className="text-slate-400 hover:text-slate-600 p-0.5 rounded transition-colors"
              title={balanceVisible ? 'Hide Balance' : 'Show Balance'}
            >
              {balanceVisible ? <EyeOff className="w-3.5 h-3.5" /> : <Eye className="w-3.5 h-3.5" />}
            </button>
          </div>

          {/* Quick Demo Switcher Dropdown (Mobile / Desktop) */}
          <div className="relative">
            <button
              onClick={() => setShowRoleMenu((prev) => !prev)}
              className={`flex items-center gap-1.5 px-3 py-1.5 rounded-xl border text-xs font-bold transition-all shadow-xs ${roleInfo.bg}`}
            >
              <RoleIcon className="w-3.5 h-3.5" />
              <span className="hidden sm:inline">{roleInfo.label}</span>
              <ChevronDown className="w-3.5 h-3.5 opacity-60" />
            </button>

            {showRoleMenu && (
              <div className="absolute right-0 mt-2 w-64 bg-white rounded-2xl border border-slate-200 shadow-xl p-2 z-50 animate-in fade-in zoom-in-95 duration-150">
                <div className="px-3 py-2 border-b border-slate-100 mb-1">
                  <p className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">
                    Switch Active Surface
                  </p>
                </div>
                <div className="space-y-1">
                  <button
                    onClick={() => handleDemoSwitch(ROLES.SUPER_ADMIN)}
                    className={`w-full flex items-center justify-between p-2.5 rounded-xl text-left text-xs font-semibold transition-colors ${
                      activeRole === ROLES.SUPER_ADMIN ? 'bg-purple-50 text-purple-900' : 'hover:bg-slate-50 text-slate-700'
                    }`}
                  >
                    <div className="flex items-center gap-2.5">
                      <div className="p-1.5 rounded-lg bg-purple-100 text-purple-700">
                        <ShieldAlert className="w-4 h-4" />
                      </div>
                      <div>
                        <span className="block font-bold">Super Admin Portal</span>
                        <span className="text-[10px] text-slate-500 font-normal">Platform control plane &amp; CDD</span>
                      </div>
                    </div>
                  </button>

                  <button
                    onClick={() => handleDemoSwitch(ROLES.ORGANIZATION)}
                    className={`w-full flex items-center justify-between p-2.5 rounded-xl text-left text-xs font-semibold transition-colors ${
                      activeRole === ROLES.ORGANIZATION ? 'bg-blue-50 text-blue-900' : 'hover:bg-slate-50 text-slate-700'
                    }`}
                  >
                    <div className="flex items-center gap-2.5">
                      <div className="p-1.5 rounded-lg bg-blue-100 text-blue-700">
                        <Building2 className="w-4 h-4" />
                      </div>
                      <div>
                        <span className="block font-bold">Organization Portal</span>
                        <span className="text-[10px] text-slate-500 font-normal">Corporate treasury &amp; ERP</span>
                      </div>
                    </div>
                  </button>

                  <button
                    onClick={() => handleDemoSwitch(ROLES.CONSUMER)}
                    className={`w-full flex items-center justify-between p-2.5 rounded-xl text-left text-xs font-semibold transition-colors ${
                      activeRole === ROLES.CONSUMER ? 'bg-emerald-50 text-emerald-900' : 'hover:bg-slate-50 text-slate-700'
                    }`}
                  >
                    <div className="flex items-center gap-2.5">
                      <div className="p-1.5 rounded-lg bg-emerald-100 text-emerald-700">
                        <User className="w-4 h-4" />
                      </div>
                      <div>
                        <span className="block font-bold">Staff &amp; Consumer</span>
                        <span className="text-[10px] text-slate-500 font-normal">Wallet, loans, savings &amp; thrift</span>
                      </div>
                    </div>
                  </button>
                </div>
              </div>
            )}
          </div>

          {/* User Account / Logout */}
          <div className="relative">
            <button
              onClick={() => setShowUserMenu((prev) => !prev)}
              className="w-9 h-9 rounded-xl bg-slate-100 border border-slate-200 text-slate-700 hover:bg-slate-200 flex items-center justify-center font-bold text-xs transition-colors"
            >
              {user?.name ? user.name.slice(0, 2).toUpperCase() : 'HA'}
            </button>

            {showUserMenu && (
              <div className="absolute right-0 mt-2 w-56 bg-white rounded-2xl border border-slate-200 shadow-xl p-2 z-50 animate-in fade-in zoom-in-95 duration-150">
                <div className="px-3 py-2 border-b border-slate-100 mb-1">
                  <p className="text-xs font-bold text-slate-900 truncate">{user?.name || 'Honour Ajani'}</p>
                  <p className="text-[11px] text-slate-500 truncate">{user?.email || 'honour@gmail.com'}</p>
                </div>
                <button
                  onClick={() => {
                    setShowUserMenu(false);
                    logout();
                    navigate('/login');
                  }}
                  className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold text-rose-600 hover:bg-rose-50 rounded-xl transition-colors"
                >
                  <LogOut className="w-4 h-4" />
                  Sign Out
                </button>
              </div>
            )}
          </div>
        </div>
      </div>
    </header>
  );
}
