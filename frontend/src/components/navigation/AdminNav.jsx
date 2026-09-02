import React from 'react';
import { NavLink } from 'react-router-dom';
import { ROUTES } from '../../constants/routes';
import {
  ShieldAlert,
  Users,
  BadgePercent,
  FileSpreadsheet,
  History,
  Coins,
  LayoutDashboard
} from 'lucide-react';

/**
 * SuperAdmin Control Console Pill Navigation Tabs.
 */
export default function AdminNav({ className = '' }) {
  const adminItems = [
    { to: ROUTES.ADMIN_DASHBOARD, label: 'Overview', icon: LayoutDashboard },
    { to: ROUTES.ADMIN_USERS, label: 'Users & Orgs', icon: Users },
    { to: ROUTES.ADMIN_COMPLIANCE, label: 'Compliance & KYB', icon: ShieldAlert },
    { to: ROUTES.ADMIN_FEES, label: 'Fee Matrix', icon: BadgePercent },
    { to: ROUTES.ADMIN_RECONCILIATION, label: 'Reconciliation', icon: FileSpreadsheet },
    { to: ROUTES.ADMIN_THRIFT, label: 'Thrift Oversight', icon: Coins },
    { to: ROUTES.ADMIN_AUDIT_LOGS, label: 'Audit Logs', icon: History }
  ];

  return (
    <div className={`flex items-center gap-2 overflow-x-auto no-scrollbar py-2 border-b border-slate-200/80 mb-6 ${className}`}>
      {adminItems.map((item) => {
        const Icon = item.icon;
        return (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === ROUTES.ADMIN_DASHBOARD}
            className={({ isActive }) =>
              `inline-flex items-center gap-2 px-3.5 py-1.5 rounded-xl text-xs font-semibold transition-all whitespace-nowrap select-none ${
                isActive
                  ? 'bg-brand-600 text-white shadow-xs'
                  : 'bg-white border border-slate-200 text-slate-600 hover:bg-slate-50 hover:text-slate-900'
              }`
            }
          >
            <Icon size={14} />
            <span>{item.label}</span>
          </NavLink>
        );
      })}
    </div>
  );
}
