import React from 'react';
import { NavLink } from 'react-router-dom';
import { ROUTES } from '../../constants/routes';
import {
  LayoutDashboard,
  Wallet,
  Zap,
  Receipt,
  Users,
  Package,
  PiggyBank,
  Settings
} from 'lucide-react';

/**
 * Topbar customer navigation pill tabs.
 */
export default function CustomerNav({ className = '' }) {
  const navItems = [
    { to: ROUTES.DASHBOARD, label: 'Dashboard', icon: LayoutDashboard },
    { to: ROUTES.WALLET, label: 'Wallet & Payouts', icon: Wallet },
    { to: ROUTES.VAS, label: 'VAS & Bills', icon: Zap },
    { to: ROUTES.PAYROLL, label: 'Payroll', icon: Receipt },
    { to: ROUTES.STAFF, label: 'Staff & Roles', icon: Users },
    { to: ROUTES.INVENTORY, label: 'ERP & Catalog', icon: Package },
    { to: ROUTES.SAVINGS, label: 'Savings & Thrift', icon: PiggyBank },
    { to: ROUTES.SETTINGS, label: 'Settings', icon: Settings }
  ];

  return (
    <nav className={`flex items-center gap-2 overflow-x-auto no-scrollbar py-2 ${className}`}>
      {navItems.map((item) => {
        const Icon = item.icon;
        return (
          <NavLink
            key={item.to}
            to={item.to}
            className={({ isActive }) =>
              `inline-flex items-center gap-2 px-4 py-2 rounded-full text-xs font-semibold transition-all whitespace-nowrap select-none ${
                isActive
                  ? 'bg-brand-600 text-white shadow-xs shadow-brand-500/20'
                  : 'bg-white border border-slate-200 text-slate-700 hover:bg-slate-50 hover:border-slate-300'
              }`
            }
          >
            <Icon size={15} strokeWidth={1.75} />
            <span>{item.label}</span>
          </NavLink>
        );
      })}
    </nav>
  );
}
