import React from 'react';
import { NavLink } from 'react-router-dom';
import { useAuth, ROLES } from '../../context/AuthContext';
import {
  LayoutDashboard,
  ShieldCheck,
  Percent,
  Sliders,
  RefreshCw,
  FileSpreadsheet,
  Users2,
  Building,
  UserCheck,
  Briefcase,
  Banknote,
  PiggyBank,
  Package,
  Layers,
  Contact,
  Truck,
  ShoppingCart,
  Receipt,
  FileCheck,
  ReceiptText,
  LineChart,
  Wallet,
  ArrowRightLeft,
  CreditCard,
  Smartphone,
  Shield,
  BadgePercent,
  Compass,
  FileText
} from 'lucide-react';

export default function Sidebar() {
  const { activeRole } = useAuth();

  // Navigation schema per role
  const getNavSections = () => {
    if (activeRole === ROLES.SUPER_ADMIN) {
      return [
        {
          title: 'Control Plane',
          items: [
            { path: '/admin', label: 'Platform Overview', icon: LayoutDashboard },
            { path: '/admin/compliance', label: 'KYC & KYB Compliance', icon: ShieldCheck },
            { path: '/admin/reconciliation', label: 'Financial Reconciliation', icon: RefreshCw },
          ]
        },
        {
          title: 'Policies & Economics',
          items: [
            { path: '/admin/fees', label: 'Fee Policy Engine', icon: Percent },
            { path: '/admin/savings-policies', label: 'Savings Interest Policies', icon: Sliders },
          ]
        },
        {
          title: 'Governance & Audits',
          items: [
            { path: '/admin/audit-logs', label: 'Platform Audit Trail', icon: FileSpreadsheet },
            { path: '/admin/governance', label: 'Admin Permissions', icon: Users2 },
          ]
        }
      ];
    }

    if (activeRole === ROLES.ORGANIZATION) {
      return [
        {
          title: 'Corporate Treasury',
          items: [
            { path: '/org', label: 'Org Dashboard', icon: LayoutDashboard },
            { path: '/org/kyb', label: 'KYB Onboarding & CAC', icon: Building },
            { path: '/org/payroll', label: 'Payroll Engine', icon: Banknote },
          ]
        },
        {
          title: 'Workforce & HRIS',
          items: [
            { path: '/org/staff', label: 'Staff Directory', icon: UserCheck },
            { path: '/org/departments', label: 'Depts, Roles & Levels', icon: Layers },
            { path: '/org/loans', label: 'Corporate Loan Plans', icon: BadgePercent },
            { path: '/org/savings', label: 'Corporate Savings', icon: PiggyBank },
            { path: '/org/recruitment', label: 'Recruitment & Jobs', icon: Briefcase },
          ]
        },
        {
          title: 'ERP & Invoicing',
          items: [
            { path: '/org/erp/inventory', label: 'Inventory & Stock', icon: Package },
            { path: '/org/erp/services', label: 'Services Catalog', icon: Compass },
            { path: '/org/erp/orders', label: 'Purchase & Sales Orders', icon: ShoppingCart },
            { path: '/org/erp/invoices', label: 'Invoices & Receipts', icon: Receipt },
            { path: '/org/erp/expenses', label: 'Operating Expenses', icon: FileText },
            { path: '/org/erp/vouchers', label: 'Company Disbursement', icon: FileCheck },
            { path: '/org/erp/crm', label: 'Customer & Supplier CRM', icon: Contact },
            { path: '/org/erp/reports', label: 'Financial Accounting', icon: LineChart },
          ]
        }
      ];
    }

    // Consumer / Staff role
    return [
      {
        title: 'Personal Finance',
        items: [
          { path: '/consumer', label: 'My Wallet', icon: Wallet },
          { path: '/consumer/transfers', label: 'Transfers & Payouts', icon: ArrowRightLeft },
          { path: '/consumer/cards', label: 'Card Funding & Cards', icon: CreditCard },
          { path: '/consumer/vas', label: 'Airtime & Data Bundles', icon: Smartphone },
          { path: '/consumer/kyc', label: 'Identity & KYC Levels', icon: Shield },
        ]
      },
      {
        title: 'Work & Benefits',
        items: [
          { path: '/consumer/work', label: 'Workplace & Payslips', icon: Briefcase },
          { path: '/consumer/loans', label: 'Salary Advance Loans', icon: Banknote },
          { path: '/consumer/savings', label: 'Savings & Fixed-Lock', icon: PiggyBank },
          { path: '/consumer/thrift', label: 'Thrift (Ajo / Esusu)', icon: Users2 },
        ]
      },
      {
        title: 'Public Portal',
        items: [
          { path: '/careers', label: 'Open Job Board', icon: Compass },
        ]
      }
    ];
  };

  const sections = getNavSections();

  return (
    <aside className="w-64 shrink-0 bg-white border-r border-slate-200/80 min-h-[calc(100vh-61px)] p-4 flex flex-col justify-between hidden md:flex">
      <div className="space-y-6">
        {sections.map((sec, secIdx) => (
          <div key={secIdx}>
            <h5 className="px-3 text-[11px] font-bold uppercase tracking-wider text-slate-400 mb-2">
              {sec.title}
            </h5>
            <nav className="space-y-1">
              {sec.items.map((item) => {
                const Icon = item.icon;
                return (
                  <NavLink
                    key={item.path}
                    to={item.path}
                    end={item.path === '/admin' || item.path === '/org' || item.path === '/consumer'}
                    className={({ isActive }) =>
                      `flex items-center gap-3 px-3 py-2.5 rounded-xl text-xs font-semibold transition-all ${
                        isActive
                          ? activeRole === ROLES.SUPER_ADMIN
                            ? 'bg-purple-50 text-purple-900 font-bold border border-purple-200/60 shadow-xs'
                            : activeRole === ROLES.ORGANIZATION
                            ? 'bg-blue-50 text-blue-900 font-bold border border-blue-200/60 shadow-xs'
                            : 'bg-emerald-50 text-emerald-900 font-bold border border-emerald-200/60 shadow-xs'
                          : 'text-slate-600 hover:bg-slate-50 hover:text-slate-900'
                      }`
                    }
                  >
                    <Icon className="w-4 h-4 shrink-0" />
                    <span className="truncate">{item.label}</span>
                  </NavLink>
                );
              })}
            </nav>
          </div>
        ))}
      </div>

      {/* Footer System Status */}
      <div className="p-3 bg-slate-50 rounded-2xl border border-slate-200/70 text-xs">
        <div className="flex items-center justify-between mb-1">
          <span className="font-semibold text-slate-700">Ledger Engine</span>
          <span className="flex items-center gap-1.5 text-[11px] text-emerald-700 font-bold">
            <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
            Healthy
          </span>
        </div>
        <p className="text-[11px] text-slate-400">Version 1.0.0 • PostgreSQL Double-Entry Ledger</p>
      </div>
    </aside>
  );
}
